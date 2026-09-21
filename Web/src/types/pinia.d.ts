/**
 * pinia 类型定义
 */

// 用户信息
declare interface UserInfos<T = any> {
	authApiList: string[];
	photo: string;
	roles: T[];
	time: number;
	userName: string;
	[key: string]: T;
}
declare interface UserInfosState {
	userInfos: UserInfos;
	constList: T[];
	dictList: T;
	userTableList: T[];
}

// 路由缓存列表
declare interface KeepAliveNamesState {
	keepAliveNames: string[];
	cachedViews: string[];
}

// 后端返回原始路由(未处理时)
declare interface RequestOldRoutesState {
	requestOldRoutes: string[];
}

// TagsView 路由列表
declare interface TagsViewRoutesState<T = any> {
	tagsViewRoutes: T[];
	isTagsViewCurrenFull: Boolean;
}

// 路由列表
declare interface RoutesListState<T = any> {
	routesList: T[];
	isColumnsMenuHover: Boolean;
	isColumnsNavHover: Boolean;
	currentRoute: T;
}

// 布局配置
declare interface ThemeConfigState {
	themeConfig: {
		isDrawer: boolean; // 是否开启抽屉配置
		primary: string; // 主题颜色
		topBar: string; // 顶部栏背景
		topBarColor: string; // 顶部栏背景色
		isTopBarColorGradual: boolean; // 是否顶部栏背景渐变
		menuBar: string; // 侧边栏菜单栏背景
		menuBarColor: string; // 侧边栏菜单栏背景色
		menuBarActiveColor: string; // 侧边栏激活项背景色
		isMenuBarColorGradual: boolean; // 是否侧边栏菜单栏背景渐变
		columnsMenuBar: string; // 侧边栏菜单栏背景
		columnsMenuBarColor: string; // 侧边栏菜单栏背景
		isColumnsMenuBarColorGradual: boolean; // 是否侧边栏菜单栏背景渐变
		isColumnsMenuHoverPreload: boolean; // 是否鼠标悬停预加载路由
		columnsLogoHeight: number; // 侧边栏logo高度
		columnsMenuWidth: number; // 侧边栏宽度
		columnsMenuHeight: number; // 侧边栏高度
		isCollapse: boolean; // 是否水平折叠收起菜单(支持手机端)
		isUniqueOpened: boolean; // 是否只保持一个菜单的展开
		isFixedHeader: boolean; // 是否固定头部
		isFixedHeaderChange: boolean; // 是否固定头部
		isClassicSplitMenu: boolean; // 是否分割菜单
		isLockScreen: boolean; // 是否开启锁屏
		lockScreenTime: number; //  锁屏时间
		isShowLogo: boolean; // 是否显示logo
		isShowLogoChange: boolean; // 是否显示logo动画
		isBreadcrumb: boolean; // 是否显示面包屑
		isTagsview: boolean; // 是否显示多标签页
		isBreadcrumbIcon: boolean; // 是否显示面包屑图标
		isTagsviewIcon: boolean; // 是否显示多标签页图标
		isCacheTagsView: boolean; // 是否缓存 TagsView
		isSortableTagsView: boolean; // 是否开启拖拽排序
		isShareTagsView: boolean; // 是否开启多标签页缓存
		isFooter: boolean; // 是否显示页脚
		isGrayscale: boolean; // 是否灰度模式
		isInvert: boolean; // 是否色弱模式
		isIsDark: boolean; // 是否暗黑模式
		isWatermark: boolean; // 是否开启水印
		watermarkText: string; // 水印内容
		tagsStyle: string; // 标签页主题
		animation: string; // 动画
		columnsAsideStyle: string; // 侧边栏主题
		columnsAsideLayout: string; // 侧边栏布局
		layout: string; // 布局模式
		isRequestRoutes: boolean; // 是否开启路由懒加载
		globalI18n: string; // 是否开启国际化
		globalComponentSize: string; // 全局组件大小
		globalTitle: string; // 全局标题
		globalViceTitle: string; // 全局副标题
		globalViceTitleMsg: string; // 全局副标题消息
		copyright: string; // 版权信息
		logoUrl: string; // 系统 logo 地址
		icp: string; // Icp备案号
		icpUrl: string; // Icp地址
		version?: string; // 版本号
		carouselFiles?: any[]; // 轮播图集合
		secondVer?: boolean; // 是否开启二级验证
		captcha?: boolean; // 是否开启验证码
		mobileLogin?: boolean; // 是否开启手机号登录（本期关闭；组件与接口保留）
		forceChangePassword?: boolean; // 是否开启强制修改密码
		passwordExpirationTime?: number; // 是否验证密码有效期
		i18NSwitch: boolean; // 是否开启多语言切换
		idleTimeout: number; // 闲置超时时间
		onlineNotice: boolean; // 上线下线通知
	};
}
