<template>
	<div class="layout-columns-aside">
		<div class="layout-logo"><img :src="themeConfig.logoUrl" class="layout-logo-medium-img" /></div>
		<el-scrollbar>
			<ul @mouseleave="onColumnsAsideMenuMouseleave()">
				<li
					v-for="(v, k) in state.columnsAsideList"
					:key="k"
					@click="onColumnsAsideMenuClick(v)"
					@mouseenter="onColumnsAsideMenuMouseenter(v, k)"
					:ref="
						(el) => {
							if (el) columnsAsideOffsetTopRefs[k] = el;
						}
					"
					:class="{ 'layout-columns-active': state.liIndex === k, 'layout-columns-hover': state.liHoverIndex === k }"
					:title="$t(`message.menu.${v.meta.title}`)"
				>
					<div :class="themeConfig.columnsAsideLayout" v-if="!v.meta.isLink || (v.meta.isLink && v.meta.isIframe)">
						<SvgIcon :name="v.meta.icon" />
						<div class="columns-vertical-title font12">
							{{ $t(`message.menu.${v.meta.title}`) }}
						</div>
					</div>
					<div :class="themeConfig.columnsAsideLayout" v-else>
						<a :href="v.meta.isLink" target="_blank">
							<SvgIcon :name="v.meta.icon" />
							<div class="columns-vertical-title font12">
								{{ $t(`message.menu.${v.meta.title}`) }}
							</div>
						</a>
					</div>
				</li>
				<div ref="columnsAsideActiveRef" :class="themeConfig.columnsAsideStyle"></div>
			</ul>
		</el-scrollbar>
	</div>
</template>

<script setup lang="ts" name="layoutColumnsAside">
import { reactive, ref, onMounted, nextTick, watch, onUnmounted } from 'vue';
import { useRoute, useRouter, onBeforeRouteUpdate, RouteRecordRaw } from 'vue-router';
import { storeToRefs } from 'pinia';
import { useRoutesList } from '/@/stores/routesList';
import { useThemeConfig } from '/@/stores/themeConfig';
import mittBus from '/@/utils/mitt';

// 定义变量内容
const columnsAsideOffsetTopRefs = ref<RefType>([]);
const columnsAsideActiveRef = ref();
const stores = useRoutesList();
const storesThemeConfig = useThemeConfig();
const { routesList, isColumnsMenuHover, isColumnsNavHover } = storeToRefs(stores);
const { themeConfig } = storeToRefs(storesThemeConfig);
const route = useRoute();
const router = useRouter();
const state = reactive<ColumnsAsideState>({
	columnsAsideList: [],
	liIndex: 0,
	liOldIndex: null,
	liHoverIndex: null,
	liOldPath: null,
	difference: 0,
	routeSplit: [],
});

// 设置菜单高亮位置移动
const setColumnsAsideMove = (k: number) => {
	if (k === undefined) return false;
	state.liIndex = k;
	columnsAsideActiveRef.value.style.top = `${columnsAsideOffsetTopRefs.value[k].offsetTop + state.difference}px`;
};
// 菜单高亮点击事件
const onColumnsAsideMenuClick = async (v: RouteItem) => {
	let { path, redirect } = v;
	if (redirect) {
		onColumnsAsideDown(v.k);
		if (route.path.startsWith(redirect)) mittBus.emit('setSendColumnsChildren', setSendChildren(redirect));
		else router.push(redirect);
	} else {
		if (!v.children || v.children?.length === 0) {
			router.push(path);
			onColumnsAsideDown(v.k);
		} else {
			// 显示子级菜单
			const resData: MittMenu = setSendChildren(path);
			if (Object.keys(resData).length <= 0) return false;
			onColumnsAsideDown(resData.item?.k);
			mittBus.emit('setSendColumnsChildren', resData);
		}
	}

	// 无二级菜单可展示时收起；有子项则展开（商户常见「目录下仅 1 个菜单」，原先 length>1 才展开会导致二级栏空白）
	themeConfig.value.isCollapse = !v.children || v.children.length === 0;
};
// 鼠标移入时，显示当前的子级菜单
const onColumnsAsideMenuMouseenter = (v: RouteRecordRaw, k: number) => {
	if (!themeConfig.value.isColumnsMenuHoverPreload) return false;
	let { path } = v;
	state.liOldPath = path;
	state.liOldIndex = k;
	state.liHoverIndex = k;
	mittBus.emit('setSendColumnsChildren', setSendChildren(path));
	stores.setColumnsMenuHover(false);
	stores.setColumnsNavHover(true);
};
// 鼠标移走时，显示原来的子级菜单
const onColumnsAsideMenuMouseleave = async () => {
	if (!themeConfig.value.isColumnsMenuHoverPreload) return false;
	await stores.setColumnsNavHover(false);
	// 添加延时器，防止拿到的 store.state.routesList 值不是最新的
	setTimeout(() => {
		if (!isColumnsMenuHover && !isColumnsNavHover) mittBus.emit('restoreDefault');
	}, 100);
};
// 设置高亮动态位置
const onColumnsAsideDown = (k: number) => {
	nextTick(() => {
		setColumnsAsideMove(k);
	});
};
// 按当前路由同步二级菜单展开状态
const setMenuAutoCollaps = (path: string) => {
	const resData: MittMenu = setSendChildren(path);
	// 有可展示的二级项就展开；空列表才收成 1px（原先「redirect + ≤1 子项就收起」会把商户二级栏默认藏掉）
	themeConfig.value.isCollapse = resData.children.length === 0;
	return resData;
};
// 把当前二级菜单推给 aside（可被 aside 就绪回调反复调用）
const pushColumnsChildren = (path: string = route.path) => {
	const resData: MittMenu = setMenuAutoCollaps(path);
	onColumnsAsideDown(resData.item?.k);
	mittBus.emit('setSendColumnsChildren', resData);
	return resData;
};
// 设置/过滤路由（非静态路由/是否显示在菜单中）
const setFilterRoutes = () => {
	state.columnsAsideList = filterRoutesFun(routesList.value);
	pushColumnsChildren(route.path);
};
// 传送当前子级数据到菜单中（最长 path 前缀匹配，兼容无父目录的顶栏叶子菜单）
const setSendChildren = (path: string) => {
	const currentData: MittMenu = { children: [] };
	let best: RouteItem | undefined;
	let bestK = -1;
	state.columnsAsideList.forEach((v: RouteItem, k: number) => {
		if (path === v.path || path.startsWith(`${v.path}/`)) {
			if (!best || v.path.length > best.path.length) {
				best = v;
				bestK = k;
			}
		}
	});
	if (!best) {
		const routeFirst = `/${path.split('/').filter(Boolean)[0] || ''}`;
		state.columnsAsideList.forEach((v: RouteItem, k: number) => {
			if (v.path === routeFirst) {
				best = v;
				bestK = k;
			}
		});
	}
	if (best && bestK >= 0) {
		best['k'] = bestK;
		currentData.item = { ...best };
		currentData.children = best.children?.length ? best.children : [{ ...best }];
	}
	return currentData;
};
// 路由过滤递归函数
const filterRoutesFun = <T extends RouteItem>(arr: T[]): T[] => {
	return arr
		.filter((item: T) => !item.meta?.isHide)
		.map((item: T) => {
			item = Object.assign({}, item);
			if (item.children) item.children = filterRoutesFun(item.children);
			return item;
		});
};
// tagsView 点击时，根据路由查找下标 columnsAsideList，实现左侧菜单高亮
const setColumnsMenuHighlight = (path: string) => {
	state.routeSplit = path.split('/');
	state.routeSplit.shift();
	const routeFirst = `/${state.routeSplit[0]}`;
	const currentSplitRoute = state.columnsAsideList.find((v: RouteItem) => v.path === routeFirst);
	if (!currentSplitRoute) return false;
	// 延迟拿值，防止取不到
	setTimeout(() => {
		onColumnsAsideDown(currentSplitRoute.k);
	}, 0);
};
// 页面加载时
onMounted(() => {
	setFilterRoutes();
	// aside 异步挂载晚于本组件时会主动索取；收到后立即再推一次，避免 500ms 竞态丢事件
	mittBus.on('requestColumnsChildren', () => {
		pushColumnsChildren(route.path);
	});
	// 销毁变量，防止鼠标再次移入时，保留了上次的记录
	mittBus.on('restoreDefault', () => {
		state.liOldIndex = null;
		state.liOldPath = null;
	});
});
// 页面卸载时
onUnmounted(() => {
	mittBus.off('requestColumnsChildren');
	mittBus.off('restoreDefault');
});
// 路由更新时
onBeforeRouteUpdate((to) => {
	pushColumnsChildren(to.path);
	setColumnsMenuHighlight(to.path);
});
// 监听布局配置信息的变化，动态增加菜单高亮位置移动像素
watch(
	[() => themeConfig.value.columnsAsideStyle, isColumnsMenuHover, isColumnsNavHover],
	() => {
		themeConfig.value.columnsAsideStyle === 'columnsRound' ? (state.difference = 3) : (state.difference = 0);
		if (!isColumnsMenuHover.value && !isColumnsNavHover.value) {
			state.liHoverIndex = null;
			mittBus.emit('setSendColumnsChildren', setSendChildren(route.path));
		} else {
			state.liHoverIndex = state.liOldIndex;
			if (!state.liOldPath) return false;
			mittBus.emit('setSendColumnsChildren', setSendChildren(state.liOldPath));
		}
	},
	{
		deep: true,
	}
);
</script>

<style scoped lang="scss">
.layout-columns-aside {
	width: var(--el-columnsMenuWidth);
	height: 100%;
	background: var(--next-bg-columnsMenuBar);
	ul {
		position: relative;
		.layout-columns-active,
		.layout-columns-active a {
			color: var(--next-bg-columnsMenuBarColor) !important;
			transition: 0.3s ease-in-out;
		}
		.layout-columns-hover {
			color: var(--el-color-primary);
			a {
				color: var(--el-color-primary);
			}
		}
		li {
			color: var(--next-bg-columnsMenuBarColor);
			width: 100%;
			height: var(--el-columnsMenuHeight);
			text-align: center;
			display: flex;
			cursor: pointer;
			position: relative;
			z-index: 1;
			&:hover {
				//@extend .layout-columns-hover;
				background-color: var(--el-color-primary-light-3);
			}
			.columns-vertical {
				margin: auto;
				.columns-vertical-title {
					padding-top: 1px;
				}
			}
			.columns-horizontal {
				display: flex;
				height: var(--el-columnsMenuHeight);
				width: 100%;
				align-items: center;
				padding: 0 5px;
				i {
					margin-right: 3px;
				}
				a {
					display: flex;
					.columns-horizontal-title {
						padding-top: 1px;
					}
				}
			}
			a {
				text-decoration: none;
				color: var(--next-bg-columnsMenuBarColor);
			}
		}
		.columns-round {
			background: var(--el-color-primary);
			color: var(--el-color-white);
			position: absolute;
			left: 50%;
			top: 2px;
			height: var(--el-columnsMenuRoundHeight);
			width: var(--el-columnsMenuRoundWidth);
			transform: translateX(-50%);
			z-index: 0;
			transition: 0.5s ease-in-out;
			border-radius: 5px;
		}
		.columns-card {
			@extend .columns-round;
			top: 0;
			height: var(--el-columnsMenuHeight);
			width: 100%;
			border-radius: 0;
		}
	}
}

.layout-logo {
	height: var(--el-columnsLogoHeight);
	display: flex;
	align-items: center;
	justify-content: center;
	margin-bottom: 8px;
	margin-top: 5px;
	animation: logoAnimation 0.3s ease-in-out;
	&-medium-img {
		width: auto;
		height: 80%;
	}
	&:hover {
		img {
			animation: logoAnimation 0.3s ease-in-out;
		}
	}
}

:deep(.el-scrollbar) {
	height: calc(100% - 50px);
}
</style>
