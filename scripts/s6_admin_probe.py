#!/usr/bin/env python3
"""S6 后台管理与导出 —— 真实 HTTP 端到端验证（JWT 身份 + 签名开放接口）。

流程：造数 → 验证 → 清理，一条命令跑完。

  阶段 0  造数：管理员接口建收款账号 / 建开放身份 → 签名调用 allocate + notify
  阶段 1  登录（SM2 密文口令）→ 侧边栏菜单树
  阶段 2  订单查询：订单维度 + 账号维度（F7.2）
  阶段 3  订单详情：一次给全订单 + 事件流水 + 到账明细（F7.1）
  阶段 4  状态下拉
  阶段 5  4 类导出：真下载 xlsx（F7.5）
  阶段 6  导出留痕：pay_audit_log 里能查到刚写的「数据导出」（F7.3 / F7.5）
  阶段 7  异常台账分页
  阶段 8  清理

用法：
    python3 scripts/s6_admin_probe.py

两个前置条件（都跟框架配置有关，不是本模块的逻辑）：
  1. `Configuration/App.json` 的 `Cryptogram:CryptoType` 默认是 **SM2**，
     登录密码必须先用服务端公钥做 SM2 加密再发。脚本复用前端同款库
     （`Web/node_modules/sm-crypto-v2` 的 `sm2.doEncrypt(pwd, publicKey, 1)`，
     与登录页 `account.vue` 的调用完全一致），公钥直接从 `App.json` 读。
     ⚠️ 别想着用框架的 `/api/sysCommon/sM2Encrypt/{plainText}` —— 那个接口本身要登录，是死循环。
  2. 默认租户的 `captcha` 默认是 `true`，非交互脚本过不了图形验证码。要临时关掉，
     **必须改种子**（`Admin.NET.Application/SeedData/SysTenantSeedData.cs` 的 `Captcha=true` → `false`）
     再重启后端；直接 `update systenant set captcha=false` 是**没用的**——
     租户种子每次启动都会把该行覆盖回去。跑完记得改回 `true` 并重启。
"""
import base64
import datetime as _dt
import hashlib
import hmac
import json
import os
import re
import subprocess
import time
import urllib.error
import urllib.request
import uuid

BASE = "http://localhost:5005"
ACCOUNT = "superAdmin.NET"
PASSWORD = "Admin.NET++010101"
SUPER_ADMIN_USER_ID = 1300000000101
DEFAULT_TENANT_ID = 1300000000001

# 造数统一用这个时间戳前缀，清理时按它反查，避免误删别的数据
TAG = "S6E2E" + _dt.datetime.now().strftime("%m%d%H%M%S")
AK = "s6probe_" + TAG.lower()
SK = "s6probe_secret_" + TAG.lower()
# 本次运行开始时间。用途：清理**不带任何测试标识**的审计行（导出审计 action=7）——
# 那类行记的是「谁在什么时候导出了哪张表」，targetid=0、targetno 为空、clientkey 为空，
# 没有任何字段能被 TAG 匹配到，只能按时间窗清。
RUN_START = _dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NODE = os.environ.get("S6_NODE", "/Users/ipan/.workbuddy-ai/binaries/node/versions/22.22.2-2/bin/node")

# 本机跑着 HTTP_PROXY，直连 127.0.0.1 必须绕开，否则会被代理拦成 502
_opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
results = []


def call(method, path, body=None, token=None, raw=False, headers=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    try:
        with _opener.open(req) as r:
            payload = r.read()
            return r.status, (payload if raw else json.loads(payload.decode() or "{}")), dict(r.headers)
    except urllib.error.HTTPError as e:
        text = e.read().decode(errors="replace")
        # raw=True 时一律回原始字节：导出失败时后端返回的是 JSON 字符串（如 "该时间区间无数据可导出"），
        # 走 json.loads 会得到一个 str，后面按 bytes 判长度/魔数就会全判错。
        if raw:
            return e.code, text.encode(), dict(e.headers)
        try:
            return e.code, json.loads(text), dict(e.headers)
        except Exception:
            return e.code, text, dict(e.headers)


def check(name, ok, detail=""):
    results.append((name, ok, detail))
    print(("  PASS  " if ok else "  FAIL  ") + name + (("  | " + detail) if detail else ""))


def sm2_encrypt(plain: str) -> str:
    """用前端同款 sm-crypto-v2 做 SM2 加密，返回 C1C3C2 十六进制密文。"""
    app_json = os.path.join(REPO, "Admin.NET/Admin.NET.Application/Configuration/App.json")
    with open(app_json, encoding="utf-8") as f:
        # App.json 带 // 注释，标准 json 解析不了，直接正则抠公钥
        public_key = re.search(r'"PublicKey"\s*:\s*"([0-9A-Fa-f]+)"', f.read()).group(1)
    script = (
        "const { sm2 } = require('"
        + os.path.join(REPO, "Web/node_modules/sm-crypto-v2")
        + "');process.stdout.write(sm2.doEncrypt(process.argv[1], process.argv[2], 1));"
    )
    out = subprocess.run([NODE, "-e", script, plain, public_key], capture_output=True, text=True, check=True)
    return out.stdout.strip()


def sign_call(method, url, body=None, ak=AK, sk=SK):
    """按框架 SignatureAuthenticationHandler.GetMessageForSign 的签名串签名。

    ⚠️ nonce 用 uuid，不要用时间戳：框架有重放检测，并发请求若撞同一 nonce
    会拿到 `401 重复的请求`，伪装成业务缺陷。
    """
    path = url.split("?")[0]
    ts = str(int(time.time()))
    nonce = uuid.uuid4().hex
    message = f"{method}&{path}&{ak}&{ts}&{nonce}"
    sig = base64.b64encode(hmac.new(sk.encode(), message.encode(), hashlib.sha256).digest()).decode()
    return call(method, url, body, headers={"accessKey": ak, "timestamp": ts, "nonce": nonce, "sign": sig})


def psql(sql: str):
    """直连后端所用的库（仅用于清理不可篡改的流水表，业务上没有删除接口）。

    ⚠️ 早期实现是 `docker exec ... psql`，依赖 docker 在 PATH 上——
    清理阶段一旦拿不到 docker 就静默失败（脚本还在跑，只是脏数据没清掉），
    下次跑时残留账号会抢走「最佳适配」，让断言凭空失败。
    现在改走共用底座的 psycopg2 直连，不再依赖 docker。

    ★ 连哪个库由 scripts/db_target.py 从 Configuration 解析 —— 与后端**同源**。
      别再写死 127.0.0.1:55432：后端一旦切到远程开发库，清理就会打在另一个库上，
      于是「清干净了」但脏数据还在（或反之，误删别的库）。

    返回值保持与 ``subprocess.run`` 同形状（returncode / stdout / stderr），
    调用方无需改动；支持分号分隔的多条语句。
    """
    from _pay_common import pg

    class _R:
        def __init__(self, code, out, err=""):
            self.returncode, self.stdout, self.stderr = code, out, err

    conn = None
    try:
        conn = pg()
        lines = []
        with conn.cursor() as cur:
            for stmt in [s.strip() for s in sql.split(";") if s.strip()]:
                cur.execute(stmt)
                if cur.description:  # select：把结果渲染成 psql 风格的文本
                    rows = cur.fetchall()
                    lines.append(" | ".join(str(c[0]) for c in cur.description))
                    for row in rows:
                        lines.append(" | ".join(str(v) for v in row))
                    lines.append(f"({len(rows)} rows)")
                else:
                    lines.append(f"DELETE {cur.rowcount}")
        return _R(0, "\n".join(lines))
    except Exception as e:
        return _R(1, "", f"{type(e).__name__}: {e}")
    finally:
        if conn is not None:
            conn.close()


print("=" * 78)
print(f"阶段 0  造数（标识前缀 {TAG}）")

# 0.0 先把上一轮可能残留的造数清掉。
# 不清干净会污染「最佳适配」的选择——allocate 会挑剩余额度最接近的账号，
# 上一轮遗留的账号（额度已被用掉一部分）剩余更小，反而会把新订单抢走，
# 让后面「按新账号过滤订单」的断言凭空失败。
clean_sql = (
    # ★ 审计行必须**最先**删：下面两条子查询依赖 pay_order / pay_account 还在。
    #
    # ★ 为什么原来的 `targetno like 'S6E2E%'` 不够：订单号是**自包含雪花**（纯数字、
    #   无业务前缀），订单审计行的 targetno 是纯数字，`like 'S6E2E%'` 永远匹配不上。
    #   而管理侧动作（账号新增/修改）的审计行 targetno 是**账号类型**（如 wxpay），
    #   更是抓不到。实测残留：action=1/5 的 PayAccount 行 + 若干 action=8 的 Order 行。
    #   可靠的做法是按 clientkey（= accessKey，前缀挂在那里）+ targetid 子查询。
    "delete from pay_audit_log where clientkey like 's6probe_%'"
    " or targetno like 'S6E2E%' or remark like '%S6E2E%'"
    " or (targettype = 'PayAccount' and targetid in"
    "     (select id from pay_account where accountinfo like 'S6E2E%'))"
    " or (targettype = 'Order' and targetid in"
    "     (select id from pay_order where externalno like 'S6E2E%'));"
    "delete from pay_notify_record where orderno like 'S6E2E%' or voucherno like 'S6E2E%';"
    "delete from pay_order_event where orderid in (select id from pay_order where externalno like 'S6E2E%');"
    "delete from pay_order where externalno like 'S6E2E%';"
    "delete from pay_abnormal_receipt where reportorderno like 'S6E2E%' or voucherno like 'S6E2E%';"
    "delete from pay_account where accountinfo like 'S6E2E%';"
    "delete from sysopenaccess where accesskey like 's6probe_%';"
)
psql(clean_sql)

# 0.1 登录（后面建账号/开放身份都要 JWT）
encrypted = sm2_encrypt(PASSWORD)
st, res, _ = call("POST", "/api/sysAuth/login", {"account": ACCOUNT, "password": encrypted})
token = (res or {}).get("result", {}).get("accessToken") if isinstance(res, dict) else None
check("登录成功并拿到 accessToken", st == 200 and bool(token), f"http={st} msg={str(res)[:110]}")
if not token:
    raise SystemExit("登录失败，终止")

# 0.2 建收款账号（走 F1 管理员接口，顺带验证它）
st, res, _ = call(
    "POST",
    "/api/payAccount/add",
    {"type": "wxpay", "accountInfo": f"{TAG}-收款码", "totalQuota": 1000, "remark": f"{TAG} 探针造数"},
    token=token,
)
account_id = (res or {}).get("result") if isinstance(res, dict) else None
check("新增收款账号", st == 200 and isinstance(account_id, int) and account_id > 0, f"accountId={account_id}")

# 0.3 建开放身份（scopes 同时给 allocate + notify，否则签名调用会被 scope 门禁拦）
st, res, _ = call(
    "POST",
    "/api/sysOpenAccess/add",
    {"accessKey": AK, "accessSecret": SK, "bindUserId": SUPER_ADMIN_USER_ID, "bindTenantId": DEFAULT_TENANT_ID, "scopes": "allocate,notify"},
    token=token,
)
check("新增开放接口身份（scopes=allocate,notify）", st == 200 and res.get("code") == 200, f"http={st} msg={res.get('message')}")

# 0.4 签名调用 allocate 建订单
st, res, _ = sign_call("POST", "/api/pay/allocate", {"type": "wxpay", "amount": 88.50, "externalNo": f"{TAG}-EXT-1"})
alloc = (res or {}).get("result") or {}
order_no = alloc.get("orderNo")
check("签名调用 /api/pay/allocate 建单", st == 200 and bool(order_no), f"orderNo={order_no} msg={res.get('message')}")

# 0.5 签名调用 notify 到账（分两次，覆盖「部分到账 → 已完成」）
if order_no:
    st, res, _ = sign_call("POST", "/api/pay/notify", {"orderNo": order_no, "amount": 30.00, "voucherNo": f"{TAG}-V-1"})
    check("第 1 次到账 30.00 → 部分到账", st == 200 and res.get("code") == 200, f"msg={res.get('message')}")
    st, res, _ = sign_call("POST", "/api/pay/notify", {"orderNo": order_no, "amount": 58.50, "voucherNo": f"{TAG}-V-2"})
    check("第 2 次到账 58.50 → 订单已完成", st == 200 and res.get("code") == 200, f"msg={res.get('message')}")

# 0.6 造一条异常到账（报一个不存在的订单号 → 进台账，原因「无匹配订单」）
# 目的是让「导出异常台账」有数据可导，顺带覆盖 F5.1 的入库路径
st, res, _ = sign_call("POST", "/api/pay/notify", {"orderNo": "9999999999999999", "amount": 12.34, "voucherNo": f"{TAG}-V-ABN"})
check("异常到账进台账（无匹配订单）", st == 200 and res.get("code") == 200, f"msg={res.get('message')}")

print("=" * 78)
print("阶段 1  侧边栏菜单树")
st, res, _ = call("GET", "/api/sysMenu/loginMenuTree", token=token)
flat = []


def walk(nodes):
    for n in nodes:
        flat.append(n)
        walk(n.get("children") or [])


walk((res or {}).get("result") or [])
pay_dir = next((n for n in flat if n.get("name") == "paycenter"), None)
check("菜单树含「收款管理」目录", st == 200 and pay_dir is not None, f"name={(pay_dir or {}).get('name')} path={(pay_dir or {}).get('path')}")
kids = sorted([k.get("name") for k in ((pay_dir or {}).get("children") or [])])
check("四个子菜单齐全（账号/订单/异常/审计）", kids == ["payAbnormal", "payAccount", "payAudit", "payOrder"], f"children={kids}")

print("=" * 78)
print("阶段 2  订单查询（F7.2）")
st, res, _ = call("POST", "/api/payOrder/page", {"page": 1, "pageSize": 5}, token=token)
page = (res or {}).get("result") or {}
items = page.get("items") or []
check("订单分页返回 200", st == 200 and res.get("code") == 200, f"http={st} code={res.get('code')} total={page.get('total')}")
check("分页字段名是 hasPrevPage/hasNextPage", "hasPrevPage" in page and "hasNextPage" in page)
mine = [r for r in items if (r.get("externalNo") or "").startswith(TAG)]
check("分页里能查到本类造数", len(mine) > 0, f"rows={len(mine)}")
if mine:
    row = mine[0]
    # 断言的是「列表把账号信息补齐了」，不是「订单落在哪个账号上」——
    # 后者由匹配算法决定（剩余额度最接近者优先），不该在这里写死
    check(
        "列表补齐账号信息与计算字段",
        bool(row.get("accountInfo")) and bool(row.get("accountType")) and bool(row.get("statusText")) and row.get("outstandingAmount") == 0,
        f"accountInfo={row.get('accountInfo')} accountType={row.get('accountType')} statusText={row.get('statusText')} outstanding={row.get('outstandingAmount')}",
    )

# 账号维度过滤用「订单实际落在的账号」，而不是本次新建的账号
if mine:
    landed_account = mine[0]["accountId"]
    st, res, _ = call("POST", "/api/payOrder/page", {"page": 1, "pageSize": 20, "accountId": landed_account}, token=token)
    rows = ((res or {}).get("result") or {}).get("items") or []
    check(
        "账号维度过滤（F7.2）只返回该账号的订单",
        st == 200 and len(rows) > 0 and all(r["accountId"] == landed_account for r in rows),
        f"accountId={landed_account} rows={len(rows)}",
    )

print("=" * 78)
print("阶段 3  订单详情（F7.1 一次给全三张表）")
if mine:
    oid = mine[0]["id"]
    # ⚠️ Detail / DetailByNo 的入参是 [FromQuery]（框架里 Detail(BaseIdInput) 的惯例），
    #    参数必须放查询串，塞进 JSON body 会绑定不到、静默拿到空对象。
    st, res, _ = call("POST", f"/api/payOrder/detail?id={oid}", token=token)
    d = (res or {}).get("result") or {}
    events = d.get("events") or []
    notifies = d.get("notifyRecords") or []
    check("详情含 order/events/notifyRecords", st == 200 and bool(d.get("order")) and "events" in d and "notifyRecords" in d)
    check("事件流水有 3 条（创建 + 部分到账 + 完成）", len(events) == 3, f"events={[e.get('eventTypeText') for e in events]}")
    check("到账明细有 2 条且都已累加", len(notifies) == 2 and all(n.get("applied") for n in notifies), f"notifies={len(notifies)}")
    check("Page 与 Detail 的状态口径一致", d["order"].get("statusText") == mine[0].get("statusText"), f"page={mine[0].get('statusText')} detail={d['order'].get('statusText')}")

    st, res, _ = call("POST", f"/api/payOrder/detailByNo?orderNo={order_no}", token=token)
    check("按订单号查详情成功", st == 200 and ((res or {}).get("result") or {}).get("order"), f"orderNo={order_no}")
    st, res, _ = call("POST", "/api/payOrder/detailByNo?orderNo=0000000000000000", token=token)
    check("按不存在的订单号查详情应报错", res.get("code") != 200, f"code={res.get('code')} msg={res.get('message')}")

print("=" * 78)
print("阶段 4  订单状态下拉")
st, res, _ = call("GET", "/api/payOrder/statusOptions", token=token)
labels = sorted([o.get("label") for o in ((res or {}).get("result") or [])])
check("状态下拉 4 项且中文正确", labels == sorted(["待到账", "部分到账", "已完成", "已过期"]), f"{labels}")

print("=" * 78)
print("阶段 5  4 类导出（F7.5，真下载 xlsx）")
now = _dt.datetime.now()
start = (now - _dt.timedelta(days=1)).strftime("%Y-%m-%d %H:%M:%S")
end = (now + _dt.timedelta(minutes=5)).strftime("%Y-%m-%d %H:%M:%S")

exports = [
    ("订单", "/api/payExport/exportOrder", {"startTime": start, "endTime": end, "accountId": landed_account}),
    ("到账流水", "/api/payExport/exportNotify", {"startTime": start, "endTime": end}),
    ("异常台账", "/api/payExport/exportAbnormal", {"startTime": start, "endTime": end}),
    ("审计日志", "/api/payExport/exportAuditLog", {"startTime": start, "endTime": end}),
]
blobs = {}
for label, path, body in exports:
    st, blob, headers = call("POST", path, body, token=token, raw=True)
    size = len(blob)
    is_xlsx = size > 0 and blob[:2] == b"PK"  # xlsx 就是 zip，魔数 PK
    detail = f"size={size} ct={headers.get('Content-Type')}"
    if not is_xlsx and size > 0:
        detail += " body=" + blob.decode(errors="replace")[:60]
    check(f"导出{label}（xlsx）", st == 200 and is_xlsx, detail)
    if is_xlsx:
        blobs[label] = blob


def sheet_text(xlsx: bytes) -> str:
    """把 xlsx 里的 sheet1 + sharedStrings 抽成纯文本，用于断言表头与取值。"""
    import io
    import zipfile

    out = []
    with zipfile.ZipFile(io.BytesIO(xlsx)) as z:
        for name in z.namelist():
            if name.startswith("xl/worksheets/sheet") or name == "xl/sharedStrings.xml":
                out.append(z.read(name).decode("utf-8", errors="replace"))
    return "".join(out)


if "订单" in blobs:
    text = sheet_text(blobs["订单"])
    # 导出是给人看的：表头必须中文，枚举必须已转中文，不能出现裸枚举值
    headers_expected = ["订单号", "外部业务单号", "收款类型", "收款账号", "请求金额", "累计到账金额", "未达成金额", "订单状态", "创建时间"]
    missing = [h for h in headers_expected if h not in text]
    check("订单导出表头为中文且字段齐全", not missing, f"missing={missing}")
    check("订单导出里能找到本类造数", TAG in text and f"{TAG}-EXT-1" in text)
    check("订单导出的状态列已转中文", "已完成" in text)
if "异常台账" in blobs:
    text = sheet_text(blobs["异常台账"])
    check("异常台账导出含原因与处理状态中文", "无匹配订单" in text and "待处理" in text and f"{TAG}-V-ABN" in text)
if "到账流水" in blobs:
    text = sheet_text(blobs["到账流水"])
    check("到账流水导出含「是否已累加」列且为中文", "是否已累加" in text and "是" in text)
if "审计日志" in blobs:
    text = sheet_text(blobs["审计日志"])
    check("审计日志导出含操作动作中文与操作人", "数据导出" in text and "超级管理员" in text)

print("=" * 78)
print("阶段 6  导出留痕（F7.3 / F7.5）")
st, res, _ = call("POST", "/api/payAudit/page", {"page": 1, "pageSize": 20, "action": 7}, token=token)
logs = ((res or {}).get("result") or {}).get("items") or []
check("审计日志能按「数据导出」筛出记录", st == 200 and len(logs) > 0, f"rows={len(logs)}")
if logs:
    top = logs[0]
    check(
        "最新一条含对象类型 / 说明 / 操作人",
        bool(top.get("targetType")) and bool(top.get("remark")) and bool(top.get("operatorName")),
        f"targetType={top.get('targetType')} operator={top.get('operatorName')} remark={str(top.get('remark'))[:70]}",
    )

st, res, _ = call("POST", "/api/payAudit/page", {"page": 1, "pageSize": 20, "action": 1}, token=token)
acc_logs = ((res or {}).get("result") or {}).get("items") or []
check("「新增收款账号」审计也在（F7.3 业务前后值）", len(acc_logs) > 0, f"rows={len(acc_logs)}")

print("=" * 78)
print("阶段 7  异常台账分页")
st, res, _ = call("POST", "/api/payAbnormal/page", {"page": 1, "pageSize": 5}, token=token)
check("异常台账分页返回 200", st == 200 and res.get("code") == 200, f"http={st} code={res.get('code')}")

print("=" * 78)
print("阶段 8  清理造数")

# ── 8.0 审计行要单独清，且必须走**显式 id / 时间窗** ──────────────────────
# 两类都会「清理语句跑了但该清的行没清」，而**下面 4 项残留检查根本发现不了**
# （它只看订单/账号/开放身份/台账，不看审计表 —— 已补上 audit 一项，见 8.4）：
#
#  ① 账号审计（action=1 新增 / 2 编辑 / 5 删除）的 targetid 是**账号 Id**，
#     而 targetno 存的是**账号类型**（如 wxpay）。所以只能按 Id 抓。
#     ★ 坑在于「什么时候抓」：账号删除**本身也会写一行 action=5**，
#       所以只在接口删除**之前**清会漏掉这一行；而用 `targetid in (select ...)`
#       在接口删除**之后**清又匹配不到（账号行已经没了）。
#       解法：用**显式 Id**（与顺序无关），并且**删除前后各清一次**（8.0 + 8.3）。
#
#  ② 导出审计（action=7）**完全不携带测试标识**：targetid=0、targetno=null、
#     clientkey=null，没有任何字段能被 TAG 匹配。只能按本次运行的时间窗清。
#     （说明：这些行本身是**真实发生过的审计事实**，生产环境绝不该删；
#       这里删是因为它由本探针自己触发，测试工具有义务清理自己产生的痕迹。）
run_clean = "delete from pay_audit_log where action = 7 and createtime >= timestamp '" + RUN_START + "';"
if account_id:
    run_clean += (
        "delete from pay_audit_log where targettype = 'PayAccount' and targetid = "
        + str(int(account_id)) + ";"
    )
r0 = psql(run_clean)
if r0.returncode != 0:
    print("  ⚠️ 审计行清理（前置）未执行，请手工执行：")
    print("     ", run_clean)
    print("     stderr:", r0.stderr[:200])

# 账号与开放身份走接口删（有删除入口）
if account_id:
    call("POST", "/api/payAccount/delete", {"id": account_id}, token=token)
oa = next((r for r in ((call("POST", "/api/sysOpenAccess/page", {"page": 1, "pageSize": 50}, token=token)[1] or {}).get("result") or {}).get("items") or [] if r.get("accessKey") == AK), None)
if oa:
    call("POST", "/api/sysOpenAccess/delete", {"id": oa["id"]}, token=token)
# 订单/事件/到账/台账/审计按设计**没有**删除接口（F7.4 流水不可篡改），只能直连库清
r = psql(clean_sql)
if r.returncode == 0:
    print(r.stdout.strip())
else:
    print("  ⚠️ 直连库清理未执行，请手工执行：")
    print("     ", clean_sql)
    print("     stderr:", r.stderr[:200])

# ── 8.3 再清一次账号审计（见 8.0 ①）────────────────────────────────────
# 接口删除本身会写一行 action=5「删除收款账号」，那一行只有在这一步之后才存在。
# 用显式 targetid，所以与「账号行是否还在」无关，放在这里才抓得全。
if account_id:
    r3 = psql(
        "delete from pay_audit_log where targettype = 'PayAccount' and targetid = "
        + str(int(account_id)) + ";"
    )
    if r3.returncode != 0:
        print("  ⚠️ 审计行清理（后置）未执行，请手工执行：")
        print("     stderr:", r3.stderr[:200])

leftover = psql(
    "select 'order' t, count(*) c from pay_order where externalno like 'S6E2E%' union all "
    "select 'account', count(*) from pay_account where accountinfo like 'S6E2E%' union all "
    "select 'openaccess', count(*) from sysopenaccess where accesskey like 's6probe_%' union all "
    "select 'abnormal', count(*) from pay_abnormal_receipt where reportorderno like 'S6E2E%' or voucherno like 'S6E2E%' union all "
    # ★ 审计表也要查。原来这里只查上面 4 张表，于是「造数已清干净」在审计行残留时**依然 PASS** ——
    #   实测就是这样漏掉了 2 行账号审计 + 4 行导出审计。审计表非 0 的两种可能都要看见：
    #   本探针残留，或**别的进程/人工**动了这套系统（那同样值得当场知道）。
    "select 'audit', count(*) from pay_audit_log;"
)
if leftover.returncode == 0:
    # psql 风格输出形如 "order | 0"，只要没有非零计数就算清干净
    counts = [int(x) for x in re.findall(r"\|\s+(\d+)\s*$", leftover.stdout, re.M)]
    check("造数已清干净", bool(counts) and all(c == 0 for c in counts), "counts=" + str(counts))
else:
    check("造数已清干净", False, f"残留检查查询失败：{leftover.stderr[:120]}")

print("=" * 78)
failed = [r for r in results if not r[1]]
print(f"结果：{len(results) - len(failed)}/{len(results)} 通过")
if failed:
    print("失败项：")
    for n, _, d in failed:
        print("  -", n, d)
raise SystemExit(1 if failed else 0)
