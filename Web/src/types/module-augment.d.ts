// ⚠️ 本文件含**顶层 import** → 它是一个「模块」，不是「脚本」。
//    模块里的 declare 只在本模块内可见，**不会**成为全局类型。
//    因此：全局类型/全局 Window 增强一律放同目录 global.d.ts（那边无顶层 import，是脚本）。
//    历史 bug：两者曾混在同一文件，导致 53 处 TS2304（RouteItem/RefType/... 全部「找不到名字」）
//    以及 Window 增强静默失效（__env__ 报 TS2339）。拆分后两者各自成立。

import sysDict from '/@/components/sysDict/sysDict.vue';

// ★ 这里**故意**不放「无 body 的包 shim」（`declare module 'xxx';`）。
//   那类声明只有在**脚本**文件里才是「新声明一个环境模块」；放在本文件（模块）里会被
//   当成「增强一个**已存在**的模块」，对没有类型声明的包**静默不生效**（实测：导入处报 TS7016）。
//   它们已移到 global.d.ts（脚本）。见那边「外部 npm 插件模块 shim」一节。
//
//   下面这些**不要**启用（保留注释是为了留下判断依据）：
// declare module 'splitpanes';              // 自带 types → shim 会遮蔽真类型
// declare module 'vue-element-plus-x';      // 自带 types → shim 会遮蔽真类型
// declare module 'jwchat';                  // 未安装
// declare module '@liveqing/liveplayer-v3'; // 未安装
// declare module '@wangeditor/editor-for-vue';
// declare module 'js-table2excel';
// declare module 'qs';
// declare module 'sortablejs';

// ★ 通配符声明同样只在脚本文件里生效。这里保留原样：vite/client 已提供这些类型，
//   挪到脚本文件激活反而可能遮蔽它或造成重复声明。
declare module '*.json';
declare module '*.png';
declare module '*.jpg';
declare module '*.scss';
declare module '*.ts';
declare module '*.js';

// 声明文件，*.vue 后缀的文件交给 vue 模块来处理
declare module '*.vue' {
	import type { DefineComponent } from 'vue';
	const component: DefineComponent<{}, {}, any>;
	export default component;
}

// 声明全局组件
declare module 'vue' {
	export interface GlobalComponents {
		GSysDict: typeof sysDict;
	}
}
