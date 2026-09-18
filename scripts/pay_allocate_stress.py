#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""收款账号分配系统 —— allocate（F2）并发压测 / 回归脚本

对应设计文档 §13：S2 完成后立即做一次并发压测，模拟
「多个请求同时匹配同一账号且合计超额度」，验证额度不会超发。
S7 验收时直接重跑本脚本。

设计取舍：
  * 并发/匹配断言全部走**真实 HTTP** —— 只有 HTTP 才能拿到独立请求上下文，
    SqlSugarScope 的连接是按请求上下文隔离的，进程内并发测不出真并发。
  * 开放接口（`/api/pay/allocate`、`/api/pay/status`）**走签名鉴权**（S5 起），
    脚本自己建一个 `scopes=allocate` 的开放身份，跑完删掉。
  * 测试账号/订单的造数与核对**直连 PostgreSQL** —— 免登录、快、且能造出
    「已用 95 / 已用完 / 已停用」这类 API 不好造的状态。
  * 只有「追加额度」这一处刻意**走真实后台 API**（`/api/payAccount/addQuota`），
    因为它要验证的正是「追加额度 → 状态回置 → 重新参与匹配」这条后台链路。

用法（需要 PG 在跑、后端已启动）：

    ~/.workbuddy-ai/binaries/python/envs/default/bin/python scripts/pay_allocate_stress.py

退出码：0 = 全部通过；1 = 存在失败断言
"""

import os
import sys
import time
import datetime
from concurrent.futures import ThreadPoolExecutor
from decimal import Decimal

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _pay_common import (  # noqa: E402
    BASE, PG_HOST, PG_PORT, PG_DB, PASS, FAIL, assert_true, section, report,
    call, sign_call, admin_call, admin_login, ok, dec,
    pg_query, pg_one, pg_scalar, pg_exec, wait_backend,
)

# 枚举值（与 C# 侧保持一致）
ACCOUNT_ENABLED, ACCOUNT_DISABLED, ACCOUNT_EXHAUSTED = 1, 2, 3
ORDER_PENDING, ORDER_PARTIAL, ORDER_COMPLETED, ORDER_EXPIRED = 1, 2, 3, 4

# 测试数据统一用该前缀，便于一键清理（type 列 varchar(32)，别超）
TAG = "stress_"

# 造数用的账号 Id 走一个**远离雪花区间**的高位段。
# 雪花尾段当前约 8.5e14（15 位）且随年份增长，7e15 在几十年内不会撞上。
_ID_SEQ = [7_000_000_000_000_000]

AK = "stress_ak_" + datetime.datetime.now().strftime("%m%d%H%M%S")
SK = "stress_sk_secret_" + datetime.datetime.now().strftime("%m%d%H%M%S")

TOKEN = None


# ─────────────────────────── 开放接口 ───────────────────────────

def allocate(amount, type_, external_no=None):
    body = {"type": type_, "amount": amount}
    if external_no:
        body["externalNo"] = external_no
    return sign_call("POST", "/api/pay/allocate", body, ak=AK, sk=SK)[1]


def order_status(order_no):
    return sign_call("GET", f"/api/pay/status?orderNo={order_no}", ak=AK, sk=SK)[1]


# ─────────────────────────── 造数与核对（直连 PG） ───────────────────────────

def reset():
    """清空本脚本产生的测试数据（幂等）。"""
    # ★ 审计行必须**最先**删：下面两条子查询依赖 pay_order / pay_account 还在。
    #
    # ★ 为什么不能只按 targetno 抓：订单号是**自包含雪花**（纯数字、无业务前缀），
    #   所以 `targetno like 'stress_%'` 对订单审计行**永远匹配不上**。
    #   实测：这一条会让每轮压测都留下十几行审计孤儿，而脚本照样打印「已清干净」——
    #   「清理语句跑了」不等于「该清的行真被清了」，是两件事。
    #   可靠的做法是按 clientkey 抓（clientkey 存的就是 accessKey，前缀挂在那里）。
    pg_exec(
        "delete from pay_audit_log"
        " where clientkey like %s"
        "    or targetno like %s"
        "    or (targettype = 'PayAccount' and targetid in"
        "        (select id from pay_account where type like %s))"
        "    or (targettype = 'Order' and targetid in"
        "        (select id from pay_order where accountid in"
        "         (select id from pay_account where type like %s)))",
        ("stress_ak_%", TAG + "%", TAG + "%", TAG + "%"))
    pg_exec("delete from pay_order_event where orderno in "
            "(select orderno from pay_order where accountid in "
            "(select id from pay_account where type like %s))", (TAG + "%",))
    pg_exec("delete from pay_order where accountid in "
            "(select id from pay_account where type like %s)", (TAG + "%",))
    pg_exec("delete from pay_account where type like %s", (TAG + "%",))
    # 按前缀清开放身份（而不是只清本次 AK）：上一轮若中途失败会留下凭证行，
    # 而脚本末尾的「开放身份残留=0」检查会因此永远不通过 / 或永远被忽略
    pg_exec("delete from sysopenaccess where accesskey like %s", ("stress_ak_%",))


def seed_account(type_, account_info, total_quota, status=ACCOUNT_ENABLED,
                 used=Decimal("0"), locked=Decimal("0")):
    """直插一条收款账号，返回测试 Id。"""
    _ID_SEQ[0] += 1
    pg_exec(
        "insert into pay_account (id, type, accountinfo, totalquota, usedquota, lockedquota, "
        "status, remark, createtime, isdelete) values (%s,%s,%s,%s,%s,%s,%s,%s, now(), false)",
        (_ID_SEQ[0], type_, account_info, Decimal(str(total_quota)),
         Decimal(str(used)), Decimal(str(locked)), status, "压测数据"))
    return _ID_SEQ[0]


def read_account(account_id):
    return pg_one("select * from pay_account where id = %s", (account_id,))


def read_accounts_by_type(type_):
    return pg_query("select * from pay_account where type = %s order by id", (type_,))


def read_orders(type_):
    return pg_query("select o.* from pay_order o join pay_account a on a.id = o.accountid "
                    "where a.type = %s order by o.id", (type_,))


def count_events(type_):
    return pg_scalar("select count(*) from pay_order_event e join pay_order o on o.id = e.orderid "
                     "join pay_account a on a.id = o.accountid where a.type = %s", (type_,))


# ─────────────────────────── 场景 ───────────────────────────

def scenario_1_concurrency():
    """10 个并发 × 30 元，账号额度 100 → 恰好 3 个成功（3×30=90 ≤ 100 < 120）"""
    section("场景 1：并发抢占同一账号（核心：额度不超发）")

    type_, quota, amount, n = f"{TAG}c1", Decimal("100.00"), Decimal("30.00"), 10
    account_id = seed_account(type_, "acct-c1-quota100", quota)

    with ThreadPoolExecutor(max_workers=n) as pool:
        results = [r for _, r in pool.map(lambda _: (None, allocate(float(amount), type_)), range(n))]

    good = [r for r in results if ok(r)]
    bad = [r for r in results if not ok(r)]
    print(f"    成功 {len(good)} / 共 {n}；失败样例：{[(r.get('code'), r.get('message')) for r in bad[:2]]}")

    expect = int(quota // amount)  # 3
    assert_true(len(good) <= expect, f"成功笔数 ≤ {expect}（额度 {quota} / 单笔 {amount}）", f"实际 {len(good)}")
    assert_true(len(good) == expect, f"成功笔数 == {expect}（额度刚好用满，无浪费）", f"实际 {len(good)}")

    order_nos = {r["result"]["orderNo"] for r in good}
    assert_true(len(order_nos) == len(good), "成功订单号互不重复", f"{len(order_nos)} 个")

    orders = read_orders(type_)
    assert_true(len(orders) == len(good), "数据库订单条数 == 成功响应条数", f"库中 {len(orders)} 条")
    assert_true(count_events(type_) == len(orders), "每笔订单都有 1 条「创建」事件流水",
                f"事件 {count_events(type_)} 条 / 订单 {len(orders)} 条")

    acc = read_account(account_id)
    locked, total = dec(acc["lockedquota"]), dec(acc["totalquota"])
    assert_true(locked == amount * len(good), "LockedQuota == 成功订单金额合计（无额度泄漏）",
                f"LockedQuota={locked}, 订单合计={amount * len(good)}")
    remaining = total - dec(acc["usedquota"]) - locked
    assert_true(remaining >= 0, "剩余可用额度 ≥ 0（无超发）", f"剩余={remaining}")
    assert_true(remaining == total - amount * expect, "剩余额度精确等于总额度 − 成功预占",
                f"剩余={remaining}，期望={total - amount * expect}")
    assert_true(acc["status"] == ACCOUNT_ENABLED, "剩余额度未归零时账号保持「启用」",
                f"status={acc['status']}（1=启用）")
    assert_true(not any((r.get("code") == -1) for r in results),
                "并发过程中无数据库锁异常 / 连接错误")

    # 剩余 10 < 30，后续请求应被拒绝且不产生新订单
    r_after = allocate(float(amount), type_)
    assert_true(not ok(r_after), "额度剩余不足时后续请求被拒绝", f"code={r_after.get('code')}")
    assert_true(len(read_orders(type_)) == len(good), "被拒绝的请求未产生订单",
                f"库中 {len(read_orders(type_))} 笔")
    assert_true(dec(read_account(account_id)["lockedquota"]) == locked, "被拒绝的请求未改动额度")
    return account_id


def scenario_2_idempotent():
    """同一 ExternalNo 重复请求 → 只生成 1 笔订单"""
    section("场景 2：幂等键（F2.6）")

    type_, ext = f"{TAG}c2", "BIZ-STRESS-IDEMPOTENT-0001"
    seed_account(type_, "acct-c2", Decimal("100.00"))

    r1 = allocate(30.00, type_, ext)
    r2 = allocate(30.00, type_, ext)
    if not assert_true(ok(r1) and ok(r2), "两次请求均成功",
                       f"{r1.get('message')} / {r2.get('message')}"):
        return

    assert_true(r1["result"]["orderNo"] == r2["result"]["orderNo"], "两次返回同一订单号",
                f"{r1['result']['orderNo']} vs {r2['result']['orderNo']}")
    assert_true(r2["result"]["idempotentHit"] is True, "第二次命中幂等键（idempotentHit=true）",
                f"idempotentHit={r2['result'].get('idempotentHit')}")

    orders = read_orders(type_)
    assert_true(len(orders) == 1, "库中只有 1 笔订单", f"实际 {len(orders)} 笔")
    acc = read_accounts_by_type(type_)[0]
    assert_true(dec(acc["lockedquota"]) == Decimal("30.00"), "额度只预占一次",
                f"LockedQuota={dec(acc['lockedquota'])}")

    # 2b 并发同单号
    section("场景 2b：并发提交同一 ExternalNo（唯一索引兜底）")
    type_b, ext_b = f"{TAG}c2b", "BIZ-STRESS-IDEMPOTENT-CONCURRENT"
    seed_account(type_b, "acct-c2b", Decimal("1000.00"))
    with ThreadPoolExecutor(max_workers=5) as pool:
        rs = [r for _, r in pool.map(lambda _: (None, allocate(20.00, type_b, ext_b)), range(5))]

    orders_b = read_orders(type_b)
    assert_true(len(orders_b) == 1, "并发同一 ExternalNo 只落库 1 笔订单", f"实际 {len(orders_b)} 笔")
    assert_true(len({r["result"]["orderNo"] for r in rs if ok(r)}) == 1,
                "所有成功响应指向同一订单号")
    acc_b = read_accounts_by_type(type_b)[0]
    assert_true(dec(acc_b["lockedquota"]) == Decimal("20.00"), "并发同单号只预占一次额度",
                f"LockedQuota={dec(acc_b['lockedquota'])}")


def scenario_3_no_candidate():
    """无可用账号 → 业务失败且不生成订单（F2.5）"""
    section("场景 3：无可用账号（F2.5）")

    type_ = f"{TAG}c3"
    account_id = seed_account(type_, "acct-c3", Decimal("50.00"))

    r1 = allocate(99999.00, type_)
    assert_true(not ok(r1), "请求金额超过所有账号额度 → 业务失败",
                f"code={r1.get('code')} msg={r1.get('message')}")
    assert_true("无可用收款账号" in str(r1.get("message", "")), "错误信息为「无可用收款账号」")

    r2 = allocate(99999.00, f"{TAG}not_exist_type")
    assert_true(not ok(r2), "不存在的收款类型 → 业务失败", f"msg={r2.get('message')}")

    r3 = allocate(0, type_)
    assert_true(not ok(r3), "金额为 0 → 参数校验失败", f"msg={r3.get('message')}")

    r4 = allocate(-5.00, type_)
    assert_true(not ok(r4), "金额为负 → 参数校验失败", f"msg={r4.get('message')}")

    assert_true(len(read_orders(type_)) == 0, "失败请求未生成任何订单")
    acc = read_account(account_id)
    assert_true(dec(acc["lockedquota"]) == Decimal("0.00"), "失败请求未产生任何额度占用",
                f"LockedQuota={dec(acc['lockedquota'])}")
    assert_true(acc["status"] == ACCOUNT_ENABLED, "账号状态未被误改", f"status={acc['status']}")


def scenario_4_best_fit():
    """最佳适配（F2.2）"""
    section("场景 4：最佳适配（F2.2）")

    type_ = f"{TAG}c4"
    # 故意先建大额度账号，验证排序依据是「剩余」而非创建顺序
    seed_account(type_, "acct-c4-large", Decimal("1000.00"))
    seed_account(type_, "acct-c4-small", Decimal("50.00"))
    seed_account(type_, "acct-c4-mid", Decimal("200.00"))

    r1 = allocate(40.00, type_)
    if assert_true(ok(r1), "匹配请求成功", str(r1.get("message"))):
        assert_true(r1["result"]["accountInfo"] == "acct-c4-small",
                    "命中剩余最接近的账号（50，而非 200 / 1000）",
                    f"实际命中 {r1['result']['accountInfo']}")

    type_b = f"{TAG}c4b"
    seed_account(type_b, "acct-c4b-first", Decimal("100.00"))
    seed_account(type_b, "acct-c4b-second", Decimal("100.00"))
    r2 = allocate(30.00, type_b)
    if assert_true(ok(r2), "剩余相同时的匹配请求成功", str(r2.get("message"))):
        assert_true(r2["result"]["accountInfo"] == "acct-c4b-first",
                    "剩余相同时按创建时间升序（规则确定可复现）",
                    f"实际命中 {r2['result']['accountInfo']}")

    # 停用账号不得参与匹配
    type_c = f"{TAG}c4c"
    seed_account(type_c, "acct-c4c-disabled", Decimal("9999.00"), status=ACCOUNT_DISABLED)
    r3 = allocate(10.00, type_c)
    assert_true(not ok(r3), "停用账号不参与匹配", f"code={r3.get('code')}")

    # 已用完账号不得参与匹配
    type_d = f"{TAG}c4d"
    seed_account(type_d, "acct-c4d-exhausted", Decimal("100.00"), status=ACCOUNT_EXHAUSTED)
    r4 = allocate(10.00, type_d)
    assert_true(not ok(r4), "「已用完」账号不参与匹配", f"code={r4.get('code')}")

    # 剩余不足的账号不得参与匹配
    type_e = f"{TAG}c4e"
    seed_account(type_e, "acct-c4e-tight", Decimal("100.00"), used=Decimal("95.00"))
    r5 = allocate(10.00, type_e)
    assert_true(not ok(r5), "剩余额度不足的账号不参与匹配（剩余 5 < 请求 10）",
                f"code={r5.get('code')}")


def scenario_5_order_status():
    """订单状态查询（§7.3）"""
    section("场景 5：订单状态查询（§7.3）")

    type_ = f"{TAG}c5"
    seed_account(type_, "acct-c5", Decimal("500.00"))
    r1 = allocate(88.88, type_, "BIZ-STRESS-STATUS-0001")
    if not assert_true(ok(r1), "匹配请求成功", str(r1.get("message"))):
        return
    order_no = r1["result"]["orderNo"]

    r2 = order_status(order_no)
    if assert_true(ok(r2), "查询订单状态成功", str(r2.get("message"))):
        res = r2["result"]
        assert_true(res["orderNo"] == order_no, "订单号一致")
        assert_true(res["status"] == ORDER_PENDING, "初始状态为「待到账」", f"status={res['status']}")
        assert_true(dec(res["requestAmount"]) == Decimal("88.88"), "请求金额一致")
        assert_true(dec(res["receivedAmount"]) == Decimal("0"), "累计到账为 0")
        assert_true(res["completeTime"] is None, "完成时间为空")
        assert_true(res["statusText"] == "待到账", "状态中文描述正确", f"statusText={res['statusText']}")

    r3 = order_status("NOT-EXIST-ORDER")
    assert_true(not ok(r3), "不存在的订单号 → 业务失败", f"code={r3.get('code')}")


def scenario_6_order_no_and_expire():
    """订单号格式（自包含雪花）与过期时间（F3.1）"""
    section("场景 6：订单号格式与过期时间")

    type_ = f"{TAG}c6"
    seed_account(type_, "acct-c6", Decimal("1000.00"))
    r = allocate(1.00, type_)
    if not assert_true(ok(r), "匹配请求成功", str(r.get("message"))):
        return

    no = r["result"]["orderNo"]
    # 格式契约（F2.3 方案 B）：{yyyyMMddHHmmssfff}{雪花尾段}，纯数字、日期前置、无业务前缀
    today = datetime.datetime.now().strftime("%Y%m%d")
    assert_true(no.isdigit() and no.startswith(today) and len(no) >= 32,
                "订单号符合 {yyyyMMddHHmmssfff}{雪花} 契约（纯数字 / 日期前置 / ≥32 位）",
                f"orderNo={no}（长度 {len(no)}）")
    head = datetime.datetime.strptime(no[:17], "%Y%m%d%H%M%S%f")
    assert_true(abs((datetime.datetime.now() - head).total_seconds()) < 120,
                "前 17 位就是生成时刻（与当前时间偏差 < 120s）",
                f"前 17 位={no[:17]}")

    # 注意：自包含雪花**不再保证连续递增**（序列位只在同一毫秒内递增），
    # 这里改为验证「单调不减」，而不是旧脚本的 n2 == n1 + 1。
    r2 = allocate(1.00, type_)
    if ok(r2):
        assert_true(r2["result"]["orderNo"] > no, "订单号按字符串序单调递增（时间前置的直接收益）",
                    f"{no} → {r2['result']['orderNo']}")

    # 过期时间 = 创建时间 + 配置时长（默认 30 分钟）
    orders = read_orders(type_)
    if orders:
        diff = pg_scalar(
            "select round(extract(epoch from (expiretime - createtime)) / 60, 1) "
            "from pay_order where id = %s", (orders[0]["id"],))
        assert_true(abs(float(diff) - 30.0) < 1.0,
                    "过期时间 = 创建时间 + 配置的 30 分钟（pay_order_expire_minutes）",
                    f"实际间隔 {diff} 分钟")

    assert_true(r["result"]["accountInfo"] == "acct-c6",
                "返回完整账号信息（按设计决策不脱敏）",
                f"accountInfo={r['result']['accountInfo']}")
    assert_true(r["result"]["idempotentHit"] is False, "未传 ExternalNo 时不命中幂等键")


def scenario_7_exhaust_and_restore():
    """额度打满 → 自动置「已用完」；追加额度后恢复参与匹配（F1.4）"""
    section("场景 7：额度打满与恢复（F1.4）")

    type_ = f"{TAG}c7"
    quota, amount = Decimal("100.00"), Decimal("25.00")
    account_id = seed_account(type_, "acct-c7", quota)

    with ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(lambda _: allocate(float(amount), type_), range(8)))

    orders = read_orders(type_)
    acc = read_account(account_id)
    assert_true(len(orders) == 4, "8 个并发 × 25 元 / 额度 100 元 → 恰好 4 笔成功",
                f"实际 {len(orders)} 笔")
    assert_true(dec(acc["lockedquota"]) == amount * len(orders),
                "LockedQuota 与订单数严格一致（并发下无泄漏）",
                f"LockedQuota={dec(acc['lockedquota'])}, 订单 {len(orders)} 笔")
    assert_true(dec(acc["totalquota"]) - dec(acc["usedquota"]) - dec(acc["lockedquota"]) == 0,
                "剩余可用额度精确归零")
    assert_true(acc["status"] == ACCOUNT_EXHAUSTED,
                "额度归零后状态自动置为「已用完」（F1.4）",
                f"status={acc['status']}（3=已用完）")

    r_full = allocate(float(amount), type_)
    assert_true(not ok(r_full), "「已用完」账号不再参与匹配", f"code={r_full.get('code')}")

    # ★ 走**真实后台 API**（S6 交付的 payAccount/addQuota），不再直改库——
    #   这样才真正验证「追加额度 → 状态回置 → 重新参与匹配」这条后台链路。
    st, res, _ = admin_call("POST", "/api/payAccount/addQuota",
                            {"id": account_id, "quota": 200}, token=TOKEN)
    assert_true(st == 200 and ok(res), "后台「追加额度」接口调用成功",
                f"http={st} msg={res.get('message')}")

    acc_after = read_account(account_id)
    assert_true(acc_after["status"] == ACCOUNT_ENABLED,
                "追加额度后状态自动回置为「启用」（F1.4）",
                f"status={acc_after['status']}（1=启用）")
    assert_true(dec(acc_after["totalquota"]) == quota + Decimal("200"),
                "总额度已增加 200", f"totalquota={dec(acc_after['totalquota'])}")

    r_restore = allocate(float(amount), type_)
    assert_true(ok(r_restore), "追加额度后立即恢复参与匹配（额度未被占死）",
                f"code={r_restore.get('code')} msg={r_restore.get('message')}")

    acc2 = read_account(account_id)
    expect_remaining = (quota + Decimal("200")) - amount * (len(orders) + 1)
    actual_remaining = dec(acc2["totalquota"]) - dec(acc2["usedquota"]) - dec(acc2["lockedquota"])
    assert_true(actual_remaining == expect_remaining,
                "追加后剩余额度账实相符（总额 − 已用 − 锁定）",
                f"剩余={actual_remaining}，期望={expect_remaining}")

    # 追加额度这条后台动作应留下审计（F7.3）
    audit = pg_scalar("select count(*) from pay_audit_log where targetno = %s or targetid = %s",
                      (type_, str(account_id)))
    assert_true(audit and audit >= 1, "「追加额度」写入了业务审计 pay_audit_log", f"记录数={audit}")


# ─────────────────────────── 主流程 ───────────────────────────

def setup_open_access():
    """建一个 scopes=allocate 的开放身份（跑完删掉）。"""
    global TOKEN
    TOKEN = admin_login()
    st, res, _ = admin_call("POST", "/api/sysOpenAccess/add", {
        "accessKey": AK, "accessSecret": SK,
        "bindUserId": 1300000000101, "bindTenantId": 1300000000001,
        "scopes": "allocate",
    }, token=TOKEN)
    return st == 200 and ok(res), f"http={st} msg={res.get('message')}"


def teardown_open_access():
    if not TOKEN:
        return
    row = pg_one("select id from sysopenaccess where accesskey = %s", (AK,))
    if row:
        admin_call("POST", "/api/sysOpenAccess/delete", {"id": row["id"]}, token=TOKEN)


def main():
    print(f"目标服务：{BASE}")
    print(f"数据库：  {PG_HOST}:{PG_PORT}/{PG_DB}")

    if not wait_backend(timeout=60):
        print(f"❌ 服务不可达：{BASE}")
        return 1
    print("服务已就绪")

    reset()
    print("已清理历史压测数据")

    section("准备：登录 + 建开放身份（scopes=allocate）")
    good, detail = setup_open_access()
    if not assert_true(good, "建立 scopes=allocate 的开放身份", detail):
        print("❌ 无法取得开放身份，终止")
        return 1

    # 签名可用性预检（顺带验证 scope 门禁确实放行 allocate）
    probe = allocate(0.01, "probe_only_type")
    if probe.get("code") == -1:
        print(f"❌ 签名调用不可达：{probe.get('message')}")
        return 1
    assert_true(probe.get("code") != 401 and "签名" not in str(probe.get("message")),
                "签名鉴权通过（不是 401）", f"msg={probe.get('message')}")

    try:
        scenario_1_concurrency()
        scenario_2_idempotent()
        scenario_3_no_candidate()
        scenario_4_best_fit()
        scenario_5_order_status()
        scenario_6_order_no_and_expire()
        scenario_7_exhaust_and_restore()
    finally:
        section("清理")
        reset()
        teardown_open_access()
        left = pg_scalar("select count(*) from pay_account where type like %s", (TAG + "%",))
        left_oa = pg_scalar("select count(*) from sysopenaccess where accesskey = %s", (AK,))
        assert_true(left == 0 and left_oa == 0, "压测数据与开放身份已清干净",
                    f"账号残留={left} 开放身份残留={left_oa}")

        # ★ 审计表也要查。本脚本每次 allocate 都写一行 action=8「开放接口调用」，
        #   其 targetno 是**纯数字订单号**（自包含雪花，无业务前缀），
        #   所以 `targetno like 'stress_%'` 永远抓不到 —— 只能靠 clientkey。
        #   原来只看账号和开放身份，于是审计孤儿每轮静默累积却一路 PASS（实测留下过十几行）。
        left_audit = pg_scalar("select count(*) from pay_audit_log")
        assert_true(left_audit == 0, "审计表无残留",
                    f"审计残留={left_audit}"
                    f"（非 0 可能是本脚本残留，也可能是别的进程/人工动了这套系统）")

    return report()


if __name__ == "__main__":
    sys.exit(main())
