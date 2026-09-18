#!/usr/bin/env node
/**
 * 删除生成物里的「自引用 import」。
 *
 * 为什么需要这一步（不是偷懒，是模板层表达不出来）
 * ─────────────────────────────────────────────────────────────
 * 递归模型（如 `ApiOutput.children?: Array<ApiOutput>`）会让生成器**在自己的文件里 import 自己**：
 *
 *     // models/api-output.ts
 *     import { ApiOutput } from './api-output';     ← 自己 import 自己
 *     export interface ApiOutput { ... }
 *
 * 与本地声明冲突 → `TS2440: Import declaration conflicts with local declaration`。
 *
 * 修在模板层需要「比较 import 名与当前模型名」，而实测本生成器的 Handlebars
 * **没有相等比较 helper**：
 *
 *     could not find helper: 'eq'
 *     {{#unless (eq class ../classname)}}...
 *
 * 模板里也确实拿不到两个值以外的比较手段（`{{class}}` 与 `{{../classname}}` 都能取到，
 * 但没有运算符）。所以退到生成后处理 —— 规则是**无歧义**的：
 *
 *     一个文件**永远不该**从它自己的路径 import。
 *
 * 这条规则与生成器版本无关，也不需要理解模型语义，因此可以长期稳定。
 *
 * 用法：
 *   node strip-self-imports.mjs <models 目录>
 *   node strip-self-imports.mjs <models 目录> --check    # 只报告不修改，有自引用则退出码 1
 */

import fs from 'fs';
import path from 'path';

const args = process.argv.slice(2);
const CHECK = args.includes('--check');
const dir = args.find((a) => !a.startsWith('--'));

if (!dir) {
	console.error('✗ 用法：node strip-self-imports.mjs <models 目录> [--check]');
	process.exit(2);
}
if (!fs.existsSync(dir) || !fs.statSync(dir).isDirectory()) {
	console.error(`✗ 目录不存在：${dir}`);
	process.exit(2);
}

/** api-output → ApiOutput */
function pascalCase(kebab) {
	return kebab
		.split(/[-_]/)
		.filter(Boolean)
		.map((s) => s.charAt(0).toUpperCase() + s.slice(1))
		.join('');
}

const files = fs.readdirSync(dir).filter((f) => f.endsWith('.ts')).sort();
let changedFiles = 0;
let removedLines = 0;
let trimmedNames = 0;
const touched = [];

for (const file of files) {
	const base = file.slice(0, -'.ts'.length);
	const ownPath = `./${base}`;
	const full = path.join(dir, file);
	const lines = fs.readFileSync(full, 'utf8').split('\n');
	const out = [];
	let fileChanged = false;

	for (const line of lines) {
		// 只匹配「从自己路径 import」这一种形态
		const m = line.match(/^import\s*\{([^}]*)\}\s*from\s*'(\.\/[^']+)';\s*$/);
		if (!m || m[2] !== ownPath) {
			out.push(line);
			continue;
		}

		const names = m[1]
			.split(',')
			.map((s) => s.trim())
			.filter(Boolean);
		const self = pascalCase(base);
		const rest = names.filter((n) => n !== self);

		// ★ 安全不变式：只允许删掉「与本文件同名」的那个名字。
		//   若出现「从自己路径 import 了别的名字」这种异常形态，宁可报错也不要猜。
		const dropped = names.filter((n) => n === self);
		if (dropped.length === 0) {
			console.error(
				`✗ ${file}：从自身路径 import 了非同名标识符 ${JSON.stringify(names)}（预期含 ${self}）。` +
					`不猜测，请人工确认。`
			);
			process.exit(3);
		}

		removedLines += 1;
		fileChanged = true;
		if (rest.length > 0) {
			// 同一行还 import 了别的名字（少见）：只摘掉自引用那一个
			out.push(`import { ${rest.join(', ')} } from '${m[2]}';`);
			trimmedNames += 1;
		}
	}

	if (fileChanged) {
		changedFiles += 1;
		touched.push(file);
		if (!CHECK) fs.writeFileSync(full, out.join('\n'), 'utf8');
	}
}

const verb = CHECK ? '发现' : '已删除';
console.log(
	`${CHECK ? '检查' : '处理'} ${files.length} 个模型文件：${verb} ${removedLines} 条自引用 import` +
		`（涉及 ${changedFiles} 个文件${trimmedNames ? `，其中 ${trimmedNames} 条只摘掉名字` : ''}）`
);
if (touched.length) console.log(`  ${touched.join(', ')}`);

if (CHECK && removedLines > 0) {
	console.error('✗ 仍存在自引用 import（会导致 TS2440）。');
	process.exit(1);
}
