#!/usr/bin/env bash
# 一键跑完 pay_* 的全部 HTTP 回归
#
#   ① pay_allocate_stress.py      F2 额度预占并发压测（不超发）
#   ② pay_notify_regression.py    F4 到账通知（幂等 / 部分到账 / 超额 / 异常台账）
#   ③ pay_openaccess_gate_probe.py 凭证停用即失效（走后台接口改状态、不重启）
#   ④ s6_admin_probe.py           S6 后台管理 + 4 类导出 + 导出留痕
#
# 用法：
#   scripts/regress-all.sh                 # 后端已在跑、且验证码已关；前置不满足则**拒绝并退出 2**
#   scripts/regress-all.sh --with-login    # 自己把整条链路做完（见下），含还原
#
# ── 为什么需要这个脚本 ───────────────────────────────────────────────────────
# 这 4 个脚本此前是**手工一个个跑**的，没有统一入口 —— 于是「这轮跑全了没有」
# 只能靠人记，而漏跑一个不会有任何提示（本项目最怕的就是静默）。
#
# ★ 为什么必须**串行**：s6_admin_probe.py 的导出留痕断言要按「运行时间窗」核对与清理
#   （导出审计行不带测试标识，只能按 createtime 卡窗口，见 PROJECT-NOTES §7.1）。
#   并行跑会互相误伤时间窗 —— 所以这里**故意不并行**，即使它看起来可以。
#
# ★ 为什么验证码要先关：4 个脚本都要登录后台，而租户图形验证码默认开启。
#   关它必须改**种子**再重启（直接 UPDATE 库无效：JwtHandler 读的是租户缓存），
#   所以这一步没法在脚本里「顺手」做 —— 它需要重建 + 重启。
#   --with-login 会把整条链路做完，并用 trap 保证**无论怎么退出都会还原**；
#   还原后 dev-up.sh 的守卫 §7n 会断言 Captcha 已回到 true（漏还原会被它抓住）。
#
# 退出码：0 = 全部通过；1 = 有脚本失败；2 = 前置条件不满足（**不是**通过）

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PY="${PY:-$HOME/.workbuddy-ai/binaries/python/envs/default/bin/python}"
DOTNET="${DOTNET:-/usr/local/share/dotnet/dotnet}"
PORT="${PORT:-5005}"
SEED="$REPO/Admin.NET/Admin.NET.Application/SeedData/SysTenantSeedData.cs"
GUARD="$REPO/scripts/pay_schema_guard.py"

WITH_LOGIN=0
for arg in "$@"; do
  case "$arg" in
    --with-login) WITH_LOGIN=1 ;;
    *) echo "未知参数：$arg" >&2; exit 2 ;;
  esac
done

step() { printf '\n\033[1m==> %s\033[0m\n' "$1"; }
die()  { printf '\033[31m✗ %s\033[0m\n' "$1" >&2; exit 2; }

SCRIPTS=(
  pay_allocate_stress.py
  pay_notify_regression.py
  pay_openaccess_gate_probe.py
  s6_admin_probe.py
)

# ── 前置判断 ─────────────────────────────────────────────────────────────────
# ★ 端口探活**不能用 curl**：实测本沙箱 curl 对没有任何进程监听的端口也返回退出码 0，
#   用它判断会无条件「通过」。lsof 可靠（有监听 2 行 / 无监听 0 行）；拿不到就 fail-closed。
port_listening() {
  if command -v lsof >/dev/null 2>&1; then
    [[ -n "$(lsof -nP -iTCP:"$PORT" -sTCP:LISTEN 2>/dev/null)" ]]
  elif command -v ss >/dev/null 2>&1; then
    [[ -n "$(ss -ltnH "sport = :$PORT" 2>/dev/null)" ]]
  else
    return 1
  fi
}

# ★ 注意这里用的是**单模式** grep。本沙箱的 grep 是 toybox（非 GNU），
#   **不支持 BRE 交替 `\|`** —— 写成 `grep "a\|b"` 会静默返回 0 匹配。
#   需要多模式请用 `grep -E "a|b"`。见 PROJECT-NOTES §8.9。
captcha_on() { grep -q 'Captcha=true,' "$SEED"; }

set_captcha() {  # on | off
  local want="$1" from to
  if [[ "$want" == "on" ]]; then from='Captcha=false,'; to='Captcha=true,';
  else                            from='Captcha=true,';  to='Captcha=false,'; fi
  "$PY" - "$SEED" "$from" "$to" <<'PYEOF'
import pathlib, sys
p = pathlib.Path(sys.argv[1]); s = p.read_text(encoding="utf-8")
assert s.count(sys.argv[2]) == 1, f"种子里的 {sys.argv[2]!r} 命中 {s.count(sys.argv[2])} 次，预期 1"
p.write_text(s.replace(sys.argv[2], sys.argv[3]), encoding="utf-8")
PYEOF
}

build() { (cd "$REPO/Admin.NET" && "$DOTNET" build Admin.NET.sln --nologo -v q); }

restart_backend() {  # $1 = 传给 dev-up.sh 的额外参数（可为空）
  bash "$REPO/scripts/dev-down.sh" >/dev/null 2>&1 || true
  bash "$REPO/scripts/dev-up.sh" ${1:-}
}

# ── 还原（无论怎么退出都要跑）────────────────────────────────────────────────
RESTORE_NEEDED=0
restore() {
  local rc=$?
  if [[ "$RESTORE_NEEDED" == "1" ]]; then
    printf '\n\033[1m==> 还原：把租户验证码种子改回 true 并重启\033[0m\n'
    set_captcha on
    build || echo "✗ 还原时构建失败 —— 请手工确认种子已回 true" >&2
    restart_backend "" || echo "✗ 还原时重启失败 —— 请手工重启" >&2
    # 守卫 §7n 会断言 Captcha=true：跑一次，等于**验证还原真的生效了**
    "$PY" "$GUARD" >/dev/null 2>&1 \
      && echo "✓ 还原已验证（守卫 §7n 通过：Captcha 已回到 true）" \
      || echo "✗ 守卫未通过 —— 验证码可能没还原，请立刻检查 $SEED" >&2
  fi
  exit "$rc"
}
trap restore EXIT

# ── 前置检查 ─────────────────────────────────────────────────────────────────
step "0/3 前置检查"
[[ -x "$PY" ]] || die "找不到 python venv：${PY}（只有它装了 psycopg2）"
[[ -f "$SEED" ]] || die "找不到种子文件：$SEED"
[[ -f "$GUARD" ]] || die "找不到守卫：$GUARD"

if [[ "$WITH_LOGIN" == "1" ]]; then
  step "1/3 关验证码 → 构建 → 重启（用 --no-guard：此刻守卫 §7n 必然不通过）"
  # ★ 顺序很重要：**先**置 RESTORE_NEEDED，再动种子。
  #   反过来的话，若在「已改种子」与「置标志」之间被打断，trap 会以为无需还原 →
  #   验证码就**永久留在关闭状态**（而它是个安全控制）。
  RESTORE_NEEDED=1
  set_captcha off
  build || die "构建失败"
  restart_backend "--no-guard" || die "重启失败"
else
  step "1/3 前置条件（后端在跑 + 验证码已关）"
  port_listening || die "后端没在 :$PORT 上跑。先执行：scripts/dev-up.sh（脚本不会替你起，避免与已有进程打架）"
  if captcha_on; then
    cat >&2 <<EOF
✗ 租户图形验证码是**开启**的，4 个脚本都登录不了后台。

  两种做法：
    a) 让本脚本代劳（含自动还原）：
         scripts/regress-all.sh --with-login
    b) 手工（注意**必须**改种子再重启，直接 UPDATE 库无效）：
         把 $SEED 的 Captcha=true, 改成 Captcha=false,
         → 构建 → scripts/dev-down.sh → scripts/dev-up.sh --no-guard
         → 跑完**务必改回 true** 再重启（守卫 §7n 会断言，漏了会被抓住）
EOF
    exit 2
  fi
  echo "后端在跑，验证码已关 —— 继续"
fi

# ── 跑 ───────────────────────────────────────────────────────────────────────
step "2/3 串行执行 ${#SCRIPTS[@]} 个回归脚本"
declare -a NAMES=() RCS=()
FAILED=0
for s in "${SCRIPTS[@]}"; do
  echo
  echo "──────────────────────────────────────────────────────────────"
  echo "▶ $s"
  echo "──────────────────────────────────────────────────────────────"
  rc=0
  "$PY" "$REPO/scripts/$s" || rc=$?
  NAMES+=("$s"); RCS+=("$rc")
  [[ "$rc" == "0" ]] || FAILED=1
done

# ── 汇总 ─────────────────────────────────────────────────────────────────────
step "3/3 汇总"
printf '  %-34s %s\n' "脚本" "结果"
for i in "${!NAMES[@]}"; do
  if [[ "${RCS[$i]}" == "0" ]]; then
    printf '  %-34s \033[32m通过\033[0m\n' "${NAMES[$i]}"
  else
    printf '  %-34s \033[31m失败（退出码 %s）\033[0m\n' "${NAMES[$i]}" "${RCS[$i]}"
  fi
done
echo
if [[ "$FAILED" == "1" ]]; then
  echo "✗ 存在失败项 —— 不要把它们当噪声，每个脚本失败都对应一条真实断言" >&2
  exit 1
fi
echo "✓ 全部通过（${#SCRIPTS[@]}/${#SCRIPTS[@]}）"
exit 0
