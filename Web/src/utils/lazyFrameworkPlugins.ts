/**
 * 框架重依赖按需注册（支付中心首屏不加载）。
 * 不删除任何页面/包；仅在真正进入用到它们的页面时 import + app.use 一次。
 */
import type { App } from 'vue';
import { getCurrentInstance } from 'vue';

type FlagKey =
	| '__pc_vform3'
	| '__pc_tree_org'
	| '__pc_signature'
	| '__pc_grid_layout'
	| '__pc_hiprint'
	| '__pc_animate_css';

function resolveApp(app?: App): App {
	if (app) return app;
	const inst = getCurrentInstance();
	if (!inst) throw new Error('lazyFrameworkPlugins: 无 Vue 实例，请在 setup 内调用');
	return inst.appContext.app;
}

function flagged(app: App, key: FlagKey): boolean {
	return !!(app as unknown as Record<string, boolean>)[key];
}

function mark(app: App, key: FlagKey) {
	(app as unknown as Record<string, boolean>)[key] = true;
}

/** 表单设计器（开发工具 → 表单设计） */
export async function ensureVForm3(app?: App) {
	const a = resolveApp(app);
	if (flagged(a, '__pc_vform3')) return;
	const [{ default: VForm3 }] = await Promise.all([import('vform3-builds'), import('vform3-builds/dist/designer.style.css')]);
	a.use(VForm3);
	mark(a, '__pc_vform3');
}

/** 组织架构图（用户中心等） */
export async function ensureVue3TreeOrg(app?: App) {
	const a = resolveApp(app);
	if (flagged(a, '__pc_tree_org')) return;
	const [{ default: vue3TreeOrg }] = await Promise.all([import('vue3-tree-org'), import('vue3-tree-org/lib/vue3-tree-org.css')]);
	a.use(vue3TreeOrg);
	mark(a, '__pc_tree_org');
}

/** 电子签名板（个人中心） */
export async function ensureVueSignaturePad(app?: App) {
	const a = resolveApp(app);
	if (flagged(a, '__pc_signature')) return;
	const { default: VueSignaturePad } = await import('vue-signature-pad');
	a.use(VueSignaturePad);
	mark(a, '__pc_signature');
}

/** 栅格布局插件（少数演示/旧页） */
export async function ensureVueGridLayout(app?: App) {
	const a = resolveApp(app);
	if (flagged(a, '__pc_grid_layout')) return;
	const { default: VueGridLayout } = await import('vue-grid-layout');
	a.use(VueGridLayout);
	mark(a, '__pc_grid_layout');
}

/** 打印插件（关闭自动连接；有打印页再用） */
export async function ensureHiprint(app?: App) {
	const a = resolveApp(app);
	if (flagged(a, '__pc_hiprint')) return;
	const { disAutoConnect } = await import('vue-plugin-hiprint');
	disAutoConnect();
	mark(a, '__pc_hiprint');
}

/** animate.css（参数设置里动画预览等） */
export async function ensureAnimateCss() {
	const g = globalThis as unknown as Record<string, boolean>;
	if (g.__pc_animate_css) return;
	await import('animate.css');
	g.__pc_animate_css = true;
}
