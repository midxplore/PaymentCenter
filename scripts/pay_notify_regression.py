#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""收款账号分配系统 —— notify（F4 到账通知 / 分次到账 / 去重）回归脚本

设计取舍（与 pay_allocate_stress.py 一致）：
  * 到账通知走**真实 HTTP** —— 只有 HTTP 才有独立请求上下文（SqlSugarScope 的连接按请求上下文隔离）。
  * 开放接口走**签名鉴权**（S5 起），脚本自己建 `scopes=notify` 的开放身份，跑完删掉。
  * 账号与订单的造数、结果核对直连 **PostgreSQL** —— 便于精确构造
    「部分到账 / 已过期 / 已完成」等状态，且不依赖登录态。

核心不变式（贯穿所有场景核对）：
    UsedQuota   == Σ(已终结订单(已完成/已过期)的累计到账金额)
    LockedQuota == Σ(未终结订单(待到账/部分到账)的请求金额)
    剩余可用额度 == TotalQuota − UsedQuota − LockedQuota ≥ 0

用法（需要 PG 在跑、后端已启动）：

    ~/.workbuddy-ai/binaries/python/envs/default/bin/python scripts/pay_notify_regression.py

退出码：0 = 全部通过；1 = 存在失败断言
"""

import os
import sys
import time
import uuid
from concurrent.futures import ThreadPoolExecutor
from decimal import Decimal

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _pay_common import (  # noqa: E402
    BASE, PG_HOST, PG_PORT, PG_DB, PASS, FAIL,
    assert_true, section, report, call, sign_call, admin_call, admin_login, ok, dec,
    pg_query, pg_one, pg_scalar, pg_exec, wait_backend,
)

# 枚举值（与 C# 侧保持一致）
ACCOUNT_ENABLED, ACCOUNT_DISABLED, ACCOUNT_EXHAUSTED = 1, 2, 3
ORDER_PENDING, ORDER_PARTIAL, ORDER_COMPLETED, ORDER_EXPIRED = 1, 2, 3, 4
EV_CREATED, EV_PARTIAL, EV_COMPLETED, EV_EXPIRED, EV_MANUAL = 1, 2, 3, 4, 5
REASON_NOT_FOUND, REASON_EXPIRED, REASON_COMPLETED = 1, 2, 3
HANDLE_PENDING, HANDLE_LINKED, HANDLE_IGNORED = 1, 2, 3

TAG = "nt_"

# 造数 Id 走远离雪花区间的高位段（见 pay_allocate_stress.py 的说明）
_ID = [7_100_000_000_000_000]

AK = "nt_ak_" + time.strftime("%m%d%H%M%S")
SK = "nt_sk_secret_" + time.strftime("%m%d%H%M%S")

# 「库里不存在的 accessKey」：用于验「未知密钥被拒」。
# ★ 提成常量而不是在用例里写字符串字面量，是为了让**清理**能引用同一个值 ——
#   这个 accessKey 会被框架的认证失败审计（action=9）记一行，clientkey 就是它本身。
#   写成两处字面量时，改了用例忘了改清理，就会每轮留下一行审计孤儿（实测踩过）。
MISSING_AK = "no_such_key"

TOKEN = None


# ─────────────────────────── 开放接口 ───────────────────────────

def notify(order_no, amount, voucher_no=None, notify_time=None):
    body = {"orderNo": order_no, "amount": amount,
            "voucherNo": voucher_no or f"V-{uuid.uuid4().hex[:16]}"}
    if notify_time:
        body["notifyTime"] = notify_time
    return sign_call("POST", "/api/pay/notify", body, ak=AK, sk=SK)[1]


def voucher(tag):
    return f"V-{TAG}{tag}-{uuid.uuid4().hex[:8]}"


# ─────────────────────────── 造数与核对（直连 PG） ───────────────────────────

def reset():
    """清空本脚本产生的测试数据（幂等）。"""
    # ★ 审计行必须**最先**删：下面两条子查询依赖 pay_order / pay_account 还在。
    #
    # ★ 本脚本造的订单号是**带 TAG 前缀**的（`nt_S1-ORDER`），所以 `targetno like 'nt_%'`
    #   在这里**恰好**能匹配。但那是巧合：产品里的订单号是**自包含雪花**（纯数字、无前缀），
    #   一旦以后改成「用真接口造单」，这条清理就会静默失效、留下一堆审计孤儿。
    #   所以这里同时按 clientkey（= accessKey，前缀挂在那里）抓，不依赖订单号的形态。
    pg_exec(
        "delete from pay_audit_log"
        " where clientkey like %s"
        "    or clientkey = %s"
        "    or targetno like %s"
        "    or remark like %s"
        "    or (targettype = 'PayAccount' and targetid in"
        "        (select id from pay_account where type like %s))"
        "    or (targettype = 'Order' and targetid in"
        "        (select id from pay_order where orderno like %s))",
        ("nt_ak_%", MISSING_AK, TAG + "%", "%" + TAG + "%", TAG + "%", TAG + "%"))
    pg_exec("delete from pay_order_event where orderno like %s", (TAG + "%",))
    pg_exec("delete from pay_notify_record where voucherno like %s", ("V-" + TAG + "%",))
    pg_exec("delete from pay_abnormal_receipt where voucherno like %s", ("V-" + TAG + "%",))
    pg_exec("delete from pay_order where orderno like %s", (TAG + "%",))
    pg_exec("delete from pay_account where type like %s", (TAG + "%",))
    # 按前缀清开放身份（而不是只清本次 AK）：中途失败会留下凭证行
    pg_exec("delete from sysopenaccess where accesskey like %s", ("nt_ak_%",))


def _next_id():
    _ID[0] += 1
    return _ID[0]


def seed_account(type_, info, total, status=ACCOUNT_ENABLED, used=0, locked=0):
    aid = _next_id()
    pg_exec(
        "insert into pay_account (id, type, accountinfo, totalquota, usedquota, lockedquota, "
        "status, remark, createtime, isdelete) values (%s,%s,%s,%s,%s,%s,%s,%s, now(), false)",
        (aid, type_, info, Decimal(str(total)), Decimal(str(used)), Decimal(str(locked)),
         status, "回归数据"))
    return aid


def seed_order(order_no, account_id, request_amount, status=ORDER_PENDING,
               received=0, expire_offset_minutes=30):
    """expire_offset_minutes 为负表示已过期。"""
    oid = _next_id()
    pg_exec(
        "insert into pay_order (id, orderno, externalno, requestamount, accountid, receivedamount, "
        "status, expiretime, createtime, isdelete) values "
        "(%s,%s,null,%s,%s,%s,%s, now() + (%s || ' minutes')::interval, now(), false)",
        (oid, order_no, Decimal(str(request_amount)), account_id, Decimal(str(received)),
         status, str(expire_offset_minutes)))
    return oid


def read_order(order_no):
    return pg_one("select * from pay_order where orderno = %s", (order_no,))


def read_account(aid):
    return pg_one("select * from pay_account where id = %s", (aid,))


def read_events(order_id):
    return pg_query("select * from pay_order_event where orderid = %s order by id", (order_id,))


def read_notify_record(vno):
    return pg_one("select * from pay_notify_record where voucherno = %s", (vno,))


def read_abnormal(vno):
    return pg_one("select * from pay_abnormal_receipt where voucherno = %s", (vno,))


def check_invariants(label):
    """全局不变式：UsedQuota / LockedQuota 与订单状态必须自洽

    额度语义（设计文档 §4.1 + §5.2/§5.3）：
      已用额度 = Σ(已终结订单(已完成/已过期)的累计到账金额)   ← 钱确实到了就计入已用
      锁定额度 = Σ(未终结订单(待到账/部分到账)的请求金额)      ← 预占按请求金额
    """
    rows = pg_query(
        "select a.id, a.totalquota, a.usedquota, a.lockedquota, "
        "  coalesce((select sum(o.receivedamount) from pay_order o where o.accountid = a.id "
        "            and o.status in (%s,%s)), 0) as settled_recv, "
        "  coalesce((select sum(o.requestamount) from pay_order o where o.accountid = a.id "
        "            and o.status in (%s,%s)), 0) as active_req "
        "from pay_account a where a.type like %s",
        (ORDER_COMPLETED, ORDER_EXPIRED, ORDER_PENDING, ORDER_PARTIAL, TAG + "%"))
    for r in rows:
        used, locked = dec(r["usedquota"]), dec(r["lockedquota"])
        exp_used, exp_locked = dec(r["settled_recv"]), dec(r["active_req"])
        total = dec(r["totalquota"])
        assert_true(used == exp_used,
                    f"[{label}] UsedQuota == Σ已终结订单累计到账",
                    f"账号{r['id']}: Used={used} 期望={exp_used}")
        assert_true(locked == exp_locked,
                    f"[{label}] LockedQuota == Σ未终结订单请求金额",
                    f"账号{r['id']}: Locked={locked} 期望={exp_locked}")
        assert_true(total - used - locked >= 0,
                    f"[{label}] 剩余可用额度 ≥ 0",
                    f"账号{r['id']}: 剩余={total - used - locked}")


# ─────────────────────────── 场景 ───────────────────────────

def scenario_1_partial_then_complete():
    """分 3 次到账，累计达标后完成 —— 重点验证 UsedQuota 按累计到账结转"""
    section("场景 1：分次到账 → 累计达标完成（F4.2/F4.3）★")

    t = f"{TAG}s1"
    aid = seed_account(t, "acct-s1", Decimal("1000.00"), used=0, locked=100)
    order_no = f"{TAG}S1-ORDER"
    oid = seed_order(order_no, aid, Decimal("100.00"), ORDER_PENDING, received=0)

    # 第 1 笔 30
    r1 = notify(order_no, 30.00, voucher("S1-A"))
    if assert_true(ok(r1) and r1["result"]["result"] == "accepted", "第 1 笔 30 元受理",
                   str(r1.get("message")) or r1.get("result", {}).get("resultText")):
        assert_true(r1["result"]["orderStatus"] == "部分到账", "状态 → 部分到账",
                    f"orderStatus={r1['result']['orderStatus']}")
        assert_true(dec(r1["result"]["receivedAmount"]) == Decimal("30.00"), "累计到账 30.00")

    o = read_order(order_no)
    a = read_account(aid)
    assert_true(o["status"] == ORDER_PARTIAL, "订单落库状态 = 部分到账", f"status={o['status']}")
    assert_true(dec(a["usedquota"]) == Decimal("0.00"), "部分到账不动 UsedQuota", f"Used={dec(a['usedquota'])}")
    assert_true(dec(a["lockedquota"]) == Decimal("100.00"), "部分到账继续锁定全额", f"Locked={dec(a['lockedquota'])}")

    # 第 2 笔 40
    r2 = notify(order_no, 40.00, voucher("S1-B"))
    assert_true(ok(r2) and r2["result"]["orderStatus"] == "部分到账", "第 2 笔 40 元 → 仍为部分到账")
    o = read_order(order_no)
    assert_true(dec(o["receivedamount"]) == Decimal("70.00"), "累计到账 70.00", f"received={dec(o['receivedamount'])}")

    # 第 3 笔 30 → 达标
    r3 = notify(order_no, 30.00, voucher("S1-C"))
    if assert_true(ok(r3) and r3["result"]["result"] == "accepted", "第 3 笔 30 元受理"):
        assert_true(r3["result"]["orderStatus"] == "已完成", "状态 → 已完成",
                    f"orderStatus={r3['result']['orderStatus']}")
        assert_true(dec(r3["result"]["receivedAmount"]) == Decimal("100.00"), "累计到账 100.00")

    o = read_order(order_no)
    a = read_account(aid)
    assert_true(o["status"] == ORDER_COMPLETED, "订单落库状态 = 已完成", f"status={o['status']}")
    assert_true(o["completetime"] is not None, "完成时间已写入")

    # ★ 关键：UsedQuota 必须是「累计到账」100，而不是最后一笔的 30
    assert_true(dec(a["usedquota"]) == Decimal("100.00"),
                "★ UsedQuota == 累计到账 100.00（而非最后一笔的 30.00）",
                f"Used={dec(a['usedquota'])}")
    assert_true(dec(a["lockedquota"]) == Decimal("0.00"), "LockedQuota 已全额释放",
                f"Locked={dec(a['lockedquota'])}")
    assert_true(dec(a["totalquota"]) - dec(a["usedquota"]) - dec(a["lockedquota"]) == Decimal("900.00"),
                "剩余可用额度 = 1000 − 100 = 900")

    evs = read_events(oid)
    types = [e["eventtype"] for e in evs]
    assert_true(types == [EV_PARTIAL, EV_PARTIAL, EV_COMPLETED],
                "事件流水依次为 部分到账/部分到账/完成", f"实际 {types}")
    assert_true([dec(e["receivedtotal"]) for e in evs] == [Decimal("30.00"), Decimal("70.00"), Decimal("100.00")],
                "每条事件的累计到账金额依次为 30 / 70 / 100",
                f"{[str(dec(e['receivedtotal'])) for e in evs]}")
    assert_true(all(e["eventtype"] != EV_CREATED for e in evs), "到账流程不产生「订单创建」事件")
    check_invariants("场景1")
    return aid, order_no


def scenario_2_duplicate_voucher():
    """凭证号去重（F4.5）—— 串行重复"""
    section("场景 2：凭证号重复（F4.5）")

    t = f"{TAG}s2"
    aid = seed_account(t, "acct-s2", Decimal("1000.00"), used=0, locked=100)
    order_no = f"{TAG}S2-ORDER"
    seed_order(order_no, aid, Decimal("100.00"), ORDER_PENDING, received=0)

    v = voucher("S2")
    r1 = notify(order_no, 50.00, v)
    r2 = notify(order_no, 50.00, v)
    r3 = notify(order_no, 50.00, v)

    assert_true(ok(r1) and r1["result"]["result"] == "accepted", "首次通知受理")
    assert_true(ok(r2) and r2["result"]["result"] == "duplicated", "第 2 次 → duplicated",
                f"result={r2.get('result', {}).get('result')}")
    assert_true(ok(r3) and r3["result"]["result"] == "duplicated", "第 3 次 → duplicated")

    o = read_order(order_no)
    assert_true(dec(o["receivedamount"]) == Decimal("50.00"),
                "重复通知未重复累加（仍为 50.00）", f"received={dec(o['receivedamount'])}")
    assert_true(len(read_events(o["id"])) == 1, "只产生 1 条事件流水")

    rec = read_notify_record(v)
    assert_true(rec is not None and rec["applied"] is True, "通知记录 Applied=true")
    assert_true(rec is not None and rec["orderid"] == o["id"], "通知记录已回填 OrderId")

    # duplicated 也必须返回订单当前状态，避免通知方无脑重试
    assert_true(r2["result"]["receivedAmount"] is not None and r2["result"]["orderStatus"] == "部分到账",
                "duplicated 响应携带订单当前状态",
                f"orderStatus={r2['result'].get('orderStatus')}")
    check_invariants("场景2")


def scenario_3_concurrent_same_voucher():
    """并发同凭证号 → 只有一笔被累加"""
    section("场景 3：并发同凭证号（去重抗并发）")

    t = f"{TAG}s3"
    aid = seed_account(t, "acct-s3", Decimal("1000.00"), used=0, locked=100)
    order_no = f"{TAG}S3-ORDER"
    seed_order(order_no, aid, Decimal("100.00"), ORDER_PENDING, received=0)

    v = voucher("S3")
    with ThreadPoolExecutor(max_workers=8) as pool:
        rs = [r for _, r in pool.map(lambda _: (None, notify(order_no, 20.00, v)), range(8))]

    accepted = [r for r in rs if r.get("result", {}).get("result") == "accepted"]
    duplicated = [r for r in rs if r.get("result", {}).get("result") == "duplicated"]
    assert_true(len(accepted) == 1, "8 并发同凭证号 → 恰好 1 笔 accepted",
                f"accepted={len(accepted)}, duplicated={len(duplicated)}")
    assert_true(len(accepted) + len(duplicated) == 8, "其余全部为 duplicated")

    o = read_order(order_no)
    assert_true(dec(o["receivedamount"]) == Decimal("20.00"),
                "累计到账只加了一次 20.00", f"received={dec(o['receivedamount'])}")
    assert_true(len(read_events(o["id"])) == 1, "只产生 1 条事件流水",
                f"{len(read_events(o['id']))} 条")
    check_invariants("场景3")


def scenario_4_overpay():
    """超额到账（F4.4）—— 按实际到账记账，差额写入 OverpayRemark"""
    section("场景 4：超额到账（F4.4）")

    t = f"{TAG}s4"
    aid = seed_account(t, "acct-s4", Decimal("1000.00"), used=0, locked=100)
    order_no = f"{TAG}S4-ORDER"
    oid = seed_order(order_no, aid, Decimal("100.00"), ORDER_PENDING, received=0)

    r = notify(order_no, 120.00, voucher("S4"))
    if assert_true(ok(r) and r["result"]["result"] == "accepted", "120 元到账受理"):
        assert_true(r["result"]["orderStatus"] == "已完成", "状态 → 已完成")
        assert_true(dec(r["result"]["receivedAmount"]) == Decimal("120.00"), "累计到账 120.00")

    o = read_order(order_no)
    a = read_account(aid)
    assert_true(dec(o["receivedamount"]) == Decimal("120.00"), "累计到账按实际入账 120.00")
    assert_true(o["overpayremark"] is not None and "20.00" in o["overpayremark"],
                "超额差额 20.00 写入 OverpayRemark", f"OverpayRemark={o['overpayremark']}")
    assert_true(dec(a["usedquota"]) == Decimal("120.00"),
                "★ UsedQuota 按实际到账 120.00（设计决策 #2：如实记账）",
                f"Used={dec(a['usedquota'])}")
    assert_true(dec(a["lockedquota"]) == Decimal("0.00"), "LockedQuota 按请求金额 100 释放",
                f"Locked={dec(a['lockedquota'])}")
    assert_true(dec(a["totalquota"]) - dec(a["usedquota"]) - dec(a["lockedquota"]) == Decimal("880.00"),
                "剩余 = 1000 − 120 = 880（超额部分真实占用额度）")
    check_invariants("场景4")


def scenario_5_abnormal_branches():
    """异常分支：订单不存在 / 已过期 / 已完成 → 入异常台账（F5.1）"""
    section("场景 5：异常到账入台账（F5.1）")

    # 5a 订单不存在
    v = voucher("S5A")
    r = notify(f"{TAG}NOT-EXIST-ORDER", 10.00, v)
    assert_true(ok(r) and r["result"]["result"] == "abnormal", "订单不存在 → abnormal",
                f"result={r.get('result', {}).get('result')}")
    ab = read_abnormal(v)
    assert_true(ab is not None, "已写入异常台账")
    if ab:
        assert_true(ab["reason"] == REASON_NOT_FOUND, "原因 = 无匹配订单", f"reason={ab['reason']}")
        assert_true(ab["handlestatus"] == HANDLE_PENDING, "处理状态 = 待处理")
        assert_true(dec(ab["amount"]) == Decimal("10.00"), "台账金额正确")

    # 5b 订单已过期（按 §5.3 结转后的自洽状态：已到账 40 计入已用，预占已释放）
    t = f"{TAG}s5"
    aid = seed_account(t, "acct-s5", Decimal("1000.00"), used=40, locked=0)
    order_no = f"{TAG}S5-EXPIRED"
    seed_order(order_no, aid, Decimal("100.00"), ORDER_EXPIRED, received=40, expire_offset_minutes=-60)
    v = voucher("S5B")
    r = notify(order_no, 10.00, v)
    assert_true(ok(r) and r["result"]["result"] == "abnormal", "订单已过期 → abnormal")
    ab = read_abnormal(v)
    assert_true(ab is not None and ab["reason"] == REASON_EXPIRED, "原因 = 订单已过期",
                f"reason={ab and ab['reason']}")
    a = read_account(aid)
    assert_true(dec(a["usedquota"]) == Decimal("40.00") and dec(a["lockedquota"]) == Decimal("0.00"),
                "异常到账不改动额度（Used 仍 40、Locked 仍 0）",
                f"Used={dec(a['usedquota'])}, Locked={dec(a['lockedquota'])}")

    # 5c 订单已完成（换一个凭证号再报一次）
    order_no = f"{TAG}S5-DONE"
    aid2 = seed_account(f"{TAG}s5c", "acct-s5c", Decimal("1000.00"), used=100, locked=0)
    seed_order(order_no, aid2, Decimal("100.00"), ORDER_COMPLETED, received=100)
    v = voucher("S5C")
    r = notify(order_no, 10.00, v)
    assert_true(ok(r) and r["result"]["result"] == "abnormal", "订单已完成后再报 → abnormal")
    ab = read_abnormal(v)
    assert_true(ab is not None and ab["reason"] == REASON_COMPLETED, "原因 = 订单已完成",
                f"reason={ab and ab['reason']}")
    o = read_order(order_no)
    assert_true(dec(o["receivedamount"]) == Decimal("100.00"),
                "已完成订单的累计到账未被改动", f"received={dec(o['receivedamount'])}")
    assert_true(len(read_events(o["id"])) == 0, "异常到账不产生订单事件流水")
    assert_true(dec(read_account(aid2)["usedquota"]) == Decimal("100.00"),
                "已完成订单对应账号的已用额度未被改动")

    # 5d 异常到账也落通知记录（Applied=false）
    rec = read_notify_record(v)
    assert_true(rec is not None and rec["applied"] is False, "异常通知也落通知记录且 Applied=false",
                f"Applied={rec and rec['applied']}")
    check_invariants("场景5")


def scenario_6_concurrent_distinct_vouchers():
    """并发多笔不同凭证到账同一订单 → 额度只释放一次、不为负"""
    section("场景 6：并发多笔到账同一订单（额度只释放一次）★")

    t = f"{TAG}s6"
    aid = seed_account(t, "acct-s6", Decimal("1000.00"), used=0, locked=100)
    order_no = f"{TAG}S6-ORDER"
    seed_order(order_no, aid, Decimal("100.00"), ORDER_PENDING, received=0)

    n = 6
    with ThreadPoolExecutor(max_workers=n) as pool:
        rs = [r for _, r in pool.map(lambda i: (None, notify(order_no, 30.00, voucher(f"S6-{i}"))), range(n))]

    results = [r.get("result", {}).get("result") for r in rs]
    accepted = results.count("accepted")
    abnormal = results.count("abnormal")
    print(f"    结果分布：accepted={accepted}, abnormal={abnormal}, "
          f"duplicated={results.count('duplicated')}, 异常={[r.get('message') for r in rs if not ok(r)]}")

    o = read_order(order_no)
    a = read_account(aid)
    assert_true(o["status"] == ORDER_COMPLETED, "订单最终为已完成", f"status={o['status']}")
    assert_true(dec(a["lockedquota"]) >= 0, "★ LockedQuota 未被重复释放成负数",
                f"Locked={dec(a['lockedquota'])}")
    assert_true(dec(a["lockedquota"]) == Decimal("0.00"),
                "★ LockedQuota 恰好释放一次（100 → 0）", f"Locked={dec(a['lockedquota'])}")
    assert_true(dec(a["usedquota"]) == dec(o["receivedamount"]),
                "★ UsedQuota == 该订单的累计到账（已用与到账一致）",
                f"Used={dec(a['usedquota'])}, received={dec(o['receivedamount'])}")
    assert_true(accepted >= 1, "至少有一笔被受理", f"accepted={accepted}")

    completed_events = [e for e in read_events(o["id"]) if e["eventtype"] == EV_COMPLETED]
    assert_true(len(completed_events) == 1,
                "★「完成」事件恰好 1 条（完成动作只发生一次）", f"{len(completed_events)} 条")
    assert_true(dec(a["totalquota"]) - dec(a["usedquota"]) - dec(a["lockedquota"]) >= 0,
                "剩余可用额度 ≥ 0")
    check_invariants("场景6")


def scenario_7_validation():
    """入参校验与边界"""
    section("场景 7：入参校验")

    t = f"{TAG}s7"
    aid = seed_account(t, "acct-s7", Decimal("1000.00"), used=0, locked=100)
    order_no = f"{TAG}S7-ORDER"
    seed_order(order_no, aid, Decimal("100.00"), ORDER_PENDING, received=0)

    r1 = notify(order_no, 0, voucher("S7-Z"))
    assert_true(not ok(r1), "金额为 0 → 参数校验失败", f"msg={str(r1.get('message'))[:60]}")

    r2 = notify(order_no, -10, voucher("S7-N"))
    assert_true(not ok(r2), "金额为负 → 参数校验失败", f"msg={str(r2.get('message'))[:60]}")

    r3 = sign_call("POST", "/api/pay/notify", {"orderNo": order_no, "amount": 10}, ak=AK, sk=SK)[1]
    assert_true(not ok(r3), "缺凭证号 → 参数校验失败", f"msg={str(r3.get('message'))[:60]}")

    r4 = sign_call("POST", "/api/pay/notify", {"amount": 10, "voucherNo": voucher("S7-O")}, ak=AK, sk=SK)[1]
    assert_true(not ok(r4), "缺订单号 → 参数校验失败", f"msg={str(r4.get('message'))[:60]}")

    o = read_order(order_no)
    a = read_account(aid)
    assert_true(dec(o["receivedamount"]) == Decimal("0.00"), "校验失败的请求未改动订单")
    assert_true(dec(a["usedquota"]) == Decimal("0.00") and dec(a["lockedquota"]) == Decimal("100.00"),
                "校验失败的请求未改动额度")
    assert_true(len(read_events(o["id"])) == 0, "校验失败不产生事件流水")

    # ★ 订单号长度校验必须容得下真实订单号（历史上的 [MaxLength(32)] 与 32 位订单号正好撞线）
    real_order_no = "2" * PayConst_OrderNoLength()
    r5 = sign_call("POST", "/api/pay/notify",
                   {"orderNo": real_order_no, "amount": 10, "voucherNo": voucher("S7-LEN")},
                   ak=AK, sk=SK)[1]
    assert_true("长度" not in str(r5.get("message", "")),
                f"订单号上限容得下 {PayConst_OrderNoLength()} 位（不会在参数校验被挡）",
                f"msg={str(r5.get('message'))[:60]}")

    # 不带 notifyTime 也应受理（服务端补当前时间）
    v_ok = voucher("S7-OK")
    r6 = notify(order_no, 10.00, v_ok)
    assert_true(ok(r6) and r6["result"]["result"] == "accepted", "省略 notifyTime 时服务端补当前时间")
    rec = read_notify_record(v_ok)
    assert_true(rec is not None and rec["notifytime"] is not None, "通知记录已落库并带时间",
                f"NotifyTime={rec and rec['notifytime']}")
    check_invariants("场景7")


def PayConst_OrderNoLength():
    """从后端源码里读 PayConst.OrderNoLength（避免在 Python 侧再抄一份）。"""
    path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                        "Admin.NET/Admin.NET.Application/PayCenter/Const/PayConst.cs")
    with open(path, encoding="utf-8") as f:
        import re
        m = re.search(r"OrderNoLength\s*=\s*(\d+)", f.read())
    return int(m.group(1)) if m else 64


def scenario_8_expire_release():
    """部分到账订单过期后：已到账计入已用，未达成部分释放（设计决策 #3）

    ★ 这里**不手工模拟**结转，而是走**真实的过期作业**（F3.2）：
      造一笔「已到期」的部分到账订单 → 等 PayOrderExpireJob（每分钟）把它扫掉 → 核对结转结果。
      这样验证的是生产路径，而不是我们在脚本里重写一遍的算法。
    """
    section("场景 8：部分到账订单过期的额度结转（决策 #3，走真实过期作业 F3.2）★")

    t = f"{TAG}s8"
    aid = seed_account(t, "acct-s8", Decimal("1000.00"), used=0, locked=100)
    order_no = f"{TAG}S8-ORDER"
    oid = seed_order(order_no, aid, Decimal("100.00"), ORDER_PENDING, received=0)

    # 先到 40 → 部分到账
    r = notify(order_no, 40.00, voucher("S8-A"))
    assert_true(ok(r) and r["result"]["orderStatus"] == "部分到账", "先到 40 → 部分到账")

    # 把该单的到期时间拨到过去（模拟时间流逝），其余字段不动
    pg_exec("update pay_order set expiretime = now() - interval '1 minute' where id = %s", (oid,))
    assert_true(read_order(order_no)["status"] == ORDER_PARTIAL,
                "拨表后订单仍是「部分到账」（还没被作业扫到）")

    # 等作业把它扫掉（每分钟一轮，给足余量）
    deadline = time.time() + 180
    status = None
    while time.time() < deadline:
        status = read_order(order_no)["status"]
        if status == ORDER_EXPIRED:
            break
        time.sleep(5)

    if not assert_true(status == ORDER_EXPIRED,
                       "过期作业在 180s 内把订单置为「已过期」",
                       f"status={status}（4=已过期）"):
        return

    a = read_account(aid)
    assert_true(dec(a["usedquota"]) == Decimal("40.00"),
                "已到账 40 计入 UsedQuota（钱确实到了）", f"Used={dec(a['usedquota'])}")
    assert_true(dec(a["lockedquota"]) == Decimal("0.00"), "未达成部分已释放，LockedQuota=0",
                f"Locked={dec(a['lockedquota'])}")
    assert_true(dec(a["totalquota"]) - dec(a["usedquota"]) - dec(a["lockedquota"]) == Decimal("960.00"),
                "净释放 = 请求 100 − 已到账 40 = 60，剩余 960")

    evs = read_events(oid)
    expired_events = [e for e in evs if e["eventtype"] == EV_EXPIRED]
    assert_true(len(expired_events) == 1, "产生 1 条「过期」事件流水", f"{len(expired_events)} 条")
    if expired_events:
        assert_true(expired_events[0]["fromstatus"] == ORDER_PARTIAL
                    and expired_events[0]["tostatus"] == ORDER_EXPIRED,
                    "事件记录了状态迁移 部分到账 → 已过期",
                    f"{expired_events[0]['fromstatus']} → {expired_events[0]['tostatus']}")

    # 过期后再来一笔到账 → 进异常台账
    v = voucher("S8-B")
    r2 = notify(order_no, 10.00, v)
    assert_true(ok(r2) and r2["result"]["result"] == "abnormal", "过期后到账 → abnormal")
    ab = read_abnormal(v)
    assert_true(ab is not None and ab["reason"] == REASON_EXPIRED, "异常原因 = 订单已过期")
    check_invariants("场景8")


def scenario_9_manual_link():
    """异常到账 → 人工关联到订单 → 正常累计（F5.2，§5.4）

    这是 §11 验收表里「人工关联异常到账到某订单 → 正常累计」那条的**真实 HTTP** 验证：
    走 `POST /api/payAbnormal/link`（后台 JWT），而不是直调服务或直改库。
    关键断言是「关联后的额度口径与到账通知完全一致」——因为 Link 内部复用的是同一套累加逻辑。
    """
    section("场景 9：人工关联异常到账（F5.2）★")

    t = f"{TAG}s9"
    aid = seed_account(t, "acct-s9", Decimal("1000.00"), used=0, locked=100)
    order_no = f"{TAG}S9-ORDER"
    oid = seed_order(order_no, aid, Decimal("100.00"), ORDER_PENDING, received=0)

    # 先报一笔「订单号不存在」的到账 → 进异常台账
    v = voucher("S9-A")
    r = notify(f"{TAG}S9-NOT-EXIST", 40.00, v)
    if not assert_true(ok(r) and r["result"]["result"] == "abnormal",
                       "订单号不存在 → 入异常台账", f"result={r.get('result', {}).get('result')}"):
        return
    ab = read_abnormal(v)
    if not assert_true(ab is not None and ab["handlestatus"] == HANDLE_PENDING,
                      "台账记录为「待处理」", f"handleStatus={ab and ab['handlestatus']}"):
        return

    # 后台人工关联到真实订单
    st, res, _ = admin_call("POST", "/api/payAbnormal/link",
                            {"id": ab["id"], "targetOrderNo": order_no, "handleRemark": "回归-人工关联"},
                            token=TOKEN)
    if not assert_true(st == 200 and ok(res), "人工关联接口调用成功",
                       f"http={st} msg={res.get('message')}"):
        return

    o = read_order(order_no)
    a = read_account(aid)
    assert_true(dec(o["receivedamount"]) == Decimal("40.00"),
                "★ 关联后订单累计到账 40.00（复用 §5.2 累加口径）",
                f"received={dec(o['receivedamount'])}")
    assert_true(o["status"] == ORDER_PARTIAL, "订单状态 → 部分到账", f"status={o['status']}")
    assert_true(dec(a["lockedquota"]) == Decimal("100.00"),
                "部分到账仍锁定全额 100", f"Locked={dec(a['lockedquota'])}")
    assert_true(dec(a["usedquota"]) == Decimal("0.00"),
                "未完成不动 UsedQuota", f"Used={dec(a['usedquota'])}")

    ab2 = read_abnormal(v)
    assert_true(ab2["handlestatus"] == HANDLE_LINKED, "台账状态 → 已关联",
                f"handleStatus={ab2 and ab2['handlestatus']}")
    assert_true(ab2["relatedorderid"] == oid, "台账已回填关联订单 Id")
    assert_true(ab2["handleremark"] is not None, "台账记录了处理备注")

    evs = read_events(oid)
    manual = [e for e in evs if e["eventtype"] == EV_MANUAL]
    assert_true(len(manual) == 1, "产生 1 条「人工关联」事件流水", f"{len(manual)} 条")
    if manual:
        assert_true(manual[0]["operatorname"] is not None, "事件记录了操作人",
                    f"operator={manual[0]['operatorname']}")

    # 防重复处置：同一笔台账再关联一次必须失败（§5.4.1 条件更新）
    st2, res2, _ = admin_call("POST", "/api/payAbnormal/link",
                              {"id": ab["id"], "targetOrderNo": order_no}, token=TOKEN)
    assert_true(not (st2 == 200 and ok(res2)), "同一笔台账重复关联被拒绝（防重复加钱）",
                f"code={res2.get('code')} msg={res2.get('message')}")
    assert_true(dec(read_order(order_no)["receivedamount"]) == Decimal("40.00"),
                "重复关联未重复累加", f"received={dec(read_order(order_no)['receivedamount'])}")

    # 关联动作应留业务审计（F7.3）
    audit = pg_scalar("select count(*) from pay_audit_log where targetno = %s or targetid = %s",
                      (order_no, str(ab["id"])))
    assert_true(audit and audit >= 1, "人工关联写入了业务审计 pay_audit_log", f"记录数={audit}")
    check_invariants("场景9")


# ─────────────────────────── 主流程 ───────────────────────────

def setup_open_access():
    """建一个 scopes=notify 的开放身份（跑完删掉）。"""
    global TOKEN
    TOKEN = admin_login()
    st, res, _ = admin_call("POST", "/api/sysOpenAccess/add", {
        "accessKey": AK, "accessSecret": SK,
        "bindUserId": 1300000000101, "bindTenantId": 1300000000001,
        "scopes": "notify",
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
    print("已清理历史回归数据")

    section("准备：登录 + 建开放身份（scopes=notify）")
    good, detail = setup_open_access()
    if not assert_true(good, "建立 scopes=notify 的开放身份", detail):
        print("❌ 无法取得开放身份，终止")
        return 1

    # scope 门禁反向验证：notify 身份访问 allocate 必须被拒（F6.2）
    cross = sign_call("POST", "/api/pay/allocate",
                      {"type": "nt_cross", "amount": 1}, ak=AK, sk=SK)[1]
    assert_true(cross.get("code") != 200,
                "scope 门禁：notify 身份访问 allocate 被拒（F6.2）",
                f"code={cross.get('code')} msg={cross.get('message')}")

    # 鉴权负向矩阵（§11「未授权调用方调用到账接口 → 鉴权失败」）
    anon = call("POST", "/api/pay/notify",
                {"orderNo": "x", "amount": 1, "voucherNo": "v"})[1]
    assert_true(anon.get("code") != 200 and "accessKey" in str(anon.get("message", "")),
                "匿名调用被拒（401 accessKey 不能为空）",
                f"code={anon.get('code')} msg={anon.get('message')}")

    bad = sign_call("POST", "/api/pay/notify",
                    {"orderNo": "x", "amount": 1, "voucherNo": "v"},
                    ak=AK, sk="wrong_secret")[1]
    assert_true(bad.get("code") != 200 and "签名" in str(bad.get("message", "")),
                "错误密钥签名被拒（401 sign 无效的签名）",
                f"code={bad.get('code')} msg={bad.get('message')}")

    unknown = sign_call("POST", "/api/pay/notify",
                        {"orderNo": "x", "amount": 1, "voucherNo": "v"},
                        ak=MISSING_AK, sk=SK)[1]
    assert_true(unknown.get("code") != 200 and "accessKey" in str(unknown.get("message", "")),
                "未知 accessKey 被拒（401 accessKey 无效）",
                f"code={unknown.get('code')} msg={unknown.get('message')}")

    try:
        scenario_1_partial_then_complete()
        scenario_2_duplicate_voucher()
        scenario_3_concurrent_same_voucher()
        scenario_4_overpay()
        scenario_5_abnormal_branches()
        scenario_6_concurrent_distinct_vouchers()
        scenario_7_validation()
        scenario_8_expire_release()
        scenario_9_manual_link()
    finally:
        section("清理")
        reset()
        teardown_open_access()
        left = pg_scalar("select count(*) from pay_account where type like %s", (TAG + "%",))
        left_oa = pg_scalar("select count(*) from sysopenaccess where accesskey = %s", (AK,))
        assert_true(left == 0 and left_oa == 0, "回归数据与开放身份已清干净",
                    f"账号残留={left} 开放身份残留={left_oa}")

        # ★ 审计表也要查。本脚本会写两类审计行：
        #   ① action=8「开放接口调用」（targettype=Order，targetno 是**带 TAG 前缀**的
        #      订单号 nt_S1-ORDER，所以能按前缀抓到）；
        #   ② action=9「开放接口认证失败」——其中「未知 accessKey 被拒」那条的
        #      clientkey 是 MISSING_AK（库中不存在的 key），既不匹配 TAG 也不匹配 AK 前缀，
        #      实测每轮留下 1 行孤儿，而上面的残留检查**只看账号和开放身份，发现不了**。
        left_audit = pg_scalar("select count(*) from pay_audit_log")
        assert_true(left_audit == 0, "审计表无残留（含认证失败行）",
                    f"审计残留={left_audit}"
                    f"（非 0 可能是本脚本残留，也可能是别的进程/人工动了这套系统）")

    return report()


if __name__ == "__main__":
    sys.exit(main())
