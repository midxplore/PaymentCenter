<template>
	<el-form size="large" class="login-content-form">
		<el-form-item class="login-animation1">
			<el-input text :placeholder="$t('message.mobile.placeholder1')" v-model="state.ruleForm.phone" clearable autocomplete="off" :disabled="!!state.totpTicket && !state.totpSetupActive">
				<template #prefix>
					<i class="iconfont icon-dianhua el-input__icon"></i>
				</template>
			</el-input>
		</el-form-item>
		<el-form-item class="login-animation2">
			<el-col :span="15">
				<el-input text maxlength="6" :placeholder="$t('message.mobile.placeholder2')" v-model="state.ruleForm.code" clearable autocomplete="off" :disabled="!!state.totpTicket && !state.totpSetupActive">
					<template #prefix>
						<el-icon class="el-input__icon"><ele-Position /></el-icon>
					</template>
				</el-input>
			</el-col>
			<el-col :span="1"></el-col>
			<el-col :span="8">
				<el-button v-waves class="login-content-code" :loading="state.loading" :disabled="state.disabled || (!!state.totpTicket && !state.totpSetupActive)" @click="getSmsCode">
					{{ state.btnText }}
				</el-button>
			</el-col>
		</el-form-item>
		<el-form-item class="login-animation2">
			<el-input
				ref="totpRef"
				v-model="state.ruleForm.totpCode"
				text
				maxlength="6"
				inputmode="numeric"
				autocomplete="one-time-code"
				placeholder="动态验证码"
				clearable
				@input="onTotpInput"
				@keyup.enter="onSignIn"
			>
				<template #prefix>
					<el-icon><ele-Key /></el-icon>
				</template>
			</el-input>
		</el-form-item>
		<el-form-item class="login-animation3">
			<el-button round type="primary" icon="ele-Promotion" v-waves class="login-content-submit" :loading="state.signInLoading" @click="onSignIn">
				<span>{{ state.totpTicket && !state.totpSetupActive ? '验证并登录' : $t('message.mobile.btnText') }}</span>
			</el-button>
			<button v-if="state.totpTicket && !state.totpSetupActive" type="button" class="totp-back-link" @click="onTotpBack">返回重新登录</button>
		</el-form-item>
		<div class="font12 login-animation4 login-msg">{{ $t('message.mobile.msgText') }}</div>
	</el-form>
	<TotpDialog v-model="state.totpSetupActive" :ticket="state.totpTicket" @success="onTotpSuccess" @back="onTotpBack" />
</template>

<script setup lang="ts" name="loginMobile">
import { reactive, computed, ref } from 'vue';
import { ElMessage, InputInstance } from 'element-plus';
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
import TotpDialog from '/@/views/login/component/totpDialog.vue';
import { verifyTotp } from '/@/views/login/totpApi';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const btnText = t('message.mobile.codeText');
const phoneText = t('message.mobile.placeholder1');
const codeText = t('message.mobile.codeText');
const loginFail = t('message.mobile.loginfail');
const notPrivilege = t('message.account.notprivilege');
const totpRef = ref<InputInstance>();

const state = reactive({
	ruleForm: {
		phone: '',
		code: '',
		totpCode: '',
	},
	btnText: btnText,
	loading: false,
	signInLoading: false,
	disabled: false,
	timer: null as any,
	totpSetupActive: false,
	totpTicket: '',
});

const onTotpInput = () => {
	state.ruleForm.totpCode = String(state.ruleForm.totpCode || '')
		.replace(/\D/g, '')
		.slice(0, 6);
};

// 获取短信验证码
const getSmsCode = async () => {
	state.ruleForm.code = '';
	if (!verifyPhone(state.ruleForm.phone)) {
		ElMessage.error(phoneText);
		return;
	}

	await getAPI(SysSmsApi).apiSysSmsSendSmsPhoneNumberTemplateIdPost(state.ruleForm.phone, '0');

	state.disabled = true;
	state.timer && clearInterval(state.timer);

	var duration = 60;
	state.timer = setInterval(() => {
		duration--;
		state.btnText = t('message.mobile.retry', { duration });
		if (duration <= 0) {
			state.btnText = codeText;
			state.disabled = false;
			clearInterval(state.timer);
		}
	}, 1000);
};

// 登录
const onSignIn = async () => {
	if (state.totpSetupActive) return;

	if (state.totpTicket) {
		const code = state.ruleForm.totpCode.trim();
		if (!/^\d{6}$/.test(code)) {
			ElMessage.warning('请输入 6 位动态验证码');
			totpRef.value?.focus();
			return;
		}
		try {
			state.signInLoading = true;
			const login = await verifyTotp(state.totpTicket, code);
			if (!login.accessToken) {
				ElMessage.error(loginFail);
				return;
			}
			state.totpTicket = '';
			await saveTokenAndInitRoutes(login.accessToken);
		} catch (e: any) {
			ElMessage.error(e?.message || '动态码不正确');
			state.ruleForm.totpCode = '';
			totpRef.value?.focus();
		} finally {
			state.signInLoading = false;
		}
		return;
	}

	try {
		state.signInLoading = true;
		var res = await getAPI(SysAuthApi).apiSysAuthLoginPhonePost({
			...state.ruleForm,
			totpCode: state.ruleForm.totpCode || undefined,
		} as any);
		const result = res.data.result;
		if (result?.needTotpSetup) {
			state.totpTicket = result.totpTicket || '';
			state.totpSetupActive = true;
			return;
		}
		if (result?.needTotpVerify) {
			state.totpTicket = result.totpTicket || '';
			ElMessage.info('请输入身份验证器中的 6 位动态码');
			totpRef.value?.focus();
			return;
		}
		if (result?.accessToken == undefined) {
			ElMessage.error(loginFail);
			return;
		}
		await saveTokenAndInitRoutes(result.accessToken);
	} finally {
		state.signInLoading = false;
	}
};

const onTotpSuccess = async (login: { accessToken?: string | null }) => {
	if (!login.accessToken) {
		ElMessage.error(loginFail);
		return;
	}
	state.totpSetupActive = false;
	state.totpTicket = '';
	await saveTokenAndInitRoutes(login.accessToken);
};

const onTotpBack = () => {
	state.totpSetupActive = false;
	state.totpTicket = '';
	state.ruleForm.totpCode = '';
};

// 保持Token并初始化路由
const saveTokenAndInitRoutes = async (accessToken: string | any) => {
	Local.set(accessTokenKey, accessToken);
	Session.set('token', accessToken);
	const isNoPower = await initBackEndControlRoutes();
	signInSuccess(isNoPower);
};

const signInSuccess = (isNoPower: boolean | undefined) => {
	if (isNoPower) {
		ElMessage.warning(notPrivilege);
		clearTokens();
	} else {
		let currentTimeInfo = currentTime.value;
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
		const signInText = t('message.signInText');
		ElMessage.success(`${currentTimeInfo}，${signInText}`);
		NextLoading.start();
	}
};

const currentTime = computed(() => {
	return formatAxis(new Date(), t);
});
</script>

<style scoped>
.login-msg {
	margin-top: 12px;
	color: var(--el-text-color-placeholder);
	line-height: 1.5;
	word-break: break-word;
}
.totp-back-link {
	display: block;
	width: 100%;
	margin-top: 10px;
	border: none;
	background: transparent;
	color: var(--el-color-primary);
	cursor: pointer;
	font-size: 13px;
}
</style>
