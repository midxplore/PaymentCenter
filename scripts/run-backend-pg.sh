#!/usr/bin/env bash
# 启动后端（本地开发）
#
# 用法：
#   scripts/run-backend-pg.sh                     # 默认 PG，http://localhost:5005
#   URLS=http://localhost:5006 scripts/run-backend-pg.sh
#   DB=sqlite scripts/run-backend-pg.sh           # 应急：切回 SQLite（见下）
#   SKIP_BUILD=1 scripts/run-backend-pg.sh        # 跳过构建（快，但见下方警告）
#
# ─────────────────────────────────────────────────────────────────────────────
# 数据库怎么定的（三处联动，改之前先看懂）
#
#   appsettings.json  ConfigurationScanDirectories = ["Configuration", "", "ConfigurationLocal"]
#       ↓ Furion 按顺序扫描，**后扫描的优先级最高**（高于环境变量与根 appsettings*.json）
#   bin/<cfg>/ConfigurationLocal/LocalOverride.json  ← 覆盖 ConnectionConfigs[0] 的 DbType/连接串
#       ↓ 没有这个文件时
#   Configuration/Database.Development.json          ← 回落目标（SQLite）
#
#   两个必须知道的事实：
#     1) **Furion 读的是输出目录（bin/）里的那份**，不是仓库里的源文件。
#        改了源目录的 LocalOverride.json 而不重新 build，运行时用的还是旧副本。
#     2) **ConfigurationLocal 目录必须存在**，否则 Furion 在 AddJsonFiles 阶段抛
#        DirectoryNotFoundException（主机构建期，早于日志系统 → logs/ 里什么都没有，
#        只在控制台留一段栈）。目录由 csproj 的 EnsureConfigurationLocalInOutput /
#        EnsureConfigurationLocalInPublish 两个 MakeDir 目标保证，别删。
#
# ─────────────────────────────────────────────────────────────────────────────
# 关于「切回 SQLite」—— 常见的说法是错的，这里说明正确做法
#
#   ❌ 只把 ConfigurationLocal/LocalOverride.json 从**源目录**移走：
#        不重新 build → bin 里的旧副本仍在 → 还是连 PG（你以为切了，其实没切）；
#        重新 build → 目录里的 json 没了，若目录也随之消失 → **下次启动直接崩**。
#
#   ✅ 本脚本 DB=sqlite 的做法：**只临时改名 bin 里的那一份**（Furion 真正读的那份），
#        源文件不动、目录不动，进程退出时自动还原。
#        这样既真的切过去了，又不会踩「目录消失 → 崩溃」的坑。
#
#   另外注意：SQLite 只是应急回落，**正式开发请用 PG** ——
#   本项目的额度预占用了 `FOR UPDATE` 行锁，SQLite 没有这个语义，并发行为不可比。
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENTRY_DIR="$REPO/Admin.NET/Admin.NET.Web.Entry"
cd "$ENTRY_DIR"

DOTNET="${DOTNET:-/usr/local/share/dotnet/dotnet}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

OUT_DIR="bin/Debug/net8.0"
OVERRIDE="$OUT_DIR/ConfigurationLocal/LocalOverride.json"
RESTORE_NEEDED=0

restore_override() {
  if [[ "$RESTORE_NEEDED" == "1" && -f "$OVERRIDE.disabled" ]]; then
    mv -f "$OVERRIDE.disabled" "$OVERRIDE"
    echo "[run-backend] 已还原 $OVERRIDE"
  fi
}
trap restore_override EXIT

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

# ── 数据库选择 ──────────────────────────────────────────────────────────────
if [[ "${DB:-pg}" == "sqlite" ]]; then
  if [[ ! -d "$OUT_DIR/ConfigurationLocal" ]]; then
    echo "[run-backend] ✗ 缺少 $OUT_DIR/ConfigurationLocal（应由构建生成；先执行一次不带 SKIP_BUILD 的构建）" >&2
    exit 1
  fi
  if [[ -f "$OVERRIDE" ]]; then
    mv -f "$OVERRIDE" "$OVERRIDE.disabled"
    RESTORE_NEEDED=1
    echo "[run-backend] ⚠️ DB=sqlite：已临时停用 PG 覆盖 → 本次连 SQLite（仅应急；并发语义与 PG 不同）"
  fi
else
  if [[ ! -f "$OVERRIDE" ]]; then
    echo "[run-backend] ⚠️ 未找到 $OVERRIDE" >&2
    echo "[run-backend]    → 本次会**静默回落到 SQLite**（启动日志里会出现「初始化数据库 Sqlite」）。" >&2
    echo "[run-backend]    想连 PG：确认 $ENTRY_DIR/ConfigurationLocal/LocalOverride.json 存在并重新构建。" >&2
  else
    echo "[run-backend] 数据库覆盖：$OVERRIDE → PostgreSQL"
  fi
fi

# ── 启动 ────────────────────────────────────────────────────────────────────
exec "$DOTNET" run --no-build --urls "${URLS:-http://localhost:5005}"
