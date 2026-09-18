// 用 @vue/compiler-sfc 校验改动过的 .vue 文件语法（单文件级，秒级返回）
//
// ★ 定位（2026-09-18 更新）：本项目**已有**完整类型检查 —— `npm run typecheck`
//   （= `node script/check-types.mjs`，见该文件顶部说明）。所以本脚本**不是**主力手段，
//   它只覆盖「语法/模板编译」这一层，保留的理由是：
//     · 只改了一两个文件时，比跑整轮 vue-tsc 快得多；
//     · 类型检查通过**不等于**模板能编译（v-xxx 用法、指令语法等仍可能报错）。
//   ★ 它**不做**类型检查，查不出「调错 API 方法名 / 用错模型字段名」—— 那是 check-types.mjs 的职责。
//
// 用法：node script/check-sfc.cjs <file.vue> [file2.vue ...]
const fs = require('fs');
const path = require('path');
const { parse, compileTemplate, compileScript } = require('@vue/compiler-sfc');

const FILES = process.argv.slice(2);
let bad = 0;

for (const f of FILES) {
  const id = path.basename(f);
  // ★ 只接受 .vue：喂 .ts 进去会得到 "Element is missing end tag" 这种**完全误导**的报错
  //   （SFC 解析器把整个 ts 文件当成模板）。.ts 的类型问题交给 `npm run typecheck`。
  if (!f.endsWith('.vue')) {
    console.log(`– ${id}  跳过（本脚本只校验 .vue；.ts 请跑 npm run typecheck）`);
    continue;
  }
  const src = fs.readFileSync(f, 'utf8');
  try {
    const { descriptor, errors } = parse(src, { filename: id });
    if (errors.length) {
      console.log(`✗ ${id}  parse 错误：`);
      errors.forEach((e) => console.log('   ', e.message));
      bad++;
      continue;
    }
    const hasScriptSetup = !!descriptor.scriptSetup;
    if (hasScriptSetup) {
      compileScript(descriptor, { id });
    }
    if (descriptor.template) {
      const r = compileTemplate({
        source: descriptor.template.content,
        filename: id,
        id,
        compilerOptions: { bindingMetadata: hasScriptSetup ? compileScript(descriptor, { id }).bindings : undefined },
      });
      if (r.errors.length) {
        console.log(`✗ ${id}  template 错误：`);
        r.errors.forEach((e) => console.log('   ', typeof e === 'string' ? e : e.message));
        bad++;
        continue;
      }
    }
    console.log(`✓ ${id}  语法通过`);
  } catch (e) {
    console.log(`✗ ${id}  异常：${e.message}`);
    bad++;
  }
}
console.log(bad === 0 ? '\n全部通过' : `\n失败 ${bad} 个`);
process.exit(bad === 0 ? 0 : 1);
