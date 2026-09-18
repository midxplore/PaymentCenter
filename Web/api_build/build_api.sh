#!/bin/sh
# 选一个 Swagger 地址，然后**委托给 build.sh** 生成。
#
# 为什么改成委托：原来本脚本把「java + 三个 URL 分支 + 清理」抄了三份，
# 于是每次改动（例如加 -t 自定义模板）都要改四处、漏一处就静默不一致。
# 现在生成逻辑只有 build.sh 一份，这里只负责选地址。
#
# 用法：npm run buildApi _api   （或直接 ./build_api.sh）

url1="http://172.18.32.33:5050"
url2="http://127.0.0.1:5050"
url3="http://localhost:5005"

echo "请选择Swagger地址:"
echo "(1) $url1"
echo "(2) $url2"
echo "(3) $url3"
printf '%s' "请输入选项 [1-3]: "
read choice

currPath=$(pwd)

case $choice in
    1) base=$url1 ;;
    2) base=$url2 ;;
    3) base=$url3 ;;
    *) echo "无效的选项，请输入[1-3]。" >&2; exit 1 ;;
esac

echo "您选择了: $base"

exec "${currPath}/build.sh" "$base/swagger/All%20Groups/swagger.json"
