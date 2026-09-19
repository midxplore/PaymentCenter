#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""收款账号分配系统 —— 回归脚本共用底座。

把三个回归脚本（allocate 压测 / notify 回归 / S6 后台探针）都要用的东西收在一处，
避免「签名串怎么拼」「PG 列名大小写」「SM2 口令怎么加」这三件事在多个文件里各写一遍、
然后某一处悄悄漂移。

提供：
  * HTTP：``call`` / ``sign_call`` / ``admin_call``（含 401/400 也回原始响应体）
  * 鉴权：``sm2_encrypt``（前端同款库）/ ``admin_login``（拿 JWT）
  * 数据库：``pg`` / ``pg_query`` / ``pg_exec``（直连 PG，列名一律小写）
  * 断言：``assert_true`` / ``section`` / ``report``

环境变量：
  PAY_BASE          后端地址，默认 http://localhost:5005
  PAY_PG_HOST       PG 主机   ┐
  PAY_PG_PORT       PG 端口   │ 不设时从 Configuration/Database.json 解析
  PAY_PG_USER       PG 用户   │ （见 scripts/db_target.py）；设了则显式覆盖
  PAY_PG_DB         PG 库名   │
  PAY_PG_PASSWORD   PG 口令   ┘
  PAY_ENV           配置环境名，默认 Development
  PAY_NODE          node 可执行文件（SM2 加密要用）

★ 数据库目标**不在这里写死**：回归脚本必须和「后端实际连的库」是同一个，
  否则会安静地校验另一个库、给出假的「全绿」。解析逻辑唯一实现见 db_target.py。
"""

import base64
import hashlib
import hmac
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

# ─────────────────────────── 配置 ───────────────────────────

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# ★ 数据库目标由 scripts/db_target.py 从配置解析 —— 与后端实际连的库**同一个来源**。
#   这里只负责「取出来放成模块常量」，不承担任何解析/默认值逻辑。
_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)
import db_target  # noqa: E402

_DB = db_target.resolve()

BASE = os.environ.get("PAY_BASE", "http://localhost:5005")
PG_HOST = _DB["host"]
PG_PORT = _DB["port"]
PG_USER = _DB["user"]
PG_DB = _DB["db"]
PG_PASSWORD = _DB["password"]
PG_DESC = db_target.describe(_DB)

NODE = os.environ.get("PAY_NODE", "/Users/ipan/.workbuddy-ai/binaries/node/versions/22.22.2-2/bin/node")

# ★ 默认值必须与后端种子一致（SysUserSeedData 的 superLang + SysConfigSeedData 的 sys_password）。
#   原默认值 superAdmin.NET / Admin.NET++010101 是上游**演示环境**的账号口令，本仓库库里不存在 →
#   不设 PAY_ADMIN_* 时 admin_login 必然失败，且报的是「账号或密码错误」，容易被当成产品缺陷。
ADMIN_ACCOUNT = os.environ.get("PAY_ADMIN_ACCOUNT", "superLang")
ADMIN_PASSWORD = os.environ.get("PAY_ADMIN_PASSWORD", "Langya.18")

# 本机跑着 HTTP_PROXY，直连 127.0.0.1 必须绕开，否则会被代理拦成 502
_opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

# ─────────────────────────── HTTP ───────────────────────────


def call(method, path, body=None, token=None, raw=False, headers=None, timeout=60):
    """返回 ``(http_status, payload, response_headers)``。

    ``raw=True`` 时 payload 一定是 bytes（错误响应也一样）——导出接口失败时后端回的是
    JSON 字符串而不是文件，走 json.loads 会得到一个 str，后面按 bytes 判长度/魔数就会全判错。
    """
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    try:
        with _opener.open(req, timeout=timeout) as r:
            payload = r.read()
            return r.status, (payload if raw else json.loads(payload.decode() or "{}")), dict(r.headers)
    except urllib.error.HTTPError as e:
        text = e.read().decode(errors="replace")
        if raw:
            return e.code, text.encode(), dict(e.headers)
        try:
            return e.code, json.loads(text), dict(e.headers)
        except Exception:
            return e.code, text, dict(e.headers)
    except Exception as e:  # 网络层异常（连不上/超时）
        return -1, {"code": -1, "message": f"{type(e).__name__}: {e}"}, {}


def sign_call(method, url, body=None, ak=None, sk=None, raw=False, timeout=60):
    """带签名请求头调用开放接口。

    签名串按框架 ``SignatureAuthenticationHandler.GetMessageForSign``：
    ``{method}&{path}&{accessKey}&{timestamp}&{nonce}``，
    其中 path 是 ``context.Request.Path``——**不含查询串**，所以这里要 split("?")[0]。

    ⚠️ **nonce 必须真正唯一，不能用时间戳凑**。框架有重放检测
    （``CacheConst.KeyOpenAccessNonce`` + 缓存），同一个 ``(accessKey, timestamp, nonce)``
    第二次进来直接 ``401 重复的请求``。如果拿毫秒时间戳当 nonce，
    **并发请求会在同一毫秒内撞 nonce**，表现为「N 个并发只有 1 个成功」——
    这会伪装成产品的并发缺陷，非常容易被误判。所以这里掺 uuid。
    """
    if ak is None or sk is None:
        raise ValueError("sign_call 需要 accessKey / secretKey")
    path = url.split("?")[0]
    ts = str(int(time.time()))
    nonce = uuid.uuid4().hex
    message = f"{method}&{path}&{ak}&{ts}&{nonce}"
    sig = base64.b64encode(hmac.new(sk.encode(), message.encode(), hashlib.sha256).digest()).decode()
    return call(method, url, body, raw=raw, timeout=timeout,
                headers={"accessKey": ak, "timestamp": ts, "nonce": nonce, "sign": sig})


def admin_call(method, path, body=None, token=None, raw=False, timeout=60):
    """后台管理接口：JWT 身份。"""
    return call(method, path, body, token=token, raw=raw, timeout=timeout)


def ok(resp):
    """业务成功判定：body.code == 200。"""
    return isinstance(resp, dict) and resp.get("code") == 200


# ─────────────────────────── 鉴权 ───────────────────────────


def sm2_encrypt(plain):
    """用前端同款 sm-crypto-v2 做 SM2 加密，返回 C1C3C2 十六进制密文。

    ⚠️ 不要用框架自带的 ``/api/sysCommon/sM2Encrypt/{plainText}``——那个接口自身要登录，
    而登录又需要密文，是死循环。第三个参数 ``1`` = C1C3C2，与后端 BouncyCastle 一致
    （前端登录页 ``account.vue`` 也是这么调的）。
    """
    app_json = os.path.join(REPO, "Admin.NET/Admin.NET.Application/Configuration/App.json")
    with open(app_json, encoding="utf-8") as f:
        # App.json 带 // 注释，标准 json 解析不了，直接正则抠公钥
        public_key = re.search(r'"PublicKey"\s*:\s*"([0-9A-Fa-f]+)"', f.read()).group(1)
    script = (
        "const { sm2 } = require('"
        + os.path.join(REPO, "Web/node_modules/sm-crypto-v2")
        + "');process.stdout.write(sm2.doEncrypt(process.argv[1], process.argv[2], 1));"
    )
    out = subprocess.run([NODE, "-e", script, plain, public_key],
                         capture_output=True, text=True, check=True)
    return out.stdout.strip()


def admin_login(account=None, password=None):
    """登录后台，返回 accessToken。

    ⚠️ 前置条件：租户的图形验证码必须已关闭（``systenant.captcha = f``）。
    改法见 ``scripts/README`` 或设计文档——**必须改种子再重启**，直接改库会被启动种子覆盖回去。
    """
    enc = sm2_encrypt(password or ADMIN_PASSWORD)
    st, res, _ = call("POST", "/api/sysAuth/login",
                      {"account": account or ADMIN_ACCOUNT, "password": enc})

    # ★ 不要写成 `(res or {}).get("result", {}).get("accessToken")`：
    #   dict.get 的默认值**只在键不存在时**生效。登录失败时后端回的是
    #   `{"code":400,"message":"[D0008] 验证码错误","result":null}` ——
    #   `result` 这个键**存在但值是 None**，于是 `.get("result", {})` 返回 None，
    #   紧接着对 None 调 `.get` → `AttributeError: 'NoneType' object has no attribute 'get'`。
    #   这个报错**完全不提验证码**，会把人引向「脚本坏了」而不是「验证码没关」，
    #   实测误导过两次排查。所以这里显式取一次并给出带原始响应的报错。
    result = res.get("result") if isinstance(res, dict) else None
    token = (result or {}).get("accessToken")
    if not token:
        hint = ""
        if isinstance(res, dict) and "验证码" in str(res.get("message", "")):
            hint = ("\n  ⚠️ 租户图形验证码是开启的。必须改 SysTenantSeedData.cs 的 Captcha=false "
                    "再重启后端（直接 UPDATE 库无效：JwtHandler 读的是租户缓存）。")
        raise RuntimeError(f"登录失败：http={st} resp={str(res)[:200]}{hint}")
    return token


# ─────────────────────────── 数据库（PostgreSQL） ───────────────────────────
#
# ★ PG 把未加引号的标识符折叠成小写：列名是 orderno / accountid / totalquota，
#   不是 OrderNo / AccountId / TotalQuota。直连 SQL 一律用小写。
#
# ★ 连接参数来自 db_target（= 后端配置），**不再硬编码本地容器**。
#   本地容器用 trust 认证（无口令），远程库要口令 —— 所以 password 为空时不传该参数。
#   旧注释曾说「Configuration/Database*.json 是受保护文件、读会被沙箱拦」：
#   实测**不成立**（`wc -c` 与 python 读取均正常返回），那条说法已删除。


def pg():
    """新建一个 autocommit 连接（调用方负责 close）。"""
    import psycopg2  # 装在 ~/.workbuddy-ai/binaries/python/envs/default
    kwargs = dict(host=PG_HOST, port=PG_PORT, user=PG_USER, dbname=PG_DB)
    if PG_PASSWORD:
        kwargs["password"] = PG_PASSWORD
    conn = psycopg2.connect(**kwargs)
    conn.autocommit = True
    return conn


def pg_query(sql, params=None):
    """查询，返回 list[dict]。"""
    conn = pg()
    try:
        with conn.cursor() as cur:
            cur.execute(sql, params)
            cols = [d[0] for d in cur.description]
            return [dict(zip(cols, row)) for row in cur.fetchall()]
    finally:
        conn.close()


def pg_one(sql, params=None):
    """查询单行，返回 dict 或 None。"""
    rows = pg_query(sql, params)
    return rows[0] if rows else None


def pg_scalar(sql, params=None):
    """查询单值。"""
    row = pg_one(sql, params)
    return list(row.values())[0] if row else None


def pg_exec(sql, params=None):
    """执行写语句，返回影响行数。"""
    conn = pg()
    try:
        with conn.cursor() as cur:
            cur.execute(sql, params)
            return cur.rowcount
    finally:
        conn.close()


# ─────────────────────────── 断言与输出 ───────────────────────────

PASS = []
FAIL = []


def assert_true(cond, label, detail=""):
    (PASS if cond else FAIL).append(label)
    print(("  PASS  " if cond else "  FAIL  ") + label + (f"   → {detail}" if detail else ""))
    return bool(cond)


def section(title):
    print(f"\n{'─' * 74}\n▶ {title}\n{'─' * 74}")


def report(title="结果"):
    print(f"\n{'═' * 74}")
    print(f"{title}：通过 {len(PASS)} 项，失败 {len(FAIL)} 项")
    if FAIL:
        print("\n失败项：")
        for f in FAIL:
            print(f"  ✗ {f}")
    print("═" * 74)
    return 1 if FAIL else 0


def wait_backend(timeout=120):
    """等服务就绪（swagger 文档 >100KB 视为就绪）。"""
    import urllib.parse
    probe = "/swagger/All%20Groups/swagger.json"
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            req = urllib.request.Request(BASE + probe)
            with _opener.open(req, timeout=5) as r:
                if r.status == 200 and len(r.read()) > 100_000:
                    return True
        except Exception:
            pass
        time.sleep(3)
    return False


def dec(v):
    """转成两位小数的 Decimal，避免浮点比较踩坑。"""
    from decimal import Decimal
    return Decimal(str(v)).quantize(Decimal("0.01"))
