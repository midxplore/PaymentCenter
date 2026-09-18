#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""开放接口凭证「停用即失效」端到端探针（安全控制回归）。

## 这个脚本补的是哪块空白

其它回归脚本（`pay_allocate_stress` / `pay_notify_regression` / `s6_admin_probe`）
用的都是**已经启用**的凭证，所以它们只能证明「启用态能用」。
而「停用一把凭证之后，下一次调用**立刻**被拒」这条控制——也就是密钥泄漏时
运维唯一能按的那个按钮——此前只有静态证据（守卫脚本比对源码 + 单测比对掩码语义），
**没有任何一条链路真的把 accessKey 从启用切到停用、再观察接口行为**。

## 为什么必须走后台接口改状态，而不是直接 UPDATE 数据库

`SysOpenAccessService.GetByKey` 是**缓存旁路**（cache-aside）：

    _sysCacheService.GetOrAdd(CacheConst.KeyOpenAccess + accessKey, _ => 查库)

一旦某次请求把凭证读进缓存，**直接 UPDATE 数据库不会生效**——缓存里还是启用态，
接口照样放行。所以「停用后立刻失效」这条性质**天然包含两件事**：

  ① 状态闸门（`OnGetAccessSecret` 读到 Disable → 返回空密钥 → 签名校验失败）
  ② 更新时**驱逐缓存**（`UpdateOpenAccess` 里的 `_sysCacheService.Remove`）

只直接改库只能验到 ①，验不到 ②；而 ② 一旦漏了，症状是
「运维点了停用、接口还能用，直到缓存过期」——正是最需要自动化守住的那类回归。
本脚本全程走 `/api/sysOpenAccess/add` / `/update`，两条一起验。

## 为什么不用重启后端

新建的 accessKey 从未被缓存过，`GetOrAdd` 会回源查库，所以**不需要重启**就能
拿到「启用态」的初始状态。后续的状态切换由 `/update` 自己驱逐缓存。
（这也是这个脚本能挂在 CI 上的原因：它不依赖「重启来清缓存」这种脆弱前提。）

## 前置条件

租户图形验证码必须关闭（`systenant.captcha = f`），否则 `admin_login` 拿不到 token。
**必须改 `SysTenantSeedData.cs` 的种子再重启**——直接 UPDATE 数据库无效，
因为 `JwtHandler` 读的是 `SysCacheService` 里的租户缓存。
详见 `scripts/README`。

## 清理

脚本自己在 finally 里删掉造的凭证行与审计行，可重复运行。
审计行按 `targetno = accessKey` 清理——认证失败的行 `targetid` 记 0
（那时 accessKey 可能压根不在 `sysopenaccess` 里，拿不到凭证 Id），
所以**不能**按 targetid 关联，只能按 accessKey。

用法：
    python scripts/pay_openaccess_gate_probe.py
"""

import os
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from _pay_common import (  # noqa: E402
    admin_call,
    admin_login,
    assert_true,
    pg_exec,
    pg_one,
    report,
    section,
    sign_call,
)

# 造数标识：挂在 accessKey 上（accessKey 是 varchar(128)，塞得下前缀）
AK_PREFIX = "utgate_"

# 绑定到超管账号与默认租户，保证 Includes(BindUser) 拿得到用户
BIND_USER_ID = 1300000000101
BIND_TENANT_ID = 1300000000001

# /api/pay/status 需要 scope=allocate（见 PayAllocateService.GetStatus 的 [PayScope]）
SCOPE_ALLOCATE = "allocate"

# 随便给一个不存在的订单号：认证通过后应拿到「订单不存在」业务错误，
# 而不是认证失败——这正是「认证已通过」的判据。
PROBE_PATH = "/api/pay/status?orderNo=99999999999999999999999999"


def is_auth_failure(http_status, payload):
    """判定这次调用是否**卡在认证层**。

    Admin.NET 的鉴权失败由 `AdminNETResultProvider` 产出：HTTP 401，body 里 code=401。
    但为稳妥起见两边都认——只要任意一边是 401，就认为没通过认证。
    """
    if http_status == 401:
        return True
    return isinstance(payload, dict) and payload.get("code") == 401


def describe(http_status, payload):
    if isinstance(payload, dict):
        return f"HTTP {http_status} code={payload.get('code')} msg={str(payload.get('message'))[:120]}"
    return f"HTTP {http_status} body={str(payload)[:120]}"


def main():
    section("开放接口凭证「停用即失效」端到端验证")

    ak = AK_PREFIX + uuid.uuid4().hex[:12]
    # 真密钥的字符集是 Base64（`CreateSecret` 用 Convert.ToBase64String），
    # 不含 `*`；这里照同样的方式造，保证和「掩码标记」不冲突。
    sk = "utSk" + uuid.uuid4().hex

    credential_id = None
    token = None
    try:
        # ── 0. 登录后台 ────────────────────────────────────────────────
        try:
            token = admin_login()
            assert_true(True, "后台登录成功（图形验证码已关闭）")
        except Exception as ex:
            assert_true(False, "后台登录成功（图形验证码已关闭）", str(ex)[:200])
            return report("开放接口凭证停用闸门")

        # ── 1. 造一把「启用」凭证（走后台接口，与运维操作同一条路径）──
        st, res, _ = admin_call("POST", "/api/sysOpenAccess/add", {
            "accessKey": ak,
            "accessSecret": sk,
            "bindUserId": BIND_USER_ID,
            "bindTenantId": BIND_TENANT_ID,
            "scopes": SCOPE_ALLOCATE,
            "status": 1,
        }, token=token)
        added = isinstance(res, dict) and res.get("code") == 200
        assert_true(added, "后台新增开放接口凭证（启用态）", describe(st, res))

        row = pg_one("select id, status, scopes, accesssecret from sysopenaccess where accesskey = %s", (ak,))
        if not row:
            assert_true(False, "凭证已落库", f"库中查不到 accesskey={ak}")
            return report("开放接口凭证停用闸门")
        credential_id = row["id"]
        assert_true(row["status"] == 1, "落库 status = 1（启用）", f"实际 {row['status']}")
        assert_true(row["scopes"] == SCOPE_ALLOCATE, "落库 scopes = allocate", f"实际 {row['scopes']}")

        # ── 2. 启用态：应当**通过认证** ───────────────────────────────
        st, res, _ = sign_call("GET", PROBE_PATH, ak=ak, sk=sk)
        enabled_ok = not is_auth_failure(st, res)
        assert_true(enabled_ok, "启用态：签名认证通过（拿到业务错误而非 401）", describe(st, res))

        # ── 3. 后台停用（密钥字段留空 = 不修改密钥）────────────────────
        #    这一步同时验证「掩码/留空即不变」的语义：如果留空被当成新密钥写进去，
        #    下一步就会因为「密钥对不上」而 401 —— 那样这条用例会**假绿**。
        #    所以下面第 5 步要再改回启用并复查能否通过，用来把这两种原因区分开。
        st, res, _ = admin_call("POST", "/api/sysOpenAccess/update", {
            "id": credential_id,
            "accessKey": ak,
            "accessSecret": None,
            "bindUserId": BIND_USER_ID,
            "bindTenantId": BIND_TENANT_ID,
            "scopes": SCOPE_ALLOCATE,
            "status": 2,
        }, token=token)
        assert_true(isinstance(res, dict) and res.get("code") == 200,
                    "后台停用该凭证（status=2，密钥字段留空）", describe(st, res))

        row = pg_one("select status, accesssecret from sysopenaccess where id = %s", (credential_id,))
        assert_true(row and row["status"] == 2, "落库 status = 2（停用）", f"实际 {row and row['status']}")
        assert_true(row and row["accesssecret"] == sk,
                    "留空密钥未被改写（掩码/留空 = 不修改）", f"库中密钥与提交值{'一致' if row and row['accesssecret'] == sk else '不一致'}")

        # ── 4. ★ 核心断言：停用后**不重启**，下一次调用立刻被拒 ────────
        st, res, _ = sign_call("GET", PROBE_PATH, ak=ak, sk=sk)
        disabled_rejected = is_auth_failure(st, res)
        assert_true(disabled_rejected,
                    "★ 停用后立刻被拒（证明状态闸门 + 缓存驱逐同时生效，无需重启）",
                    describe(st, res))

        # 停用态对外与「accessKey 不存在」不可区分（不泄漏「这个 key 存在但被停用」）
        if isinstance(res, dict):
            assert_true("accessKey 无效" in str(res.get("message", "")),
                        "停用态对外报「accessKey 无效」，与「密钥不存在」不可区分",
                        str(res.get("message"))[:120])

        # ── 5. 审计留痕 ──────────────────────────────────────────────
        audit = pg_one(
            "select action, targetno, clientkey, operatorid, operatorname, remark"
            " from pay_audit_log where targetno = %s and action = 9 order by id desc limit 1",
            (ak,))
        if assert_true(audit is not None, "认证失败已写入 pay_audit_log（action=9）",
                       "无匹配行" if audit is None else ""):
            assert_true(audit["clientkey"] == ak, "审计行 clientkey = 该 accessKey", str(audit["clientkey"]))
            assert_true(audit["operatorid"] == 0, "审计行 operatorid = 0（无登录用户）", str(audit["operatorid"]))
            assert_true("开放接口" in str(audit["operatorname"]), "审计行 operatorname = 开放接口",
                        str(audit["operatorname"]))
            assert_true("GET" in str(audit["remark"]) and "/api/pay/status" in str(audit["remark"]),
                        "审计行 remark 记录了方法与路径", str(audit["remark"])[:120])

        # ── 6. 重新启用：应当**立刻**恢复可用 ────────────────────────
        #    这一步把「停用被拒」的原因钉死成「状态闸门」而不是「密钥被写坏」：
        #    如果密钥真被改坏了，重新启用后仍然会 401，本步就会失败。
        st, res, _ = admin_call("POST", "/api/sysOpenAccess/update", {
            "id": credential_id,
            "accessKey": ak,
            "accessSecret": None,
            "bindUserId": BIND_USER_ID,
            "bindTenantId": BIND_TENANT_ID,
            "scopes": SCOPE_ALLOCATE,
            "status": 1,
        }, token=token)
        assert_true(isinstance(res, dict) and res.get("code") == 200, "后台重新启用（status=1）", describe(st, res))

        st, res, _ = sign_call("GET", PROBE_PATH, ak=ak, sk=sk)
        assert_true(not is_auth_failure(st, res),
                    "重新启用后立刻恢复可用（反证「停用被拒」不是密钥被写坏）", describe(st, res))

        # ── 7. scope 失败也要留审计（fail-closed 的旁证）─────────────
        #    把 scopes 清空 → 该接口需要 allocate → 应被拒
        st, res, _ = admin_call("POST", "/api/sysOpenAccess/update", {
            "id": credential_id,
            "accessKey": ak,
            "accessSecret": None,
            "bindUserId": BIND_USER_ID,
            "bindTenantId": BIND_TENANT_ID,
            "scopes": "",
            "status": 1,
        }, token=token)
        assert_true(isinstance(res, dict) and res.get("code") == 200, "后台清空 scopes", describe(st, res))

        st, res, _ = sign_call("GET", PROBE_PATH, ak=ak, sk=sk)
        assert_true(is_auth_failure(st, res),
                    "★ 未配置 scope 时 fail-closed（拒绝而不是放行）", describe(st, res))
        if isinstance(res, dict):
            assert_true("权限范围" in str(res.get("message", "")),
                        "scope 拒绝的报错写明缺少哪个 scope", str(res.get("message"))[:140])

    finally:
        # ── 清理：凭证行 + 审计行（可重复运行）──────────────────────
        #   审计行的 targetid 是 0（认证失败时拿不到凭证 Id），所以只能按 accessKey 清；
        #   且与 pay_order 无关，不必考虑删订单的先后顺序。
        try:
            if credential_id:
                pg_exec("delete from sysopenaccess where id = %s", (credential_id,))
            pg_exec("delete from pay_audit_log where targetno = %s and targettype = 'OpenAccess'", (ak,))
            left = pg_one("select count(*) as n from sysopenaccess where accesskey = %s", (ak,))
            print(f"\n[清理] accessKey={ak} 残留凭证行 = {left['n'] if left else '?'}")
        except Exception as ex:
            print(f"\n[清理] 失败（请手工清理 accessKey={ak}）：{type(ex).__name__}: {ex}")

    return report("开放接口凭证停用闸门")


if __name__ == "__main__":
    sys.exit(main())
