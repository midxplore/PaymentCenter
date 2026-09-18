#!/usr/bin/env bash
# 启动后端（本地开发，**前台**运行）
#
# 用法：
#   scripts/run-backend-pg.sh                     # 默认 http://localhost:5005
#   URLS=http://localhost:5006 scripts/run-backend-pg.sh
#   SKIP_BUILD=1 scripts/run-backend-pg.sh        # 跳过构建（快，但见下方警告）
#
# 与 dev-up.sh 的分工：
#   dev-up.sh         一键（起库 → 构建 → **后台**起后端 → schema 纠偏 → 守卫），适合从零拉起。
#   run-backend-pg.sh **前台**运行后端，适合要持续盯日志、或被上层进程托管（如后台任务）时用。
#
# ─────────────────────────────────────────────────────────────────────────────
# 数据库配置怎么定的（只有一处，改之前先看懂）
#
#   Admin.NET.Application/Configuration/Database.json   ← 唯一来源（DbType / ConnectionString）
#     ↓ Admin.NET.Application.csproj 把 Configuration\**\* 复制到输出目录
#   bin/Debug/net8.0/Configuration/*.json   ← Furion **真正读的是这里**
#
#   ★ 两个必须知道的事实：
#     1) **Furion 读的是输出目录（bin/）里的副本**，不是仓库里的源文件。
#        改了源文件而不重新 build，运行时用的还是旧副本 —— 现象是「改了没生效」。
#     2) 配置写错时后端**不报错**，只是安静地连上另一个库，
#        业务代码于是在错误的数据上跑，看起来像「功能没实现」。
#        所以本脚本启动前**断言** bin/ 下确有这份配置；
#        「真的连上了 PostgreSQL」的断言在 dev-up.sh（它读启动日志的「初始化数据库」那一行）。
#
#   ★ 本项目只有 PostgreSQL 一种形态，配置里不保留任何回落项。
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENTRY_DIR="$REPO/Admin.NET/Admin.NET.Web.Entry"
CFG_SRC="$REPO/Admin.NET/Admin.NET.Application/Configuration"
cd "$ENTRY_DIR"

DOTNET="${DOTNET:-/usr/local/share/dotnet/dotnet}"
PY="${PY:-$HOME/.workbuddy-ai/binaries/python/envs/default/bin/python}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

OUT_DIR="bin/Debug/net8.0"
CFG_OUT="$OUT_DIR/Configuration"

# ── 构建 ────────────────────────────────────────────────────────────────────
# 默认每次都构建。原因：`dotnet run --no-build` 跑的是 bin/ 里的**已有**二进制，
# 而 `dotnet test` 只刷新 Application/bin、不刷新 Web.Entry/bin ——
# 改了业务代码不 build 就起服务，会拿旧代码跑，且看不出任何异常。
if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
  echo "[run-backend] 构建解决方案…"
  (cd "$REPO/Admin.NET" && "$DOTNET" build Admin.NET.sln --nologo -v q) || {
    echo "[run-backend] ✗ 构建失败" >&2; exit 1;
  }
else
  echo "[run-backend] ⚠️ SKIP_BUILD=1：跑的是 bin/ 里的旧二进制，改过代码就别这么用"
fi

# ── 数据库配置检查（fail-closed，防止静默连错库）────────────────────────────
if [[ ! -f "$CFG_SRC/Database.json" ]]; then
  echo "[run-backend] ✗ 缺少 $CFG_SRC/Database.json" >&2
  echo "[run-backend]   它是连 PG 的唯一来源（DbType / ConnectionString）。" >&2
  exit 1
fi
if [[ ! -f "$CFG_OUT/Database.json" ]]; then
  echo "[run-backend] ✗ $CFG_OUT/Database.json 不存在 —— 构建没有把它复制到输出目录。" >&2
  echo "[run-backend]   检查 Admin.NET.Application.csproj 里 Configuration\\**\\* 的 CopyToOutputDirectory。" >&2
  exit 1
fi
echo "[run-backend] 数据库配置：$CFG_SRC/Database.json（已复制到 $CFG_OUT/）"
# 顺手把「即将连的库」打出来 —— 脚本本身不决定库，只是让操作者看得见，
# 免得对着一个远程库调半天却以为是本地容器。
if [[ -x "$PY" ]]; then
  echo "[run-backend] 目标库：$("$PY" "$REPO/scripts/db_target.py")"
fi

# ── 启动 ────────────────────────────────────────────────────────────────────
# exec：让 pid 就是宿主本身（而不是 dotnet run 的父进程），上层进程托管时杀得干净。
exec "$DOTNET" run --no-build --urls "${URLS:-http://localhost:5005}"
