<template>
	<div class="layout-navbars-breadcrumb-user pr15" :style="{ flex: layoutUserFlexNum }">
		<el-dropdown :show-timeout="70" :hide-timeout="50" trigger="click" @command="onComponentSizeChange">
			<div class="layout-navbars-breadcrumb-user-icon">
				<i class="iconfont icon-ziti" :title="$t('message.user.title0')"></i>
			</div>
			<template #dropdown>
				<el-dropdown-menu>
					<el-dropdown-item command="large" :disabled="state.disabledSize === 'large'">{{ $t('message.user.dropdownLarge') }}</el-dropdown-item>
					<el-dropdown-item command="default" :disabled="state.disabledSize === 'default'">{{ $t('message.user.dropdownDefault') }}</el-dropdown-item>
					<el-dropdown-item command="small" :disabled="state.disabledSize === 'small'">{{ $t('message.user.dropdownSmall') }}</el-dropdown-item>
				</el-dropdown-menu>
			</template>
		</el-dropdown>
		<el-dropdown v-if="themeConfig.i18NSwitch" :show-timeout="70" :hide-timeout="50" trigger="click" @command="onLanguageChange">
			<div class="layout-navbars-breadcrumb-user-icon">
				<span :class="`fi fi-${currentCountryCode}`"></span>
			</div>
			<template #dropdown>
				<el-dropdown-menu>
					<el-dropdown-item v-for="(value, key) in languageList" :key="key" :command="key" :disabled="state.disabledI18n === key">
						<div class="flex items-center">
							<div class="mr-2">
								<span :class="`fi fi-${getCountryCode(key)}`"></span>
							</div>
							<div style="margin-left: 10px">
								{{ value }}
							</div>
						</div>
					</el-dropdown-item>
				</el-dropdown-menu>
			</template>
		</el-dropdown>
		<div class="layout-navbars-breadcrumb-user-icon" @click="onSearchClick">
			<el-icon :title="$t('message.user.title2')">
				<ele-Search />
			</el-icon>
		</div>
		<div class="layout-navbars-breadcrumb-user-icon" @click="onLayoutSetingClick">
			<i class="icon-skin iconfont" :title="$t('message.user.title3')"></i>
		</div>
		<div class="layout-navbars-breadcrumb-user-icon">
			<el-popover placement="bottom" trigger="hover" transition="el-zoom-in-top" :width="400" :persistent="false">
				<template #reference>
					<el-badge :is-dot="hasUnreadNotice">
						<el-icon :title="$t('message.user.title4')">
							<ele-Bell />
						</el-icon>
					</el-badge>
				</template>
				<UserNews :noticeList="state.noticeList" />
			</el-popover>
		</div>
		<div class="layout-navbars-breadcrumb-user-icon" @click="onScreenfullClick">
			<i class="iconfont" :title="state.isScreenfull ? $t('message.user.title6') : $t('message.user.title5')" :class="!state.isScreenfull ? 'icon-fullscreen' : 'icon-tuichuquanping'"></i>
		</div>
		<div class="layout-navbars-breadcrumb-user-icon mr10" @click="onOnlineUserClick">
			<el-icon :title="$t('message.list.onlineUser')">
				<ele-User />
			</el-icon>
		</div>

		<el-dropdown :show-timeout="70" :hide-timeout="50" size="large" @command="onHandleCommandClick">
			<span class="layout-navbars-breadcrumb-user-link">
				<el-tooltip effect="dark" placement="left">
					<template #content>
						{{ $t('message.list.account') }}：{{ userInfos.account }}<br />
						{{ $t('message.list.realName') }}：{{ userInfos.realName }}<br />
						{{ $t('message.list.phone') }}：{{ userInfos.phone }}<br />
						{{ $t('message.list.email') }}：{{ userInfos.email }}<br />
						{{ $t('message.list.orgName') }}：{{ userInfos.orgName }}<br />
						{{ $t('message.list.positionText') }}：{{ userInfos.posName }}<br />
					</template>

					<img :src="userInfos.avatar" class="layout-navbars-breadcrumb-user-link-photo mr5" />
				</el-tooltip>
				{{ userInfos.realName == '' ? userInfos.account : userInfos.realName }}
				<el-icon class="el-icon--right">
					<ele-ArrowDown />
				</el-icon>
			</span>
			<template #dropdown>
				<el-dropdown-menu>
					<!-- <el-dropdown-item command="/dashboard/home">{{ $t('message.user.dropdown1') }}</el-dropdown-item> -->
					<el-dropdown-item :icon="Avatar" command="/system/userCenter">{{ $t('message.user.dropdown2') }}</el-dropdown-item>
					<el-dropdown-item :icon="Loading" command="clearCache">{{ $t('message.user.dropdown3') }}</el-dropdown-item>
					<el-dropdown-item :icon="Lock" divided command="lockScreen">{{ $t('message.layout.threeIsLockScreen') }}</el-dropdown-item>
					<el-dropdown-item :icon="CircleCloseFilled" divided command="logOut">{{ $t('message.user.dropdown5') }}</el-dropdown-item>
				</el-dropdown-menu>
			</template>
		</el-dropdown>

		<Search ref="searchRef" />
		<OnlineUser ref="onlineUserRef" />
		<ChangePassword ref="changePasswordRef" />
	</div>
</template>

<script setup lang="ts" name="layoutBreadcrumbUser">
import { defineAsyncComponent, ref, computed, reactive, onMounted } from 'vue';
import { ElMessageBox, ElMessage, ElNotification } from 'element-plus';
import { Avatar, CircleCloseFilled, Loading, Lock } from '@element-plus/icons-vue';
import { useRouter } from 'vue-router';
import screenfull from 'screenfull';
import { useI18n } from 'vue-i18n';
import { storeToRefs } from 'pinia';
import { useUserInfo } from '/@/stores/userInfo';
import { useThemeConfig } from '/@/stores/themeConfig';
import { useRoutesList } from '/@/stores/routesList';
import other from '/@/utils/other';
import mittBus from '/@/utils/mitt';
import { Local, Session } from '/@/utils/storage';
import Push from 'push.js';
import { signalR } from '/@/views/system/onlineUser/signalR';
import { languageList, getCountryCode } from '/@/i18n';
import '/node_modules/flag-icons/css/flag-icons.min.css';

import { clearAccessTokens, getAPI } from '/@/utils/axios-utils';
import { SysAuthApi, SysCommonApi, SysNoticeApi, SysUserApi } from '/@/api-services/system/api';

// 引入组件
const UserNews = defineAsyncComponent(() => import('/@/layout/navBars/topBar/userNews.vue'));
const Search = defineAsyncComponent(() => import('/@/layout/navBars/topBar/search.vue'));
const OnlineUser = defineAsyncComponent(() => import('/@/views/system/onlineUser/index.vue'));
const ChangePassword = defineAsyncComponent(() => import('/@/views/system/user/component/changePassword.vue'));
const routeStores = useRoutesList();

// 定义变量内容
const { locale, t } = useI18n();
const router = useRouter();
const stores = useUserInfo();
const storesThemeConfig = useThemeConfig();
const { userInfos } = storeToRefs(stores);
const { themeConfig } = storeToRefs(storesThemeConfig);
const searchRef = ref();
const onlineUserRef = ref();
const changePasswordRef = ref();
const upgradeInfoRef = ref();
const currentCountryCode = ref<string>(getCountryCode(themeConfig.value.globalI18n));
const state = reactive({
	isScreenfull: false,
	disabledI18n: 'zh-cn',
	disabledSize: 'large',
	noticeList: [] as any, // 站内信列表
	tagsRefsIndex: 0,
	tagsViewList: [],
	tagsViewRoutesList: [],
});

// 设置分割样式
const layoutUserFlexNum = computed(() => {
	let num: string | number = '';
	const { layout, isClassicSplitMenu } = themeConfig.value;
	const layoutArr: string[] = ['defaults', 'columns'];
	if (layoutArr.includes(layout) || (layout === 'classic' && !isClassicSplitMenu)) num = '1';
	else num = '';
	return num;
});
// 是否有未读消息
const hasUnreadNotice = computed(() => {
	return state.noticeList.some((r: any) => r.readStatus == undefined || r.readStatus == 0);
});
// 全屏点击时
const onScreenfullClick = () => {
	if (!screenfull.isEnabled) {
		ElMessage.warning('暂不不支持全屏');
		return false;
	}
	screenfull.toggle();
	screenfull.on('change', () => {
		if (screenfull.isFullscreen) state.isScreenfull = true;
		else state.isScreenfull = false;
	});
};
// 布局配置 icon 点击时
const onLayoutSetingClick = () => {
	mittBus.emit('openSettingsDrawer');
};
// 下拉菜单点击时
const onHandleCommandClick = (path: string) => {
	if (path === 'clearCache') {
		Session.clear();
		Local.clear();
		window.location.reload();
	} else if (path === 'lockScreen') {
		Local.remove('themeConfig');
		themeConfig.value.isLockScreen = true;
		themeConfig.value.lockScreenTime = 1;
		Local.set('themeConfig', themeConfig.value);
	} else if (path === 'logOut') {
		ElMessageBox({
			closeOnClickModal: false,
			closeOnPressEscape: false,
			title: t('message.user.logOutTitle'),
			message: t('message.user.logOutMessage'),
			type: 'warning',
			showCancelButton: true,
			confirmButtonText: t('message.user.logOutConfirm'),
			cancelButtonText: t('message.user.logOutCancel'),
			// buttonSize: 'default',
			beforeClose: async (action, instance, done) => {
				if (action === 'confirm') {
					instance.confirmButtonLoading = true;
					instance.confirmButtonText = t('message.user.logOutExit');
					try {
						await getAPI(SysAuthApi).apiSysAuthLogoutPost();
					} catch (error) {
						console.error(error);
					}
					instance.confirmButtonLoading = false;
					done();
				} else {
					done();
				}
			},
		})
			.then(async () => {
				clearAccessTokens();
			})
			.catch(() => {});
	} else {
		router.push(path);
	}
};
// 菜单搜索点击
const onSearchClick = () => {
	searchRef.value.openSearch();
};
// 在线用户列表
const onOnlineUserClick = () => {
	onlineUserRef.value.openDrawer();
};
// 组件大小改变
const onComponentSizeChange = (size: string) => {
	Local.remove('themeConfig');
	themeConfig.value.globalComponentSize = size;
	Local.set('themeConfig', themeConfig.value);
	initI18nOrSize('globalComponentSize', 'disabledSize');
};

// 语言切换
const onLanguageChange = async (lang: string) => {
	Local.remove('themeConfig');
	themeConfig.value.globalI18n = lang;
	Local.set('themeConfig', themeConfig.value);
	currentCountryCode.value = getCountryCode(lang);
	locale.value = lang;
	other.useTitle();
	initI18nOrSize('globalI18n', 'disabledI18n');
	refreshCurrentTabpage();

	if (lang == 'en') window.$changeLang('en');
	else window.$changeLang('zhcn');

	// ★ 本仓库后端**没有** SetCulture 端点（已核对该后端源码，无 SetCulture 方法），
	//   生成物 SysCommonApi 里自然也没有 apiSysCommonSetCulturePost。
	//   这一行运行时必然是 TypeError（未处理的 promise rejection）。
	//   上面两行 window.$changeLang 已经把界面语言切好了，所以去掉这一行不损失功能。
	//   若将来后端补上该端点，取消注释即可。
	// await getAPI(SysCommonApi).apiSysCommonSetCulturePost(lang);
};

// 刷新当前标签页
const refreshCurrentTabpage = () => {
	mittBus.emit('onCurrentContextmenuClick', { ...routeStores.currentRoute, contextMenuClickId: 0 });
};

// 初始化组件大小/i18n
const initI18nOrSize = (value: string, attr: string) => {
	(<any>state)[attr] = Local.get('themeConfig')[value];
};
// 页面加载时
onMounted(async () => {
	if (Local.get('themeConfig')) {
		initI18nOrSize('globalComponentSize', 'disabledSize');
		initI18nOrSize('globalI18n', 'disabledI18n');
	}
	// 手动获取用户桌面通知权限
	if (Push.Permission.GRANTED) {
		// 判断当前是否有权限，没有则手动获取
		Push.Permission.request(undefined, undefined);
	}
	// 监听浏览器 当前系统是否在当前页
	document.addEventListener('visibilitychange', () => {
		if (!document.hidden) {
			// 清空关闭消息通知，
			Push.clear();
		}
	});
	// 加载未读的站内信
	var res = await getAPI(SysNoticeApi).apiSysNoticeUnReadListGet();
	state.noticeList = res.data.result ?? [];

	// 接收站内信
	signalR.on('PublicNotice', receiveNotice);

	// // 处理消息已读
	// mittBus.on('noticeRead', (id) => {
	// 	const notice = state.noticeList.find((r: any) => r.id == id);
	// 	if (notice == undefined) return;

	// 	// 设置已读
	// 	notice.readStatus = 1;
	// });

	// 是否修改密码
	await changePassword();
});
// // 页面卸载时
// onUnmounted(() => {
// 	mittBus.off('noticeRead', () => {});
// });

const receiveNotice = (msg: any) => {
	state.noticeList.unshift(msg);

	ElNotification({
		title: '提示',
		message: '您有一条新消息...',
		type: 'info',
		position: 'bottom-right',
	});
	Push.create('提示', {
		body: '你有一条新的消息',
		icon: '/upload/logo.svg', // 系统 logo（由后端 wwwroot/upload 提供）
		timeout: 4500, // 通知显示时间，单位为毫秒
	});
};

// 修改密码
const changePassword = async () => {
	// 若是超管则不进行强制修改密码和有效期验证
	if (userInfos.value.accountType == 999) return;

	// 等待配置加载完成
	if (themeConfig.value.forceChangePassword == undefined) {
		while (themeConfig.value.forceChangePassword != undefined) {
			await new Promise((resolve) => setTimeout(resolve, 100));
		}
	}
	// 开启强制修改密码
	const enabledForceChangePassword = themeConfig.value.forceChangePassword ?? false;
	if (enabledForceChangePassword && !userInfos.value.lastChangePasswordTime) {
		changePasswordRef.value?.openDialog();
		return;
	}

	// 验证密码有效期
	const passwordExpirationTime = themeConfig.value.passwordExpirationTime ?? 0;
	if (passwordExpirationTime > 0) {
		const res = await getAPI(SysUserApi).apiSysUserVerifyPwdExpirationTimePost();
		if (!res.data.result) {
			changePasswordRef.value?.openDialog();
		}
	}
};
</script>

<style scoped lang="scss">
.layout-navbars-breadcrumb-user {
	display: flex;
	align-items: center;
	justify-content: flex-end;

	&-link {
		height: 100%;
		display: flex;
		align-items: center;
		white-space: nowrap;

		&-photo {
			width: 25px;
			height: 25px;
			border-radius: 100%;
		}
	}

	&-icon {
		padding: 0 10px;
		cursor: pointer;
		color: var(--next-bg-topBarColor);
		height: 50px;
		line-height: 50px;
		display: flex;
		align-items: center;

		&:hover {
			background: var(--next-color-user-hover);

			i {
				display: inline-block;
				animation: logoAnimation 0.3s ease-in-out;
			}
		}
	}

	:deep(.el-dropdown) {
		color: var(--next-bg-topBarColor);
	}

	:deep(.el-badge) {
		height: 40px;
		line-height: 40px;
		display: flex;
		align-items: center;
	}

	:deep(.el-badge__content.is-fixed) {
		top: 12px;
	}
}
</style>
