# 本目录为什么存在

`typescript-axios/apiInner.mustache` 是从 `swagger-codegen-cli.jar` 里取出、**改了一行**的模板，
由 `build.sh` 通过 `-t` 传给生成器（`build_api.sh` 现在只是选地址、再委托 `build.sh`）。
只放被改过的这一个文件，**其余模板自动回落 jar 内置**
（已实测：单文件 fork 与整套 fork 的输出完全一致，552 个文件逐个相同）。

## 改了什么

在**读取 `headers` 之前**补一次非 `any` 的赋值：

```diff
  localVarRequestOptions.headers = {...localVarHeaderParameter, ...headersFromBaseOptions, ...options.headers};
+ localVarRequestOptions.headers = localVarRequestOptions.headers || {};
  const needsSerialization = (typeof body !== "string") || localVarRequestOptions.headers['Content-Type'] === 'application/json';
```

## ★ 为什么补丁必须放在这里（放错位置等于没修）

最初的版本把这一行放在**声明之后**（`const localVarRequestOptions = {...}` 的下一行）。
**实测无效**：类型检查计数一条都没降。原因是模板后面还有一次赋值，
而 TypeScript 对「赋值后读取可选属性」的收窄规则是：

> **只有当赋值右侧不是 `any` 时，才会收窄该属性。**

生成物里 `localVarHeaderParameter` 是 `{} as any`，于是
`{...localVarHeaderParameter, ...headersFromBaseOptions, ...options.headers}`
整体是 `any` → TS **不**收窄 `localVarRequestOptions.headers` →
下一行读 `['Content-Type']` 报 `TS18048`。放在声明之后的那一行，收窄会被这次 `any` 赋值**覆盖掉**。

最小复现（`/tmp/pc-ts-probe/probe3.ts`，用 `tsc --strict` 实测）：

| 变体 | 写法 | 结果 |
|---|---|---|
| M1 | `o.headers = { a: 'b' }`（右侧非 any） | ✅ 收窄，无错 |
| M2 | `o.headers = { ...anyVal }`（右侧 any） | ❌ 不收窄 |
| M3 | `o.headers = { ...anyVal, ...(options.headers \|\| {}) }` | ❌ 仍是 any |
| **M6** | 之后再 `o.headers = o.headers \|\| {}` | ✅ 收窄 |
| M7 | 读取处 `(o.headers \|\| {})['x']` | ✅ 无错 |

采用 M6（多一行赋值，读表达式不动）。**运行时是 no-op**：上一行永远是对象字面量，
不可能走到 `|| {}`；纯粹是给类型检查器一个非 `any` 的赋值。

**效果（实测）**：这条模板补丁消掉 **175** 条 `TS18048`（生成物 552 个文件中 **46 个**各多这一行）。

## ★ 另一个修法：生成后删掉「自引用 import」（递归模型 → TS2440）

还有一类**模板层修不掉**的：**递归模型**（如 `ApiOutput.children?: Array<ApiOutput>`）会让生成器
**在自己的文件里 import 自己**，与本地声明冲突 → `TS2440`（9 条）。

模板层修不掉的**实测证据** —— 本生成器的 Handlebars 没有相等比较 helper：

```
could not find helper: 'eq'
{{#unless (eq class ../classname)}}...
```

（模板里 `{{class}}` 与 `{{../classname}}` **都取得到** —— 已用探针模板验证：
`filter.ts` 同时有 `Filter`（自引用）与两个合法枚举 import。但没有运算符可用。）

所以退到生成后处理：`build.sh` 在替换前调用 `api_build/strip-self-imports.mjs`。
规则**无歧义、与生成器版本无关**：

> 一个文件**永远不该**从它自己的路径 import。

- 只匹配 `import { ... } from './<本文件名>'`；同一行若还 import 了别的名字，只摘掉自引用那一个。
- **fail-closed**：出现「从自身路径 import 了非同名标识符」这种异常形态 → 退出 3、**不改文件**；
  目录不存在 → 退出 2。宁可报错也不猜。
- `--check` 只报告不修改，有自引用则退出 1（可挂 CI）。

## ★ 第三个坑：`swagger-codegen` 失败时**退出码是 0**

实测：把 `-i` 指向一个不可达地址，java 打印 `Unable to read URL` 异常栈，
但**进程退出码仍是 0**。所以「检查 java 退出码」**不足以**判断生成是否成功。
（同理，模板里写错 helper 时它也只是打印 `Could not generate model 'X'` 然后退出 0。）

因此 `build.sh` 现在有三道闸：
1. java 退出码（拦真正的崩溃）；
2. **产物非空校验**（`system.tmp/apis` 必须存在且有文件）—— 这一道才是拦住上面那种情况的；
3. 自引用清理脚本自身的退出码（2 / 3）。

配套改动：生成改为「先写 `system.tmp` → 校验 + 清理 → 才替换 `system`」。
原实现是「先 `rm -rf system` 再生成」，一旦生成失败，正式目录已经没了，
而脚本**仍打印「生成结束」并以 0 退出** —— 前端会拿不到任何 api 客户端，
npm / CI 侧看到的却全是成功。`script/build-api.js` 也改为把子进程退出码**传播**出去
（原来只 `console.error`，自己仍以 0 退出）。

## 总效果（实测）

| 指标 | 修之前 | 修之后 |
|---|---|---|
| `src/api-services/` 类型错误 | 184 | **0** |
| 全仓已登记债务 | 451 | **267** |
| 类型检查闸门 | 0 / 未归类 0 | 0 / 未归类 0 |
| `npm run build` | 通过 | 通过 |

并且 `src/api-services/` 已从「已登记债务」提升为**硬闸门**（见 `Web/script/check-types.mjs`）：
一旦回退，闸门**直接失败**，而不是安静地打印一个数字。

## 维护须知

- ★ 只加 `-t` 不改模板 = 没有效果；只改模板不加 `-t` = 没有效果。**两者必须同时存在。**
- ★ 自引用清理是 `build.sh` 里的一步，**不要**绕过 `build.sh` 直接调 java 生成。
- ★ 升级 `swagger-codegen-cli.jar` 后，本 fork 会**固定住旧版 apiInner.mustache**；
  若新版内置模板有变化，需重新取出并重打补丁。失效是**可见的**
  （`npm run typecheck` 会因 `src/api-services/` 不再是 0 而**直接失败**），不会静默。
- 想临时验证「不 fork 会怎样」：去掉 `-t` 重新生成，那 175 条错误会原样回来。
