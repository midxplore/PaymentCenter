import { Local } from '/@/utils/storage';
import { storeToRefs } from 'pinia';
import { useThemeConfig } from '/@/stores/themeConfig';
import logoImg from '/@/assets/logo.svg';
import { updateIdleTimeout } from '/@/utils/idleTimeout';

import { feature, getAPI } from '/@/utils/axios-utils';
import { SysTenantApi } from '/@/api-services/system/api';

const storesThemeConfig = useThemeConfig();
const { themeConfig } = storeToRefs(storesThemeConfig);

// 加载系统信息
export async function loadSysInfo(tenantid: number) {
	const [err, res] = await feature(getAPI(SysTenantApi).apiSysTenantSysInfoTenantIdGet(Number(tenantid)));
	if (err) {
		// 默认 logo 地址
		themeConfig.value.logoUrl = logoImg;
		// 保存配置
		Local.remove('themeConfig');
		Local.set('themeConfig', storesThemeConfig.themeConfig);
		return;
	} else {
		if (res.data.type != 'success' || res.data.result == null) return;

		const data = res.data.result;
		// 系统logo
		themeConfig.value.logoUrl = data.logo;
		// 主标题
		themeConfig.value.globalTitle = data.title;
		// 副标题
		themeConfig.value.globalViceTitle = data.viceTitle;
		// 系统说明
		themeConfig.value.globalViceTitleMsg = data.viceDesc;
		// Icp备案信息
		themeConfig.value.icp = data.icp;
		themeConfig.value.icpUrl = data.icpUrl;
		// 水印
		themeConfig.value.isWatermark = data.watermark != null;
		themeConfig.value.watermarkText = data.watermark;
		// 版权说明
		themeConfig.value.copyright = data.copyright;
		// 版本号
		themeConfig.value.version = data.version;
		// 轮播图集合
		themeConfig.value.carouselFiles = data.carouselFiles;
		// 全局主题
		themeConfig.value.primary = data.themeColor;
		// 布局切换
		themeConfig.value.layout = data.layout;
		// 面切动画
		themeConfig.value.animation = data.animation;
		// 登录验证
		themeConfig.value.secondVer = data.secondVer;
		themeConfig.value.captcha = data.captcha;
		// 开启强制修改密码
		themeConfig.value.forceChangePassword = data.forceChangePassword;
		// 是否验证密码有效期
		themeConfig.value.passwordExpirationTime = data.passwordExpirationTime;
		// 开启多语言切换
		themeConfig.value.i18NSwitch = data.i18NSwitch;
		// 闲置超时时间
		themeConfig.value.idleTimeout = data.idleTimeout;
		// 上线下线通知
		themeConfig.value.onlineNotice = data.onlineNotice;
		// 密码加解密公匙
		window.__env__.VITE_SM_PUBLIC_KEY = data.publicKey;

		// 更新 favicon
		updateFavicon(data.logo);

		// 更新空闲超时时间
		updateIdleTimeout(themeConfig.value.idleTimeout ?? 0);

		// 保存配置
		Local.remove('themeConfig');
		Local.set('themeConfig', storesThemeConfig.themeConfig);
	}
}

// 更新 favicon
export const updateFavicon = (url: string): void => {
	const favicon = document.getElementById('favicon') as HTMLAnchorElement;
	favicon!.href = url ? url : 'data:;base64,=';
};
