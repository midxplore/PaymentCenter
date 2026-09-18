#!/bin/sh
# 生成前端 API 客户端（src/api-services/system）
#
# 用法：
#   ./build.sh                                     # 默认 http://localhost:5005
#   ./build.sh http://localhost:5006/swagger/All%20Groups/swagger.json
#
# 通常不直接跑，而是走 npm：
#   npm run buildApi          # → ./build.sh（默认地址）
#   npm run buildApi _api     # → ./build_api.sh（菜单选地址，再委托本脚本）
#
# ── 为什么先生成到 .tmp、成功后才替换 ★ ──────────────────────────────────────
# 原实现是「先 rm -rf 正式目录，再生成」。一旦生成失败（后端没起 / Swagger 不可达 /
# java 报错），正式目录已经没了，而脚本**仍然打印「生成结束」并以 0 退出** ——
# 结果是前端拿不到任何 api 客户端，而 npm / CI 侧看到的全是成功。
# 现在：生成到 system.tmp → 校验产物非空 → 才替换。失败时原客户端**原样保留**、退出码非 0。
#
# ── -t 自定义模板（不要删）★ ────────────────────────────────────────────────
# 指向本项目 fork 的模板目录（只放被改过的 apiInner.mustache，其余自动回落 jar 内置）。
# 补丁内容：给 localVarRequestOptions.headers 兜一个 {} —— 模板原本先声明 AxiosRequestConfig
# （headers 可选）再读 localVarRequestOptions.headers["Content-Type"]，于是 vue-tsc 报
# TS18048「可能为 undefined」，在生成物里累积成 ~175 条类型错误。补 {} 对 axios 等价、零行为变化。
# ★ 删掉 -t 会让这 ~175 条错误**原样回来**（类型检查闸门会打印计数，不会静默）。

set -e

currPath=$(pwd)
parentPath=$(dirname "$currPath")
apiRoot=${parentPath}/src/api-services
apiServicesPath=${apiRoot}/system
tmpPath=${apiRoot}/system.tmp

SWAGGER_URL="${1:-http://localhost:5005/swagger/All%20Groups/swagger.json}"

echo "================================ 生成目录 ${apiServicesPath} ================================"
echo "================================ Swagger ${SWAGGER_URL}"

# 清掉可能残留的临时目录；**不碰**正式目录
rm -rf "${tmpPath}"

echo "================================ 开始生成 ================================"

if ! java -jar "${currPath}"/swagger-codegen-cli.jar generate -i "${SWAGGER_URL}" -l typescript-axios \
  -t "${currPath}"/templates/typescript-axios -o "${tmpPath}"; then
  echo "✗ 生成失败（swagger-codegen 退出码非 0）" >&2
  echo "  常见原因：后端没起 / Swagger 地址不可达 / 该地址不是本项目的实例。" >&2
  echo "  原 ${apiServicesPath} 未做任何改动。" >&2
  rm -rf "${tmpPath}"
  exit 1
fi

# 退出码 0 不等于有产物 —— 再核一次
if [ ! -d "${tmpPath}/apis" ] || [ -z "$(ls -A "${tmpPath}/apis" 2>/dev/null)" ]; then
  echo "✗ 生成产物为空（${tmpPath}/apis 不存在或无文件），拒绝替换。" >&2
  rm -rf "${tmpPath}"
  exit 1
fi

# 清掉生成器附带、本项目不需要的文件
rm -rf "${tmpPath}/.swagger-codegen"
rm -f "${tmpPath}/.gitignore" "${tmpPath}/.npmignore" "${tmpPath}/.swagger-codegen-ignore" \
      "${tmpPath}/git_push.sh" "${tmpPath}/package.json" "${tmpPath}/README.md" "${tmpPath}/tsconfig.json"

# 删掉「自引用 import」★
# 递归模型（如 ApiOutput.children?: Array<ApiOutput>）会让生成器**在自己的文件里 import 自己**，
# 与本地声明冲突 → TS2440（共 9 条）。模板层修不掉：实测该 Handlebars 没有相等比较 helper
#   could not find helper: 'eq'
# 所以退到生成后处理。规则无歧义、与生成器版本无关：**一个文件永远不该从自己的路径 import**。
# 脚本自身会 fail-closed（异常形态退出 3、目录不存在退出 2），这里再兜一层。
if ! command -v node >/dev/null 2>&1; then
  echo "✗ 清理自引用 import 需要 node（否则会留下 9 条 TS2440）。找不到 node，拒绝继续。" >&2
  rm -rf "${tmpPath}"
  exit 1
fi
if ! node "${currPath}"/strip-self-imports.mjs "${tmpPath}/models"; then
  echo "✗ 自引用 import 清理失败，拒绝替换（原 ${apiServicesPath} 未动）。" >&2
  rm -rf "${tmpPath}"
  exit 1
fi

# 校验通过 → 替换
rm -rf "${apiServicesPath}"
mv "${tmpPath}" "${apiServicesPath}"

echo "================================ 生成结束（已替换 ${apiServicesPath}）================================"
