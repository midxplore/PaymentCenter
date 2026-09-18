<template>
	<el-form size="large" class="login-content-form">
		<el-form-item class="login-animation1">
			<el-input text :placeholder="$t('message.mobile.placeholder1')" v-model="state.ruleForm.phone" clearable autocomplete="off">
				<template #prefix>
					<i class="iconfont icon-dianhua el-input__icon"></i>
				</template>
			</el-input>
		</el-form-item>
		<el-form-item class="login-animation2">
			<el-col :span="15">
				<el-input text maxlength="6" :placeholder="$t('message.mobile.placeholder2')" v-model="state.ruleForm.code" clearable autocomplete="off">
					<template #prefix>
						<el-icon class="el-input__icon"><ele-Position /></el-icon>
					</template>
				</el-input>
			</el-col>
			<el-col :span="1"></el-col>
			<el-col :span="8">
				<el-button v-waves class="login-content-code" :loading="state.loading" :disabled="state.disabled" @click="getSmsCode">
					{{ state.btnText }}
				</el-button>
			</el-col>
		</el-form-item>
		<el-form-item class="login-animation3">
			<el-button round type="primary" icon="ele-Promotion" v-waves class="login-content-submit" @click="onSignIn">
				<span>{{ $t('message.mobile.btnText') }}</span>
			</el-button>
		</el-form-item>
		<div class="font12 mt30 login-animation4 login-msg">{{ $t('message.mobile.msgText') }}</div>
	</el-form>
</template>

<script setup lang="ts" name="loginMobile">
import { reactive, computed } from 'vue';
import { ElMessage } from 'element-plus';
import { verifyPhone } from '/@/utils/toolsValidate';
import { useRoute, useRouter } from 'vue-router';

import { useI18n } from 'vue-i18n';
import { storeToRefs } from 'pinia';
import { NextLoading } from '/@/utils/loading';
import { formatAxis } from '/@/utils/formatTime';
import { Local, Session } from '/@/utils/storage';
import { useRoutesList } from '/@/stores/routesList';
import { initBackEndControlRoutes } from '/@/router/backEnd';

import { accessTokenKey, clearTokens, getAPI } from '/@/utils/axios-utils';
import { SysSmsApi, SysAuthApi } from '/@/api-services/system/api';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const btnText = t('message.mobile.codeText');
const phoneText = t('message.mobile.placeholder1');
const codeText = t('message.mobile.codeText');
const loginFail = t('message.mobile.loginfail');
const notPrivilege = t('message.account.notprivilege');

const state = reactive({
	ruleForm: {
		phone: '',
		code: '',
	},
	btnText: btnText,
	loading: false,
	disabled: false,
	timer: null as any,
});

// 获取短信验证码
const getSmsCode = async () => {
	state.ruleForm.code = '';
	if (!verifyPhone(state.ruleForm.phone)) {
		ElMessage.error(phoneText);
		return;
	}

	await getAPI(SysSmsApi).apiSysSmsSendSmsPhoneNumberTemplateIdPost(state.ruleForm.phone, '0');

	// 倒计时期间禁止点击
	state.disabled = true;

	// 清除定时器
	state.timer && clearInterval(state.timer);

	// 开启定时器
	var duration = 60;
	state.timer = setInterval(() => {
		duration--;
		state.btnText = t('message.mobile.retry', { duration });
		if (duration <= 0) {
			state.btnText = codeText;
			state.disabled = false; // 恢复按钮可以点击
			clearInterval(state.timer); // 清除掉定时器
		}
	}, 1000);
};

// 登录
const onSignIn = async () => {
	const host = route.query.host ?? location.host;
	var res = await getAPI(SysAuthApi).apiSysAuthLoginPhonePost({ ...state.ruleForm });
	if (res.data.result?.accessToken == undefined) {
		ElMessage.error(loginFail);
		return;
	}

	// 系统登录
	await saveTokenAndInitRoutes(res.data.result?.accessToken);
};

// 保持Token并初始化路由
const saveTokenAndInitRoutes = async (accessToken: string | any) => {
	// 缓存token
	Local.set(accessTokenKey, accessToken);
	// Local.set(refreshAccessTokenKey, refreshAccessToken);
	Session.set('token', accessToken);

	// 添加完动态路由再进行router跳转，否则可能报错 No match found for location with path "/"
	const isNoPower = await initBackEndControlRoutes();
	signInSuccess(isNoPower); // 再执行 signInSuccess
};

// 登录成功后的跳转
const signInSuccess = (isNoPower: boolean | undefined) => {
	if (isNoPower) {
		ElMessage.warning(notPrivilege);
		clearTokens(); // 清空Token缓存
	} else {
		// 初始化登录成功时间问候语
		let currentTimeInfo = currentTime.value;
		// 登录成功，跳到转首页 如果是复制粘贴的路径，非首页/登录页，那么登录成功后重定向到对应的路径中
		if (route.query?.redirect) {
			const stores = useRoutesList();
			const { routesList } = storeToRefs(stores);
			const recursion = (routeList: any[], url: string): boolean | undefined => {
				if (routeList && routeList.length > 0) {
					for (let i = 0; i < routeList.length; i++) {
						if (routeList[i].path === url) return true;
						if (routeList[i]?.children.length > 0) {
							let result = recursion(routeList[i]?.children, url);
							if (result) return true;
						}
					}
				}
			};
			let exist = recursion(routesList.value, route.query?.redirect as string);
			if (exist) {
				router.push({
					path: <string>route.query?.redirect,
					query: Object.keys(<string>route.query?.params).length > 0 ? JSON.parse(<string>route.query?.params) : '',
				});
			} else router.push('/');
		} else {
			router.push('/');
		}

		// 登录成功提示
		const signInText = t('message.signInText');
		ElMessage.success(`${currentTimeInfo}，${signInText}`);
		// 添加 loading，防止第一次进入界面时出现短暂空白
		NextLoading.start();
	}
};

// 获取时间
const currentTime = computed(() => {
	return formatAxis(new Date(), t);
});
</script>

<style scoped lang="scss">
.login-content-form {
	margin-top: 20px;
	@for $i from 1 through 4 {
		.login-animation#{$i} {
			opacity: 0;
			animation-name: error-num;
			animation-duration: 0.5s;
			animation-fill-mode: forwards;
			animation-delay: calc($i/10) + s;
		}
	}
	.login-content-code {
		width: 100%;
		padding: 0;
	}
	.login-content-submit {
		width: 100%;
		letter-spacing: 2px;
		font-weight: 300;
		margin-top: 15px;
	}
	.login-msg {
		color: var(--el-text-color-placeholder);
	}
}
</style>
