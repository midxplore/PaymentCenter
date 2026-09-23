import { RouteRecordRaw } from 'vue-router';
import pinia from '/@/stores/index';
import { useUserInfo } from '/@/stores/userInfo';
import { useRequestOldRoutes } from '/@/stores/requestOldRoutes';
import { Session } from '/@/utils/storage';
import { NextLoading } from '/@/utils/loading';
import { dynamicRoutes, notFoundAndNoPower } from '/@/router/route';
import { formatTwoStageRoutes, formatFlatteningRoutes, copyRouteTree, router } from '/@/router/index';
import { useRoutesList } from '/@/stores/routesList';
import { useTagsViewRoutes } from '/@/stores/tagsViewRoutes';

import { getAPI } from '/@/utils/axios-utils';
import { SysMenuApi } from '/@/api-services/system/api';
// import { ElMessage } from 'element-plus';

// 后端控制路由

/**
 * 获取目录下的 .vue、.tsx 全部文件
 * @method import.meta.glob
 * @link 参考：https://cn.vitejs.dev/guide/features.html#json
 */
const layouModules: any = import.meta.glob('../layout/routerView/*.{vue,tsx}');
const viewsModules: any = import.meta.glob('../views/**/*.{vue,tsx}');
const dynamicViewsModules: Record<string, Function> = Object.assign({}, { ...layouModules }, { ...viewsModules });

/**
 * 后端控制路由：初始化方法，防止刷新时路由丢失
 * @method NextLoading 界面 loading 动画开始执行
 * @method useUserInfo().setUserInfos() 触发初始化用户信息 pinia
 * @method useRequestOldRoutes().setRequestOldRoutes() 存储接口原始路由（未处理component），根据需求选择使用
 * @method setAddRoute 添加动态路由
 * @method setFilterMenuAndCacheTagsViewRoutes 设置路由到 pinia routesList 中（已处理成多级嵌套路由）及缓存多级嵌套数组处理后的一维数组
 */
export async function initBackEndControlRoutes() {
	// 界面 loading 动画开始执行
	if (window.nextLoading === undefined) NextLoading.start();
	// 无 token 停止执行下一步
	if (!Session.get('token')) return false;
	// 触发初始化用户信息 pinia
	// https://gitee.com/lyt-top/vue-next-admin/issues/I5F1HP
	await useUserInfo().setUserInfos();
	await useUserInfo().setConstList();
	await useUserInfo().setDictList();
	// 获取路由菜单数据
	const res = await getBackEndControlRoutes();
	// 无登录权限时，添加判断
	// https://gitee.com/lyt-top/vue-next-admin/issues/I64HVO
	if (res == undefined || res.length <= 0) return Promise.resolve(true);
	// 存储接口原始路由（未处理component），根据需求选择使用
	useRequestOldRoutes().setRequestOldRoutes(res as string[]);
	// 处理路由（component），替换 dynamicRoutes（/@/router/route）第一个顶级 children 的路由
	dynamicRoutes[0].children = await backEndComponent(res);
	// 登录落地：有工作台则保持 /dashboard/home；否则落到首个可见菜单（商户角色无工作台时避免 404）
	const firstPath = findFirstMenuPath(dynamicRoutes[0].children as any[]);
	if (firstPath) {
		const preferHome = '/dashboard/home';
		const hasHome = routePathExists(dynamicRoutes[0].children as any[], preferHome);
		dynamicRoutes[0].redirect = hasHome ? preferHome : firstPath;
	}
	// 添加动态路由
	await setAddRoute();
	// 设置路由到 pinia routesList 中（已处理成多级嵌套路由）及缓存多级嵌套数组处理后的一维数组
	setFilterMenuAndCacheTagsViewRoutes();
}

/**
 * 设置路由到 pinia routesList 中（已处理成多级嵌套路由）及缓存多级嵌套数组处理后的一维数组
 * @description 用于左侧菜单、横向菜单的显示
 * @description 用于 tagsView、菜单搜索中：未过滤隐藏的(isHide)
 */
export async function setFilterMenuAndCacheTagsViewRoutes() {
	const storesRoutesList = useRoutesList(pinia);
	storesRoutesList.setRoutesList(dynamicRoutes[0].children as any);
	setCacheTagsViewRoutes();
}

/**
 * 缓存多级嵌套数组处理后的一维数组
 * @description 用于 tagsView、菜单搜索中：未过滤隐藏的(isHide)
 */
export function setCacheTagsViewRoutes() {
	const storesTagsView = useTagsViewRoutes(pinia);
	storesTagsView.setTagsViewRoutes(formatTwoStageRoutes(formatFlatteningRoutes(copyRouteTree(dynamicRoutes)))[0].children);
}

/**
 * 处理路由格式及添加捕获所有路由或 404 Not found 路由
 * @description 替换 dynamicRoutes（/@/router/route）第一个顶级 children 的路由
 * @returns 返回替换后的路由数组
 */
export function setFilterRouteEnd() {
	// ★ 必须 copyRouteTree：flatten 会删 children，不能改坏侧栏用的 dynamicRoutes 菜单树
	let filterRouteEnd: any = formatTwoStageRoutes(formatFlatteningRoutes(copyRouteTree(dynamicRoutes)));
	// notFoundAndNoPower 防止 404、401 不在 layout 布局中，不设置的话，404、401 界面将全屏显示
	// 关联问题 No match found for location with path 'xxx'
	filterRouteEnd[0].children = [...filterRouteEnd[0].children, ...notFoundAndNoPower];
	return filterRouteEnd;
}

/**
 * 添加动态路由
 * @method router.addRoute
 * @description 此处循环为 dynamicRoutes（/@/router/route）第一个顶级 children 的路由一维数组，非多级嵌套
 * @link 参考：https://next.router.vuejs.org/zh/api/#addroute
 */
export async function setAddRoute() {
	await setFilterRouteEnd().forEach((route: RouteRecordRaw) => {
		router.addRoute(route);
	});
}

/**
 * 请求后端路由菜单接口
 * @description isRequestRoutes 为 true，则开启后端控制路由
 * @returns 返回后端路由菜单数据
 */
export async function getBackEndControlRoutes() {
	var res = await getAPI(SysMenuApi).apiSysMenuLoginMenuTreeGet();
	// if (res.data.result == undefined || res.data.result.length < 1) {
	// 	ElMessage.error('没有任何菜单权限，请联系管理员！');
	// 	setTimeout(() => {
	// 		Session.removeToken();
	// 		window.location.reload();
	// 	}, 3000);
	// }
	let resData = res.data.result;
	// recursive(resData);

	return resData;
}

/**
 * 改成前端国际化，此方法作废
 * @param resData
 */
function recursive(resData: any) {
	if (resData && resData.length > 0) {
		resData.forEach((item: any) => {
			if (item.children && item.children.length > 0) {
				recursive(item.children);
			}
		});
	}
}

/**
 * 重新请求后端路由菜单接口
 * @description 用于菜单管理界面刷新菜单（未进行测试）
 * @description 路径：/src/views/system/menu/component/addMenu.vue
 */
export async function setBackEndControlRefreshRoutes() {
	await getBackEndControlRoutes();
}

/**
 * 后端路由 component 转换
 * @param routes 后端返回的路由表数组
 * @returns 返回处理成函数后的 component
 */
export function backEndComponent(routes: any) {
	if (!routes) return;
	return routes.map((item: any) => {
		if (!item.path) item.path = ''; // 防止后端返回的路由没有path属性，导致路由报错
		// Layout 目录：dynamicImport 命中 0 → undefined。真正页面扁平挂在 `/` 下，
		// 由 layout/main 内的 parent.vue 渲染；不要再映射成嵌套 parent（会双 keep-alive 空白）。
		if (item.component && item.component !== 'Layout' && item.component !== 'layout') {
			item.component = dynamicImport(dynamicViewsModules, item.component as string);
		} else if (item.component === 'Layout' || item.component === 'layout') {
			item.component = undefined;
		}
		item.children && backEndComponent(item.children);
		return item;
	});
}

/**
 * 后端路由 component 转换函数
 * @param dynamicViewsModules 获取目录下的 .vue、.tsx 全部文件
 * @param component 当前要处理项 component
 * @returns 返回处理成函数后的 component
 */
export function dynamicImport(dynamicViewsModules: Record<string, Function>, component: string) {
	const keys = Object.keys(dynamicViewsModules);
	const matchKeys = keys.filter((key) => {
		const k = key.replace(/..\/views|../, '');
		return k.startsWith(`${component}`) || k.startsWith(`/${component}`);
	});
	if (matchKeys?.length === 1) {
		const matchKey = matchKeys[0];
		return dynamicViewsModules[matchKey];
	}
	if (matchKeys?.length > 1) {
		return false;
	}
}

/** 深度优先找第一个带 path 的叶子菜单（用于无工作台时的登录落地） */
function findFirstMenuPath(routes: any[] | undefined): string | undefined {
	if (!routes?.length) return undefined;
	for (const r of routes) {
		if (r?.children?.length) {
			const child = findFirstMenuPath(r.children);
			if (child) return child;
		}
		if (r?.path) return r.path as string;
	}
	return undefined;
}

function routePathExists(routes: any[] | undefined, path: string): boolean {
	if (!routes?.length) return false;
	for (const r of routes) {
		if (r?.path === path) return true;
		if (routePathExists(r?.children, path)) return true;
	}
	return false;
}
