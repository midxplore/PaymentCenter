// ⚠️ 本文件**必须保持为「脚本」**（顶层不得出现任何 import/export，否则整个文件退化为模块，
//    下面所有 declare 都只在本文件内可见，全项目又会变成「找不到名字」）。
//    需要 import 的模块增强（declare module 'vue' 等）请放 module-augment.d.ts。

// ── 外部 npm 插件模块 shim ─────────────────────────────────────────────────
// ★ 这些「无 body」的 `declare module 'xxx';` **必须放在脚本文件里**才生效。
//   放在模块文件（如 module-augment.d.ts）里会被**静默忽略** —— 模块里的
//   `declare module` 语义是「增强一个**已存在**的模块」，而不是「新声明一个环境模块」，
//   对本身没有类型声明的包不起作用。最小复现实测：
//     shim 放模块文件 → 导入处报 TS2307 / TS7016（等于没写）
//     shim 放脚本文件 → 零报错
//   这些 shim 从前长期待在模块作用域里，所以一直是**空操作**。
//
//   ★ 只列「确实没有类型」且「确实被 import」的包。自带 types 的包**不要**加 shim，
//     否则 shim 会**遮蔽**真类型，把有类型的包退化成 any。
//     （例：splitpanes、vue-element-plus-x 都自带 types，见 module-augment.d.ts 的说明。）
declare module 'vue-grid-layout';
declare module 'qrcodejs2-fixes';
declare module 'js-cookie';
declare module 'vue-plugin-hiprint';
declare module 'vcrontab-3';
declare module 'vue-signature-pad';
declare module 'vform3-builds';
declare module 'crypto-js';

// 声明文件，定义全局变量
/* eslint-disable */
declare interface Window {
	nextLoading: boolean;
	BMAP_SATELLITE_MAP: any;
	BMap: any;
	__env__: any;
	$changeLang: (lang: string) => void;
}

// 声明路由当前项类型
declare type RouteItem<T = any> = {
	path: string;
	name?: string | symbol | undefined | null;
	redirect?: string;
	// 后端菜单类型：1=目录 2=菜单 3=按钮（对应 MenuTypeEnum）。
	// 路由对象由 /@/router/backEnd.ts 的 backEndComponent 直接从 loginMenuTree 的返回项
	// 加工而来，只替换 component、其余字段原样保留，所以运行时确实带 type；
	// navBars/topBar/search.vue 依赖它过滤目录项（v.type !== 1）。
	// 这里只能写 number：global.d.ts 是脚本（不允许顶层 import），引不到 MenuTypeEnum。
	type?: number;
	k?: T;
	meta?: {
		title?: string;
		isLink?: string;
		isHide?: boolean;
		isKeepAlive?: boolean;
		isAffix?: boolean;
		isIframe?: boolean;
		roles?: string[];
		icon?: string;
		isDynamic?: boolean;
		isDynamicPath?: string;
		isIframeOpen?: string;
		loading?: boolean;
	};
	children: T[];
	query?: { [key: string]: T };
	params?: { [key: string]: T };
	contextMenuClickId?: string | number;
	commonUrl?: string;
	isFnClick?: boolean;
	url?: string;
	transUrl?: string;
	title?: string;
	id?: string | number;
};

// 声明路由 to from
declare interface RouteToFrom<T = any> extends RouteItem {
	path?: string;
	children?: T[];
}

// 声明 Nullable 可空类型
// T 默认 undefined：裸用 `Nullable` 等价于原来的 `null | undefined`，
// 因此 NullableString / NullableNumber / NullableBoolean 语义不变；
// 同时支持 `Nullable<HTMLCanvasElement>` 这种泛型用法（base64Conver.ts 一直在用，
// 只是从前类型未生效、检查不到）。
declare type Nullable<T = undefined> = T | null;
declare type NullableString = Nullable | String;
declare type NullableNumber = Nullable | Number;
declare type NullableBoolean = Nullable | Boolean;

// 声明路由当前项类型集合
declare type RouteItems<T extends RouteItem = any> = T[];

// 声明 ref
declare type RefType<T = any> = T | null;

// 声明 HTMLElement
declare type HtmlType = HTMLElement | string | undefined | null;

// 申明 children 可选
declare type ChilType<T = any> = {
	children?: T[];
};

// 申明 数组
declare type EmptyArrayType<T = any> = T[];

// 申明 对象
declare type EmptyObjectType<T = any> = {
	[key: string]: T;
};

// 申明 select option
declare type SelectOptionType = {
	value: string | number;
	label: string | number;
};

// 鼠标滚轮滚动类型
declare interface WheelEventType extends WheelEvent {
	wheelDelta: number;
}

// table 数据格式公共类型
declare interface TableType<T = any> {
	total: number;
	loading: boolean;
	param: {
		pageNum: number;
		pageSize: number;
		[key: string]: T;
	};
}

// 表单数据合并格式
type Merge<A, B> = {
	[K in keyof A | keyof B]: K extends keyof A ? (K extends keyof B ? A[K] | B[K] : A[K]) : K extends keyof B ? B[K] : never;
};
