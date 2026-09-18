#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""解析「后端实际会连的数据库」—— 全项目唯一实现。

为什么需要它
  脚本必须知道「后端连的是哪个库」：``dev-up.sh`` 的守卫、``_pay_common.py`` 的造数/清理。
  曾经各自硬编码 ``127.0.0.1:55432/paymentcenter``，于是**切库后脚本会安静地校验另一个库**：
  输出「全绿」，但校验的根本不是后端用的那个库 —— 比失败更糟，因为它给的是假成功信号。
  所以解析逻辑只写在这一处，其它地方一律调它。

数据来源
  ``Admin.NET/Admin.NET.Application/Configuration/Database.json``（唯一来源）。
  若同目录存在 ``Database.<ENV>.json``，按 .NET 的**数组按下标合并**语义叠加（与 Furion 一致）。
  ``<ENV>`` 取 ``PAY_ENV`` → ``ASPNETCORE_ENVIRONMENT`` → ``Development``。
  环境变量 ``PAY_PG_*`` 优先级最高，用于临时指向别的库。

输出
  ``--json``   机器可读（含 password）
  ``--shell``  ``PAY_PG_*`` 的 shell 赋值，供 ``eval "$(...)"`` 接
  默认         人类可读一行（**密码打码**）

退出码
  0 正常；1 读不到配置；``--require-pg`` 时 DbType 不是 PostgreSQL 也退 1。
"""

import json
import os
import re
import shlex
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
CFG_SRC = os.path.join(REPO, "Admin.NET", "Admin.NET.Application", "Configuration")
# Furion 读的是**输出目录的副本**，不是源文件；两者不一致 = 「改了没生效」的经典坑。
CFG_OUT = os.path.join(
    REPO, "Admin.NET", "Admin.NET.Web.Entry", "bin", "Debug", "net8.0", "Configuration"
)

_COMMENT_BLOCK = re.compile(r"/\*.*?\*/", re.S)
# (?<!:) 是为了别把 "http://..." 的 // 当成注释起点
_COMMENT_LINE = re.compile(r"(?<!:)//[^\n]*")
_TRAILING_COMMA = re.compile(r",(\s*[}\]])")

# 连接串键名 → 统一字段。SqlSugar(PG) 与 Npgsql 两种风格都认。
_ALIASES = {
    "host": ("HOST", "SERVER", "DATA SOURCE", "DATASOURCE", "ADDRESS", "ADDR"),
    "port": ("PORT",),
    "database": ("DATABASE", "DB", "INITIAL CATALOG"),
    "user": ("USER ID", "USERID", "USER NAME", "USERNAME", "USER", "UID"),
    "password": ("PASSWORD", "PWD"),
}


def load_jsonc(path):
    """读带 // 与 /* */ 注释、允许尾逗号的 JSON（Admin.NET 的配置就长这样）。"""
    with open(path, encoding="utf-8-sig") as fh:
        raw = fh.read()
    raw = _COMMENT_BLOCK.sub("", raw)
    raw = _COMMENT_LINE.sub("", raw)
    raw = _TRAILING_COMMA.sub(r"\1", raw)
    return json.loads(raw)


def parse_conn_str(conn):
    """把 ``PORT=5432;HOST=x;USER ID=y`` 这类连接串拆成字段。"""
    out = {}
    for part in str(conn or "").split(";"):
        if "=" not in part:
            continue
        key, val = part.split("=", 1)
        key = re.sub(r"\s+", " ", key.strip()).upper()
        for field, names in _ALIASES.items():
            if key in names and field not in out:
                out[field] = val.strip()
    return out


def _first_conn(path):
    cfg = load_jsonc(path) or {}
    conns = ((cfg.get("DbConnection") or {}).get("ConnectionConfigs")) or []
    return dict(conns[0]) if conns else {}


def bin_copy_state():
    """源配置与 bin/ 副本是否一致 —— 不一致就是「改了没重新 build」。"""
    src = os.path.join(CFG_SRC, "Database.json")
    out = os.path.join(CFG_OUT, "Database.json")
    if not os.path.isfile(out):
        return "missing"
    try:
        with open(src, "rb") as a, open(out, "rb") as b:
            return "same" if a.read() == b.read() else "stale"
    except OSError:
        return "unknown"


def resolve(env=None):
    env = (
        env
        or os.environ.get("PAY_ENV")
        or os.environ.get("ASPNETCORE_ENVIRONMENT")
        or "Development"
    )
    base_p = os.path.join(CFG_SRC, "Database.json")
    env_p = os.path.join(CFG_SRC, "Database.%s.json" % env)

    entry = {}
    for path in (base_p, env_p):
        if os.path.isfile(path):
            entry.update(_first_conn(path))
    if not entry:
        raise SystemExit("✗ 读不到数据库配置：%s / %s" % (base_p, env_p))

    fields = parse_conn_str(entry.get("ConnectionString"))
    target = {
        "dbtype": str(entry.get("DbType") or "").strip(),
        "host": fields.get("host", ""),
        "port": int(fields.get("port") or 5432),
        "user": fields.get("user", ""),
        "db": fields.get("database", ""),
        "password": fields.get("password", ""),
        "env": env,
        "source": env_p if os.path.isfile(env_p) else base_p,
    }

    # 环境变量显式覆盖
    if os.environ.get("PAY_PG_HOST"):
        target["host"] = os.environ["PAY_PG_HOST"]
    if os.environ.get("PAY_PG_PORT"):
        target["port"] = int(os.environ["PAY_PG_PORT"])
    if os.environ.get("PAY_PG_USER"):
        target["user"] = os.environ["PAY_PG_USER"]
    if os.environ.get("PAY_PG_DB"):
        target["db"] = os.environ["PAY_PG_DB"]
    if os.environ.get("PAY_PG_PASSWORD"):
        target["password"] = os.environ["PAY_PG_PASSWORD"]

    target["local"] = target["host"] in ("127.0.0.1", "localhost", "::1")
    target["bin_copy"] = bin_copy_state()
    return target


def describe(t):
    return "%s %s:%s/%s（用户 %s）" % (t["dbtype"] or "?", t["host"], t["port"], t["db"], t["user"])


def main(argv):
    as_json = "--json" in argv
    as_shell = "--shell" in argv
    require_pg = "--require-pg" in argv

    t = resolve()

    if require_pg and t["dbtype"] != "PostgreSQL":
        print("✗ DbType 不是 PostgreSQL，而是 %r" % t["dbtype"], file=sys.stderr)
        return 1

    if as_json:
        print(json.dumps(t, ensure_ascii=False, indent=2))
        return 0

    if as_shell:
        pairs = {
            "PAY_PG_HOST": t["host"],
            "PAY_PG_PORT": str(t["port"]),
            "PAY_PG_USER": t["user"],
            "PAY_PG_DB": t["db"],
            "PAY_PG_PASSWORD": t["password"],
            "PAY_PG_LOCAL": "1" if t["local"] else "0",
            "PAY_DB_DESC": describe(t),
        }
        for key, val in pairs.items():
            print("%s=%s" % (key, shlex.quote(val)))
        return 0

    print(describe(t))
    print("  来源：%s（环境 %s）" % (t["source"], t["env"]))
    if t["bin_copy"] == "stale":
        print("  ⚠️ bin/ 里的副本与源文件**不一致** —— 改了配置但没重新 build，"
              "后端跑的仍是旧副本。", file=sys.stderr)
    elif t["bin_copy"] == "missing":
        print("  ⚠️ bin/ 里没有该副本 —— 还没构建过，或构建没复制成功。", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
