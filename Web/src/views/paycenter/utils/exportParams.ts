/**
 * 导出接口的时间区间入参 —— 为什么必须传「本地格式字符串」而不是 Date
 * ────────────────────────────────────────────────────────────────────────────
 * 生成的模型（`PayExportInput` / `PayOrderExportInput`）把后端的 C# `DateTime`
 * 映射成 TS 的 `Date`，但本项目所有日期控件都用 `value-format="YYYY-MM-DD HH:mm:ss"`，
 * 所以 `queryParams.startTime` 实际拿到的是**本地格式字符串**。
 *
 * 如果照着类型提示改成真传 `Date` 对象：axios 会把它序列化成带 `Z` 的 ISO 串，
 * 后端按 UTC 解析 → 导出区间整体**偏移 8 小时**（东八区）。
 * 所以这里**故意**保持本地格式字符串，类型上对不上，只能显式断言。
 *
 * ★ 把断言集中在这一个文件，而不是在三个页面各写一个裸 `as`：
 *   裸 `as` 一旦被类型检查报错，下一个人最可能的「修法」就是改成 `new Date(...)`，
 *   正好把上面那个 8 小时偏移引进来。把理由写在旁边，堵掉那种改法。
 *
 * ⚠️ 依赖未决项：**时间基准是否统一为 UTC + timestamptz**（任务 #20）。
 *    一旦统一为 UTC，本文件的实现与上面这段说明都要跟着改。
 */

/** 与 `el-date-picker` 的 `value-format="YYYY-MM-DD HH:mm:ss"` 保持一致 */
const fmtLocal = (d: Date): string => {
	const p = (n: number) => String(n).padStart(2, '0');
	return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
};

const SEVEN_DAYS_MS = 7 * 24 * 3600 * 1000;

/**
 * 取导出用的时间区间。
 *
 * 没填时按「最近 7 天」兜底 —— 导出接口**必须**带区间（服务端按区间限流），
 * 不给默认值的话用户点了导出会没有任何反应，看起来像坏了。
 */
export const resolveExportRange = (q: { startTime?: string; endTime?: string }) => ({
	startTime: q.startTime ?? fmtLocal(new Date(Date.now() - SEVEN_DAYS_MS)),
	endTime: q.endTime ?? fmtLocal(new Date()),
});

/**
 * 把入参装成生成模型要求的类型。
 *
 * 这里的日期字段**故意**是本地格式字符串，与模型声明的 `Date` 不符（见文件头）。
 * 函数体只有一次断言，但它是一个**可被搜索、带说明的落点** —— 直接写
 * `as unknown as X` 会在页面里留下一个没有上下文的魔法断言。
 */
export const toExportInput = <T>(params: Record<string, unknown>): T => params as unknown as T;
