#!/usr/bin/env bash
# 一条命令把本地开发环境拉起来：解析目标库 →（必要时起容器）→ 构建 → 后端 → schema 纠偏 → 守卫校验
#
# 用法：
#   scripts/dev-up.sh                 # 默认端口 5005
#   PORT=5006 scripts/dev-up.sh
#   READY_MAX=1800 scripts/dev-up.sh  # 放宽就绪等待（首次对远端空库建表很慢）
#   scripts/dev-up.sh --no-guard      # 跳过 schema 守卫（省几秒）
#
# 停止：scripts/dev-down.sh
# 日志：Admin.NET/Admin.NET.Web.Entry/logs/dev-backend.log
#
# 连哪个库？**不看本脚本** —— 由 Admin.NET/Admin.NET.Application/Configuration/
# Database.json 决定，本脚本用 scripts/db_target.py 读它（唯一实现），
# 并让 schema 纠偏 / 守卫打在**同一个库**上。临时指向别处用 PAY_PG_HOST/PORT/USER/DB。
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
RUN_GUARD=1
# 就绪等待上限（秒）。CodeFirst 启动时会**逐张**处理全部表（不只是新建），
# 远端库单张实测 3~25s（延迟波动很大），45 张最坏可到 15 分钟以上。
# 循环一旦探到端口就立刻退出，所以把上限放宽**没有代价**；宁可等，也别误报失败。
# 可用 READY_MAX 覆盖。
READY_MAX="${READY_MAX:-1800}"

# 本地容器的**自用**凭据 —— 只用于容器健康检查 / 启停，与「后端连哪个库」无关。
PG_CONTAINER="paymentcenter-pg"
DB_NAME="paymentcenter"
DB_USER="payment"

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

# 把连接串里的口令打码再输出 —— 后端日志那行「初始化数据库 …」是**明文带口令**的，
# 直接 echo 会把口令写进终端记录。
mask_secret() {
  "$PY" -c 'import re,sys; sys.stdout.write(re.sub(r"(?i)(password|pwd)\s*=\s*[^;]*", r"\1=***", sys.stdin.read()))'
}

# 打印日志尾部若干行（同样打码）。
# ★ 不用 `tail`：本沙箱对后端日志**禁用 tail**（`Operation not permitted`），
#   而 python 读同一个文件正常 —— 失败路径若依赖 tail，报错时会什么都不显示。
show_log_tail() {
  "$PY" - "$1" "${2:-30}" <<'PYEOF'
import re, sys
path, n = sys.argv[1], int(sys.argv[2])
try:
    lines = open(path, encoding="utf-8", errors="replace").readlines()[-n:]
except OSError as exc:
    sys.stderr.write("（读不到日志：%s）\n" % exc)
    sys.exit(0)
txt = "".join(lines)
sys.stderr.write(re.sub(r"(?i)(password|pwd)\s*=\s*[^;]*", r"\1=***", txt))
PYEOF
}

# ── 解析「后端实际连的库」──────────────────────────────────────────────────
# ★ 唯一实现见 scripts/db_target.py；这里只消费它的输出。
#   过去本脚本把库写死成 127.0.0.1:55432/paymentcenter —— 一旦后端切到别的库，
#   第 5/6 步（schema 纠偏 + 守卫）就会安静地校验**另一个库**：输出全绿，
#   但后端用的库根本没被验过。这是本项目最忌讳的静默失效。
[[ -x "$PY" ]] || die "找不到 python venv：$PY（解析数据库配置要用它，也只有它有 psycopg2）"
DB_ENV_OUT="$("$PY" "$REPO/scripts/db_target.py" --shell --require-pg)" \
  || die "无法从 Configuration 解析数据库目标，或 DbType 不是 PostgreSQL（scripts/db_target.py）"
eval "$DB_ENV_OUT"
# ★ 必须 export：`eval` 只设了 **shell 变量**，不会进环境。
#   第 5 步的 python 与第 6 步的守卫读的都是 `os.environ` —— 不导出就会 KeyError。
export PAY_PG_HOST PAY_PG_PORT PAY_PG_USER PAY_PG_DB PAY_PG_PASSWORD

# ── 0. 前置检查 ──────────────────────────────────────────────────────────────
step "0/6 前置检查"
[[ -x "$DOTNET" ]] || die "找不到 dotnet：$DOTNET（本机不在 PATH 上，需绝对路径）"
echo "dotnet : $($DOTNET --version)"
echo "目标库 : $PAY_DB_DESC"
if [[ "$PAY_PG_LOCAL" == "1" ]]; then
  command -v docker >/dev/null || die "找不到 docker（OrbStack 装了但可能不在 PATH）"
  echo "docker : $(docker --version)"
else
  echo "docker : （目标库不在本机，本次不需要 docker）"
fi

# ── 1. 数据库 ────────────────────────────────────────────────────────────────
step "1/6 数据库"
if [[ "$PAY_PG_LOCAL" != "1" ]]; then
  # 后端连的是非本机地址 → 本地容器与它无关：不启动、不等待。
  # （容器只服务于 127.0.0.1:55432 这个目标。）
  echo "目标库不在本机，跳过本地容器 $PG_CONTAINER"
else
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
fi

# ── 2. 数据库配置检查（★ 这是「静默连错库」的第一道防线）────────────────────
# 配置只有一处来源：Admin.NET.Application/Configuration/Database.json（只有 PostgreSQL）。
step "2/6 确认数据库配置来源"
CFG_SRC="$REPO/Admin.NET/Admin.NET.Application/Configuration"
CFG_OUT="$ENTRY_DIR/bin/Debug/net8.0/Configuration"
[[ -f "$CFG_SRC/Database.json" ]] \
  || die "缺少 $CFG_SRC/Database.json —— 它是连库的唯一来源（DbType/ConnectionString）"
# db_target.py 的人类可读输出：来源文件 + 「bin/ 副本是否与源一致」。
# ★ Furion 读的是 bin/ 里的副本：源文件改了没重新 build = 改了不生效，且**不报错**。
"$PY" "$REPO/scripts/db_target.py"
echo "（构建时由 Admin.NET.Application.csproj 把 Configuration\\**\\* 复制到 bin/；"
echo "  第二道防线是启动后的日志断言）"

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
#   直接跑 apphost 则是**单进程**，pid 就是宿主本身，cwd 也确定（= 项目目录，决定 logs/ 落在哪）。
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

echo "等待端口就绪（上限 ${READY_MAX}s；首次对空库跑 CodeFirst 要建 45 张表，可能要几分钟）"
READY=0
# ★ 循环变量**不要用 `_`**：`$_` 是 bash 的特殊变量（上一条命令的最后一个参数），
#   而 `port_listening` 是函数调用 → 会把 `$_` 覆盖成函数名，
#   于是输出变成「已就绪（第 port_listening 秒）」。实测踩过。
for sec in $(seq 1 "$READY_MAX"); do
  if port_listening; then echo " —— 已就绪（第 ${sec} 秒）"; READY=1; break; fi
  # ★ 进度可见：CodeFirst 是逐张建表，远端库每张要几秒到二十几秒。
  #   不报进度时，「在建表」和「卡死」在终端上长得一模一样。
  #   用「数行数」而不是「取最后一行」：本沙箱对日志**禁用 tail**，而 grep 正常。
  if (( sec % 15 == 0 )); then
    BUILT="$(grep -ac '初始化表 ' "$LOG_FILE" 2>/dev/null || true)"
    printf '\n  …等待中 %ss，已建表 %s/45\n' "$sec" "${BUILT:-0}"
  fi
  echo -n "."; sleep 1
done
if [[ "$READY" != "1" ]]; then
  echo
  echo "✗ 后端未在 ${READY_MAX}s 内就绪。最后 30 行日志（口令已打码）：" >&2
  show_log_tail "$LOG_FILE" 30
  die "后端未就绪（日志 $LOG_FILE）"
fi

# ★ 数据库到底是哪个？启动日志里有一行「初始化数据库 xxx」—— 直接断言，不靠猜。
#   这是「静默回落」的第二道防线：配置写错时后端**不报错**，只是安静地连上另一个库，
#   业务代码于是在错误的数据上跑（最坏情况是跑在空库上，看起来「功能没实现」）。
#   宁可在这里拒绝继续，也不要让后续的 schema 纠偏与守卫在错误的库上「通过」。
DB_LINE="$(grep -am1 '初始化数据库' "$LOG_FILE" || true)"
DB_LINE_SAFE="$(printf '%s' "$DB_LINE" | mask_secret)"
echo "数据库：${DB_LINE_SAFE:-（日志里没有「初始化数据库」这一行）}"
if [[ "$DB_LINE" != *PostgreSQL* ]]; then
  echo
  echo "✗ 后端连的不是 PostgreSQL（日志：${DB_LINE_SAFE:-无}）" >&2
  echo "  本项目只有 PG：额度预占依赖 FOR UPDATE 行锁，其它库没有该语义。" >&2
  echo "  排查：" >&2
  echo "    1) 改 $CFG_SRC/Database.json 的 DbType / ConnectionString" >&2
  echo "    2) 改完**必须重新 build**（Furion 读的是 bin/ 里的副本，不是源文件）" >&2
  die "数据库不是 PostgreSQL —— 拒绝继续"
fi

# ── 5. schema 纠偏（幂等；必须在 CodeFirst 建表之后）────────────────────────
step "5/6 应用 schema 契约与列宽纠偏"
echo "目标：$PAY_DB_DESC"
# ★ 不再用 `docker exec psql`：那只能打在本地容器上，而目标库可能是远端 ——
#   一旦不一致，纠偏就作用在「后端不用的那个库」上（静默失效）。
#   改由 psycopg2 执行（该文件是纯 SQL、无 psql 元命令），本地/远端一视同仁。
"$PY" - "$REPO/scripts/paycenter-schema.sql" <<'PYEOF' || die "paycenter-schema.sql 执行失败"
import os
import sys

import psycopg2

sql = open(sys.argv[1], encoding="utf-8").read()
kwargs = dict(
    host=os.environ["PAY_PG_HOST"],
    port=int(os.environ["PAY_PG_PORT"]),
    user=os.environ["PAY_PG_USER"],
    dbname=os.environ["PAY_PG_DB"],
)
if os.environ.get("PAY_PG_PASSWORD"):
    kwargs["password"] = os.environ["PAY_PG_PASSWORD"]
conn = psycopg2.connect(**kwargs)
conn.autocommit = True
try:
    with conn.cursor() as cur:
        cur.execute(sql)
finally:
    conn.close()
print("paycenter-schema.sql 已应用（幂等，可反复执行）")
PYEOF

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
 数据库    ${PAY_DB_DESC}
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
