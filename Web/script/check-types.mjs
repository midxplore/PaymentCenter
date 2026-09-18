#!/usr/bin/env node
/**
 * 前端类型检查闸门（`npm run typecheck`）
 * ────────────────────────────────────────────────────────────────────────────
 * 为什么需要它，而不是直接跑 `vue-tsc --noEmit`：
 *
 *   本工程**从未**做过类型检查（没装 vue-tsc），首次接入时有 458 个既有错误。
 *   其中绝大多数在**不是我们写的**代码里 —— Swagger 生成的 API 客户端、框架自带
 *   的布局/组件/页面。直接跑 `vue-tsc` 只会淹没真正属于本模块的那几个错误，
 *   于是「装了类型检查」这件事会退化成「大家都不看它」。
 *
 *   目前 458 → 134（生成物 184→0 已提升为闸门；全局类型未生效导致的 60 条已修，
 *   见下方「守卫 1/2」；开放接口凭证界面已修到 0 并纳入闸门）。
 *   剩下的都在框架自带代码里，逐条登记在 KNOWN_DEBT。
 *
 * 所以这个脚本把结果分成三桶：
 *
 *   · 闸门（GATE_PREFIXES）—— 我们自己的代码，**必须 0 错**，否则退出码非 0。
 *   · 既有债务（KNOWN_DEBT）—— 逐条写明为什么不在这里修，并**始终打印计数**，
 *     避免它悄悄变大（债务可以存在，但不能不受监控地增长）。
 *   · 未归类（other）—— 谁都没认领的路径，**同样让闸门失败**：否则它的错误会从统计里消失，
 *     必须由人显式做一次归属决策（修掉 / 进闸门 / 进债务表并写原因）。
 *
 * 想直接看全部错误：`npm run typecheck:all`（= `node script/check-types.mjs --all`）
 *
 * ★ 为什么 `typecheck:all` 也走这个脚本、而不是在 package.json 里直接写 `vue-tsc --noEmit`：
 *   本机/本宿主会给子进程注入 `NODE_OPTIONS=--require=.../node-language-shim.cjs`，
 *   npm/npx 加载那个 shim 会直接崩。脚本里已统一把 NODE_OPTIONS 剥掉再 spawn，
 *   两条命令因此共享同一份环境处理，也不会把宿主特有的环境问题带进 CI。
 *
 * 为什么闸门圈这几个前缀（`src/views/paycenter/`、`src/api-services/`、`src/views/system/openAccess/`）：
 *   这正是「构建期完全发现不了」的那一类缺口 —— 页面里调错 API 方法名、
 *   用错模型字段名、对可空字段不做判空。`vite build` 只校验 import 路径，
 *   不校验对象成员，所以这些错一直要到运行时才暴露。
 *
 *   ★ `src/views/system/openAccess/` 是**补漏**加上去的：开放接口凭证管理
 *   （密钥脱敏、停用开关、scopes 编辑）是我们自己写的，但目录位置由框架路由决定，
 *   落在 `src/views/system/` 下 → 被 `{ prefix: 'src/views/' }` 那条债务整段吞掉。
 *   也就是说：**本项目最敏感的那张表（密钥）的界面，此前从未被检查过。**
 */

import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const WEB_DIR = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

/** ★ 我们自己的代码：必须 0 错 */
const GATE_PREFIXES = [
	'src/views/paycenter/',
	// ★ 生成物现在是 0 错（模板层 + 生成后处理，见 Web/api_build/templates/README.md），
	//   所以从「已登记债务」提升为**硬闸门**：一旦回退 —— 升级 swagger-codegen 后忘了重新 fork 模板、
	//   或生成脚本里的 `-t` / 自引用清理被删掉 —— 闸门会**直接失败**，
	//   而不是安静地打印一个数字（那正是「债务悄悄涨回去」的形态）。
	'src/api-services/',
	// ★★ 这条是**补漏**：开放接口凭证界面是我们自己写的，但目录由框架路由决定，
	//    落在 system/ 下 → 被下面的 `{ prefix: 'src/views/' }` 整段吞掉（原因见文件头注释）。
	//    现已修到 0 错并纳入闸门。将来在 system/ 下新增**自己的**页面，记得同样加进来。
	'src/views/system/openAccess/',
];

/** 允许带债的路径。每条都要写清「为什么不在这里修」。 */
const KNOWN_DEBT = [
	{ prefix: 'src/components/', why: '框架自带组件（116 条里 113 条集中在 dragVerify/ 的 3 个未被引用文件，见下）' },
	{ prefix: 'src/layout/', why: '框架自带布局' },
	{ prefix: 'src/utils/', why: '框架自带工具' },
	{ prefix: 'src/hooks/', why: '框架自带 hooks' },
	{ prefix: 'src/i18n/', why: '框架自带国际化（索引签名缺失一类）' },
	{ prefix: 'src/router/', why: '框架自带路由' },
	{ prefix: 'src/stores/', why: '框架自带 store' },
	{ prefix: 'src/main.ts', why: '框架自带入口（第三方包缺 .d.ts 声明）' },
	{ prefix: 'src/views/', why: '框架自带页面（system / login / home / about）—— 注意 openAccess/ 已被上面单独认领，不在此列' },
];

// ★ src/components/ 那 116 条里有 113 条集中在 src/components/dragVerify/ 的
//   dragVerify.vue (30) / dragVerifyImg.vue (32) / dragVerifyImgChip.vue (51) 三个文件上。
//   ★★ 只有**这三个**是死代码 —— 同目录的 dragVerifyImgRotate.vue 被
//      views/login/component/account.vue 引用，**不能删**（那个文件已修到 0 错）。
//      一句话概括成「dragVerify 是死代码」会诱导人删掉整个目录、把登录页弄坏。
//   这三个文件在**本项目里没有任何引用**，属于上游框架的死代码。
//   没有删掉它们，是因为删除需要用户显式确认（删掉后这一桶从 116 掉到 3 条，
//   剩 jsonEditor 的 3 条，同样零引用）。详见 .workbuddy-ai/memory 当日日志。
//
// ★ 已知**修不掉**的一条（不是没修，是修了更差）：
//   `src/components/scEcharts/index.vue` 的 `<script>` 没有 `lang="ts"`，
//   于是 5 个框架页面 import 它时报 TS7016（模块退化成 any）。给它加 `lang="ts"`
//   会让这个 75 行、到处 `this.$refs.X` / 隐式 any 的 Options API 组件自己冒出更多错，
//   净收益为负。保持 any 也无风险：调用方拿到的本来就是 any，不会产生「假的安全感」。
//
// ★ tsconfig 已从 `moduleResolution: "node"`（node10，已废弃）改为 `"bundler"`，
//   让 TS 的解析方式与 Vite 一致。顺带修掉 `relation-graph/vue3` 的 2 条 TS2307。
//   代价是 `@wangeditor/editor-for-vue` 因上游 `exports` 缺 types 条件而报 TS7016，
//   已在 tsconfig `paths` 里把它指到真实 .d.ts（**保留真类型**，不是 declare module 退化成 any）。

// ★ 为什么不干脆写一条 `src/` 通吃：那样 `other` 桶永远是空的，
//   将来谁在 src/ 下新建一个目录、写进一堆类型错误，都会被默默算成「既有债务」。
//   显式逐条列出，才能让「出现新路径」这件事重新变成告警。

const classify = (file) => {
	if (GATE_PREFIXES.some((p) => file.startsWith(p))) return 'gate';
	if (KNOWN_DEBT.some((d) => file.startsWith(d.prefix))) return 'debt';
	return 'other';
};

// ── 错误行解析 ──────────────────────────────────────────────────────────────
// 形如：src/views/paycenter/order/index.vue(244,17): error TS2352: ...
//
// ★★ 这里的字符类**必须**同时排除换行与括号，且首字符必须非空白。
//    vue-tsc 的报错消息是**多行**的，例如
//      src/components/x.vue(194,23): error TS7053: Element implicitly ... type 'A'.
//                No index signature with a parameter of type 'string' ...
//      src/components/x.vue(196,24): error TS7053: Element ...
//    续行本身不含 ": error TS"，但 `[^(]` **是能匹配换行符的**，
//    若 file 组写成 `[^(]+`，正则就会从续行开头一路吞到**下一行的真实路径**，
//    file 于是变成 "          No index signature ...\nsrc/components/x.vue"。
//
//    危害不是排版难看：`classify()` 用 startsWith 判断，被污染的名字永远落到 'other' 桶
//    —— 也就是说**本模块的错误会被算成「未归类」而放行**，闸门静默失效。
//    实测踩到过：451 条里 30 条被这样吞掉（dragVerify.vue 真实的 3 条只剩 1 条）。
//    首字符非空白 + 不跨行 + 不含括号，三个约束缺一不可。
const ERR_RE = /^(?<file>[^\s(][^\n(]*)\((?<line>\d+),(?<col>\d+)\): error (?<code>TS\d+): (?<msg>.*)$/gm;

/**
 * 量具自检：在**测量之前**先验证解析器本身。
 * 用一个合成的「多行消息 + 紧跟一条本模块错误」样本跑一遍，确认本模块错误仍被归到闸门。
 * 这段样本正是旧正则漏判的真实形态；一旦有人把 ERR_RE「简化」回去，这里立刻拦下。
 */
const selfTestParser = () => {
	const sample = [
		`src/components/x.vue(194,23): error TS7053: Element implicitly has an any type because expression of type string can not be used to index type { isMoving: boolean }.`,
		`          No index signature with a parameter of type 'string' was found on type '{ isMoving: boolean }'.`,
		`src/views/paycenter/order/index.vue(244,17): error TS2352: Conversion of type A to type B may be a mistake.`,
	].join('\n');

	const found = [...sample.matchAll(ERR_RE)].map((m) => m.groups);
	const gate = found.filter((e) => classify(e.file) === 'gate');
	const bad = found.filter((e) => /\s/.test(e.file));

	if (found.length !== 2 || gate.length !== 1 || bad.length > 0) {
		console.error('');
		console.error('✗ 解析器自检失败：ERR_RE 无法正确切分多行报错。');
		console.error(`  期望 命中 2 条 / 闸门 1 条 / 畸形 0 条，实际 命中 ${found.length} / 闸门 ${gate.length} / 畸形 ${bad.length}。`);
		console.error('  解析结果不可信，闸门拒绝给出结论（见 script/check-types.mjs 中 ERR_RE 上方注释）。');
		process.exit(2);
	}
};

selfTestParser();

// ── 守卫 1：全局类型声明文件必须保持「脚本」形态 ──────────────────────────────
// src/types/global.d.ts 声明了 RouteItem / RouteItems / RouteToFrom / RefType /
// HtmlType / ChilType / EmptyArrayType / EmptyObjectType / Nullable* / Window 增强 …
//
// ★ 但它**只要出现顶层 import/export 就会退化成「模块」**。模块里的 `declare` 只在本
//   模块内可见，于是上面这些名字全项目都「找不到」—— 实测一次回归就是：
//     53 条 TS2304（RouteItem 24 / EmptyObjectType 7 / RouteItems 6 / RouteToFrom 4 /
//                    RefType 4 / EmptyArrayType 4 / HtmlType 2 / ChilType 1 / Nullable 1）
//     + 1 条 TS2339（`__env__` 不存在于 Window —— 因为 declare interface Window 同样失效）
//     + 连带 6 条假 TS7006（导入失败的绑定退化成 any，`any.filter((v)=>…)` 没有上下文类型）
//
// ★ 为什么必须单独守：闸门只圈 `src/views/paycenter/` 与 `src/api-services/`，
//   这 60 条全落在**债务桶**里 —— 闸门**不会失败**，只会让债务从 164 悄悄涨回 224。
//   这正是「债务可以存在，但不能不受监控地增长」要防的形态。
//
// 判定用「行首（第 0 列）出现 import/export」这个保守启发式：真正让文件变模块的是
// **文件顶层**的 import/export；而 `declare module '*.vue' { import type ... }` 这类
// 缩进在块内的 import 不算。宁可漏判（还有守卫 2 兜底），也不要误判。
const GLOBAL_DTS = path.join(WEB_DIR, 'src', 'types', 'global.d.ts');

/** 去掉注释，避免把注释里的示例代码当成真代码 */
const stripComments = (s) => s.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^[ \t]*\/\/.*$/gm, '');

const globalDtsSource = (() => {
	try {
		return stripComments(readFileSync(GLOBAL_DTS, 'utf8'));
	} catch (e) {
		console.error(`✗ 无法读取 ${GLOBAL_DTS}：${e.message}`);
		process.exit(2);
	}
})();

const assertGlobalDtsIsScript = () => {
	const bad = [...globalDtsSource.matchAll(/^(import|export)\b.*$/gm)].map((m) => m[0].trim());
	if (bad.length === 0) return;
	console.error('');
	console.error('✗ 全局类型声明文件退化为「模块」：src/types/global.d.ts 出现顶层 import/export。');
	for (const b of bad) console.error(`      ${b}`);
	console.error('  后果：该文件里所有 declare type / declare interface 只在本文件内可见，');
	console.error('        全项目会报一堆 TS2304「找不到名字」+ Window 增强失效（约 60 条）。');
	console.error('  修法：把需要 import 的模块增强（declare module \'vue\' 等）挪到');
	console.error('        src/types/module-augment.d.ts，让 global.d.ts 保持无顶层 import/export。');
	process.exit(1);
};

assertGlobalDtsIsScript();

/** global.d.ts 里声明的全局名字，用于守卫 2 */
const declaredGlobalNames = () => {
	const names = new Set();
	const re = /^\s*declare\s+(?:type|interface|class|const|let|var|function)\s+([A-Za-z_$][\w$]*)/gm;
	for (const m of globalDtsSource.matchAll(re)) names.add(m[1]);
	return names;
};

const GLOBAL_NAMES = declaredGlobalNames();
if (GLOBAL_NAMES.size < 10) {
	// 抽不出名字说明守卫 2 本身失效了，必须让闸门拒绝给结论，而不是「0 条 = 通过」
	console.error(`✗ 守卫自检失败：从 global.d.ts 只解析出 ${GLOBAL_NAMES.size} 个全局名字，预期 ≥10。`);
	process.exit(2);
}

// ── 跑 vue-tsc ──────────────────────────────────────────────────────────────
const bin = path.join(WEB_DIR, 'node_modules', '.bin', 'vue-tsc');
const env = { ...process.env };
// Vite/Node 的 NODE_OPTIONS 会干扰子进程，剥掉
delete env.NODE_OPTIONS;

const res = spawnSync(bin, ['--noEmit'], { cwd: WEB_DIR, env, encoding: 'utf8' });
if (res.error) {
	console.error(`✗ 无法执行 vue-tsc：${res.error.message}`);
	console.error('  先安装：cd Web && npm i -D vue-tsc --cache ./.npm-cache');
	process.exit(2);
}

const output = `${res.stdout ?? ''}${res.stderr ?? ''}`;

// ── `--all`：原样输出 vue-tsc 结果，不做闸门/债务分类 ────────────────────────
if (process.argv.includes('--all')) {
	process.stdout.write(output);
	const n = [...output.matchAll(/: error TS\d+:/g)].length;
	console.log(`\n（全部 ${n} 条类型错误）`);
	process.exit(res.status ?? 0);
}

// ── 解析 ────────────────────────────────────────────────────────────────────
const buckets = { gate: [], debt: [], other: [] };
for (const m of output.matchAll(ERR_RE)) {
	const { file, line, code, msg } = m.groups;
	buckets[classify(file)].push({ file, line, code, msg });
}

// ── 解析结果自检 ────────────────────────────────────────────────────────────
// ★ 校验「解析出的路径里不含空白或换行」这个**精确不变式**。
//   一开始这里写的是「命中数必须等于 `: error TS` 标记数」，但实测**拦不住**跨行吞并：
//   被污染的那条匹配里仍然恰好含一个标记，两边计数都是 451，看起来完全正常。
//   而污染后的名字一定带前导空格与换行，所以直接查这个特征才有效。
const malformed = [...buckets.gate, ...buckets.debt, ...buckets.other].filter(
	(e) => /\s/.test(e.file) || e.file.length === 0
);
if (malformed.length > 0) {
	console.error('');
	console.error(`✗ 解析结果自检失败：${malformed.length} 条错误的"文件名"含空白或换行。`);
	console.error('  归类结果不可信（本模块的错误可能被算成"未归类"而放行），闸门拒绝给出结论。');
	for (const e of malformed.slice(0, 3)) {
		console.error(`    实际路径: ${JSON.stringify(e.file.slice(0, 70))}…`);
	}
	process.exit(2);
}

const total = buckets.gate.length + buckets.debt.length + buckets.other.length;

// ── 守卫 2：全局类型必须真的「可见」 ──────────────────────────────────────────
// 守卫 1 查的是「文件形态」这个**机制**；这里查的是**症状**，与机制无关：
// 只要 vue-tsc 报「Cannot find name 'X'」而 X 是 global.d.ts 声明过的名字，
// 就说明全局类型没生效 —— 不管是文件退化成模块、tsconfig 的 include 被改、
// 还是别的原因。查症状比猜机制更不容易漏。
//
// ★ 这个判定**必须**在三个桶里一起找，而不是只看 debt 桶：
//   这些错误按路径本来就会落进 debt，所以「只看闸门桶」永远发现不了。
{
	const missing = [...buckets.gate, ...buckets.debt, ...buckets.other].filter((e) => {
		if (e.code !== 'TS2304') return false;
		const m = e.msg.match(/Cannot find name '([^']+)'/);
		return m && GLOBAL_NAMES.has(m[1]);
	});
	if (missing.length > 0) {
		console.error('');
		console.error(`✗ 全局类型未生效：${missing.length} 处「找不到名字」，而这些名字在 src/types/global.d.ts 里有声明。`);
		for (const e of missing.slice(0, 8)) {
			console.error(`      ${e.file}(${e.line}): ${e.code} ${e.msg}`);
		}
		if (missing.length > 8) console.error(`      …另有 ${missing.length - 8} 处`);
		console.error('  最常见原因：global.d.ts 顶层出现了 import/export，退化成模块（见守卫 1）。');
		console.error('  另一可能是 tsconfig.json 的 include 不再覆盖 src/**/*.d.ts。');
		process.exit(1);
	}
}

// ── 报告 ────────────────────────────────────────────────────────────────────
const line = (s) => console.log(s);

if (buckets.gate.length > 0) {
	line('');
	line('══ 本模块类型错误（必须修）' + '═'.repeat(50));
	for (const e of buckets.gate) {
		line(`  ✗ ${e.file}(${e.line}): ${e.code} ${e.msg}`);
	}
}

line('');
line('══ 既有债务（本次不阻断，但请勿增长）' + '═'.repeat(34));
// ★ 全部列出（含 0 条）：这张表同时是**基线快照**。
//   只打印非零项的话，「某块债被清掉了」和「某块债被写进了别的桶」看起来一样。
for (const d of KNOWN_DEBT) {
	const n = buckets.debt.filter((e) => e.file.startsWith(d.prefix)).length;
	line(`  ${String(n).padStart(4)} 条  ${d.prefix.padEnd(22)} ${d.why}`);
}
const otherDebt = buckets.other.length;
if (otherDebt > 0) {
	line('');
	line('══ 未归类路径（必须做归属决策）' + '═'.repeat(44));
	for (const e of buckets.other) line(`  ? ${e.file}(${e.line}): ${e.code} ${e.msg}`);
}

line('');
line('─'.repeat(74));
line(`本模块 ${buckets.gate.length} 条 ／ 既有债务 ${buckets.debt.length} 条 ／ 未归类 ${otherDebt} 条 ／ 合计 ${total} 条`);

if (buckets.gate.length > 0) {
	line('');
	line('✗ 闸门未通过：本模块有类型错误（见上方第一条）。');
	process.exit(1);
}

// ★ 未归类路径也让闸门失败。理由：那意味着 src/ 下出现了没人认领的代码，
//   它的错误既不在闸门里、也不在债务表里 —— 等于**从统计中消失**。
//   处理方式只有三种，都必须由人显式做一次：修掉 / 进 GATE_PREFIXES / 进 KNOWN_DEBT 并写清原因。
//   （现在这个桶是空的；一旦非空，说明有人新增了目录却没做归属决策。）
if (otherDebt > 0) {
	line('');
	line('✗ 闸门未通过：存在未归类路径，请为它们补一条归属决策（见 script/check-types.mjs 顶部注释）。');
	process.exit(1);
}

line('✓ 闸门通过：本模块 0 类型错误，且无未归类路径。');
if (total > 0) {
	line(`  （既有债务 ${total} 条不在闸门范围内；要看全部明细跑 \`npm run typecheck:all\`）`);
}
