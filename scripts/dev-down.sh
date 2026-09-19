#!/usr/bin/env bash
# 停止 dev-up.sh 起的后端（数据库容器默认保留）
#
# 用法：
#   scripts/dev-down.sh            # 只停后端
#   scripts/dev-down.sh --with-db  # 连 PostgreSQL 容器一起停
#   scripts/dev-down.sh --purge-db # 停库并**删除数据卷**（下次是全新库，慎用）
#
# 说明：只杀 dev-up.sh 写下的 pid。不用 `pkill dotnet` 之类的广谱匹配 ——
#       机器上可能还有别的 .NET 进程，误杀起来很难排查。

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PID_FILE="$REPO/.dev-backend.pid"
PG_CONTAINER="paymentcenter-pg"
WITH_DB=0
PURGE_DB=0

for arg in "$@"; do
  case "$arg" in
    --with-db)  WITH_DB=1 ;;
    --purge-db) WITH_DB=1; PURGE_DB=1 ;;
    *) echo "未知参数：$arg" >&2; exit 2 ;;
  esac
done

export PATH="/usr/local/bin:$PATH"

# ── 停后端 ──────────────────────────────────────────────────────────────────
if [[ -f "$PID_FILE" ]]; then
  PID="$(cat "$PID_FILE")"
  if kill -0 "$PID" 2>/dev/null; then
    # dev-up.sh 起的是**单进程**（直接跑 apphost，不是 `dotnet run`），
    # 所以按 pid 精确 kill 即可。★ 不要写成 `kill -TERM -$PID`：
    # 非交互 shell 里后台任务**不会**自成进程组，`-$PID` 打的是整个脚本所在进程组，
    # 会波及无辜进程。
    kill -TERM "$PID" 2>/dev/null || true
    for _ in $(seq 1 15); do
      kill -0 "$PID" 2>/dev/null || break
      sleep 1
    done
    if kill -0 "$PID" 2>/dev/null; then
      echo "优雅退出超时，强制结束 $PID"
      kill -KILL "$PID" 2>/dev/null || true
    fi
    echo "后端已停止（pid ${PID}）"
  else
    echo "pid $PID 已不在运行，清理 pid 文件"
  fi
  rm -f "$PID_FILE"
else
  echo "没有 pid 文件（${PID_FILE}）—— 后端可能不是 dev-up.sh 起的，或已停止"
fi

# ── 停数据库 ────────────────────────────────────────────────────────────────
if [[ "$WITH_DB" == "1" ]]; then
  if [[ "$PURGE_DB" == "1" ]]; then
    echo "⚠️  --purge-db：将删除数据卷 paymentcenter_pgdata，库内数据全部丢失"
    docker compose -f "$REPO/docker/docker-compose.pg.yml" down -v
  else
    docker compose -f "$REPO/docker/docker-compose.pg.yml" down
  fi
  echo "PostgreSQL 容器已停止"
fi
