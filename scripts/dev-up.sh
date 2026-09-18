#!/usr/bin/env bash
# 一条命令把本地开发环境拉起来：PostgreSQL 容器 → 构建 → 后端 → schema 纠偏 → 守卫校验
#
# 用法：
#   scripts/dev-up.sh                 # 默认端口 5005
#   PORT=5006 scripts/dev-up.sh
#   scripts/dev-up.sh --no-guard      # 跳过 schema 守卫（省几秒）
#
# 停止：scripts/dev-down.sh
# 日志：Admin.NET/Admin.NET.Web.Entry/logs/dev-backend.log
#
# 前端另开一个终端：
#   cd Web && env -u NODE_OPTIONS npm run dev     # http://localhost:8888
#
# 设计说明：
#   * 后端放**后台**跑并写 pid 文件，这样脚本能继续做 schema 纠偏与守卫校验 ——
#     纠偏必须在 CodeFirst 建完表之后做（CodeFirst 只管建，不管改已有列宽）。
#   * 每一步失败都立刻退出并说清原因，不静默继续（本项目的坑几乎都是「静默」型的）。

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENTRY_DIR="$REPO/Admin.NET/Admin.NET.Web.Entry"
LOG_FILE="$ENTRY_DIR/logs/dev-backend.log"
PID_FILE="$REPO/.dev-backend.pid"

DOTNET="${DOTNET:-/usr/local/share/dotnet/dotnet}"
PY="${PY:-$HOME/.workbuddy-ai/binaries/python/envs/default/bin/python}"
PORT="${PORT:-5005}"
PG_CONTAINER="paymentcenter-pg"
DB_NAME="paymentcenter"
DB_USER="payment"
RUN_GUARD=1

for arg in "$@"; do
  case "$arg" in
    --no-guard) RUN_GUARD=0 ;;
    *) echo "未知参数：$arg" >&2; exit 2 ;;
  esac
done

# docker / orbstack 装了但常常不在 PATH 上
export PATH="/usr/local/bin:$PATH"

step() { printf '\n\033[1m==> %s\033[0m\n' "$1"; }
die()  { printf '\033[31m✗ %s\033[0m\n' "$1" >&2; exit 1; }

# ── 0. 前置检查 ──────────────────────────────────────────────────────────────
step "0/6 前置检查"
[[ -x "$DOTNET" ]] || die "找不到 dotnet：$DOTNET（本机不在 PATH 上，需绝对路径）"
command -v docker >/dev/null || die "找不到 docker（OrbStack 装了但可能不在 PATH）"
echo "dotnet : $($DOTNET --version)"
echo "docker : $(docker --version)"

# ── 1. 数据库 ────────────────────────────────────────────────────────────────
step "1/6 启动 PostgreSQL（$PG_CONTAINER）"
if [[ "$(docker inspect -f '{{.State.Running}}' "$PG_CONTAINER" 2>/dev/null || echo false)" != "true" ]]; then
  docker compose -f "$REPO/docker/docker-compose.pg.yml" up -d
else
  echo "容器已在运行"
fi
echo -n "等待就绪"
for sec in $(seq 1 40); do
  if docker exec "$PG_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then
    echo " —— 就绪"; break
  fi
  echo -n "."; sleep 1
done
docker exec "$PG_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1 \
  || die "PostgreSQL 未就绪（看 docker logs $PG_CONTAINER）"

# ── 2. 数据库覆盖检查（★ 这一步是「静默回落」的唯一防线）────────────────────
step "2/6 确认数据库覆盖生效"
OVERRIDE="$ENTRY_DIR/bin/Debug/net8.0/ConfigurationLocal/LocalOverride.json"
[[ -d "$ENTRY_DIR/bin/Debug/net8.0/ConfigurationLocal" ]] \
  || echo "提示：bin 下的 ConfigurationLocal 目录还不存在，构建后会自动创建（csproj 的 MakeDir 目标）"
[[ -f "$ENTRY_DIR/ConfigurationLocal/LocalOverride.json" ]] \
  || die "缺少 $ENTRY_DIR/ConfigurationLocal/LocalOverride.json —— 缺了它后端会**静默回落到 SQLite**，不是连 PG"

# ── 3. 构建 ──────────────────────────────────────────────────────────────────
step "3/6 构建解决方案"
(cd "$REPO/Admin.NET" && "$DOTNET" build Admin.NET.sln --nologo -v q) || die "构建失败"
echo "构建通过"

# ── 4. 启动后端 ──────────────────────────────────────────────────────────────
step "4/6 启动后端（:${PORT}）"
if [[ -f "$PID_FILE" ]] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
  echo "已有后端在跑（pid $(cat "$PID_FILE")），先执行 scripts/dev-down.sh"
  exit 1
fi
mkdir -p "$(dirname "$LOG_FILE")"

# ★ 直接跑**已构建的可执行文件**，不用 `dotnet run`。
#   原因：`dotnet run` 会再派生一个子进程跑真正的宿主，于是
#     (a) nohup 只能保住 `dotnet run` 本身，脚本一退宿主就被带走；
#     (b) pid 文件里记的是 `dotnet run` 的 pid，dev-down.sh 杀不干净。
#   直接跑 apphost 则是**单进程**，pid 就是宿主本身，cwd 也确定（= 项目目录，
#   决定 logs/ 与 ./Admin.NET.db 落在哪）。
APP_BIN="$ENTRY_DIR/bin/Debug/net8.0/Admin.NET.Web.Entry"
[[ -x "$APP_BIN" ]] || die "找不到可执行文件 $APP_BIN（构建没成功？）"

(
  cd "$ENTRY_DIR"
  ASPNETCORE_ENVIRONMENT=Development \
    nohup "$APP_BIN" --urls "http://localhost:${PORT}" >"$LOG_FILE" 2>&1 </dev/null &
  echo $! >"$PID_FILE"
)
echo "pid $(cat "$PID_FILE")，日志 $LOG_FILE"

# ★ 端口探活**不能用 curl**。
#   实测（本机沙箱）：curl 对一个**没有任何进程监听**的端口也返回退出码 0
#   （`curl -s -o /dev/null --max-time 2 http://localhost:5099/... ; echo $?` → 0）。
#   用它做就绪判断会**无条件打印「已就绪」**，把启动失败彻底掩盖掉 ——
#   比没有探活更糟，因为它会给出一个假的成功信号。
#   zsh 也不支持 bash 的 /dev/tcp，所以那条路也不通。
#   lsof 可靠：有监听时输出 2 行（表头 + 记录），无监听时 0 行。
#   拿不到 lsof/ss 时**返回未就绪**（fail-closed）：宁可超时报错，也不要误报就绪。
port_listening() {
  if command -v lsof >/dev/null 2>&1; then
    [[ -n "$(lsof -nP -iTCP:"$PORT" -sTCP:LISTEN 2>/dev/null)" ]]
  elif command -v ss >/dev/null 2>&1; then
    [[ -n "$(ss -ltnH "sport = :$PORT" 2>/dev/null)" ]]
  else
    return 1
  fi
}

echo -n "等待端口就绪"
READY=0
# ★ 循环变量**不要用 `_`**：`$_` 是 bash 的特殊变量（上一条命令的最后一个参数），
#   而 `port_listening` 是函数调用 → 会把 `$_` 覆盖成函数名，
#   于是输出变成「已就绪（第 port_listening 秒）」。实测踩过。
for sec in $(seq 1 90); do
  if port_listening; then echo " —— 已就绪（第 ${sec} 秒）"; READY=1; break; fi
  echo -n "."; sleep 1
done
if [[ "$READY" != "1" ]]; then
  echo
  echo "✗ 后端未在 90s 内就绪。最后 30 行日志：" >&2
  tail -30 "$LOG_FILE" >&2 || true
  die "后端未就绪（日志 $LOG_FILE）"
fi

# 数据库到底是哪个？启动日志里有一行「初始化数据库 xxx」—— 直接断言，不靠猜
if grep -q "初始化数据库 Sqlite" "$LOG_FILE"; then
  echo
  echo "⚠️  启动日志显示**连的是 SQLite**，不是 PostgreSQL ——"
  echo "    说明覆盖文件没被读到。检查 $OVERRIDE 是否存在（需重新构建才会同步到 bin）。"
  echo "    上游日志：$(grep -m1 '初始化数据库' "$LOG_FILE")"
fi

# ── 5. schema 纠偏（幂等；必须在 CodeFirst 建表之后）────────────────────────
step "5/6 应用 schema 契约与列宽纠偏"
docker exec -i "$PG_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 -q \
  <"$REPO/scripts/paycenter-schema.sql" || die "paycenter-schema.sql 执行失败"
echo "paycenter-schema.sql 已应用（可反复执行）"

# ── 6. 守卫校验 ──────────────────────────────────────────────────────────────
if [[ "$RUN_GUARD" == "1" ]]; then
  step "6/6 schema 漂移守卫"
  if [[ -x "$PY" ]]; then
    "$PY" "$REPO/scripts/pay_schema_guard.py" || die "schema 守卫未通过（说明代码/活库/入参三者已漂移）"
  else
    echo "跳过：找不到 python venv（$PY），它才有 psycopg2"
  fi
else
  step "6/6 schema 守卫（已用 --no-guard 跳过）"
fi

# ── 摘要 ─────────────────────────────────────────────────────────────────────
cat <<EOF

────────────────────────────────────────────
 本地环境就绪
────────────────────────────────────────────
 后端      http://localhost:${PORT}
 Swagger   http://localhost:${PORT}/swagger/index.html
 数据库    PostgreSQL 16 @ 127.0.0.1:55432/${DB_NAME}
 日志      $LOG_FILE

 前端（另开终端）：
   cd Web && env -u NODE_OPTIONS npm run dev     # http://localhost:8888

 停止后端：scripts/dev-down.sh

 登录：租户图形验证码**默认开启**。注意 SysTenantSeedData.cs 有**两份**，
       生效的是 Admin.NET.Application/SeedData/SysTenantSeedData.cs（[SeedData(500)]，
       Id 与框架一致 = 重写框架种子）；Core/SeedData 那份带 [IgnoreUpdateSeed]，改它没用。
       而且租户列表有缓存 → **直接改库也无效**，必须改种子再重启。
       浏览器调试正常输入验证码即可；用 Postman / 脚本调接口要先关掉它。
       另：登录口令必须 SM2 加密，见 scripts/_pay_common.py 的 sm2_encrypt。
EOF
