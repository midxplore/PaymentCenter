<template>
	<el-tooltip :visible="state.capsLockVisible" effect="light" :content="t('message.account.lockTag')" placement="top">
		<el-form ref="ruleFormRef" :model="state.ruleForm" size="large" :rules="state.rules" class="login-content-form">
			<el-form-item class="login-animation1" prop="account">
				<el-input ref="accountRef" text :placeholder="t('message.account.accountneed')" v-model="state.ruleForm.account" clearable autocomplete="off" @keyup.enter.native="handleSignIn">
					<template #prefix>
						<el-icon>
							<ele-User />
						</el-icon>
					</template>
				</el-input>
			</el-form-item>
			<el-form-item class="login-animation2" prop="password">
				<el-input
					ref="passwordRef"
					:type="state.isShowPassword ? 'text' : 'password'"
					:placeholder="t('message.account.passwordneed')"
					v-model="state.ruleForm.password"
					autocomplete="off"
					@keyup.enter.native="handleSignIn"
				>
					<template #prefix>
						<el-icon>
							<ele-Unlock />
						</el-icon>
					</template>
					<template #suffix>
						<i class="iconfont el-input__icon login-content-password" :class="state.isShowPassword ? 'icon-yincangmima' : 'icon-xianshimima'" @click="state.isShowPassword = !state.isShowPassword">
						</i>
					</template>
				</el-input>
			</el-form-item>
			<el-form-item class="login-animation3" prop="captcha" v-if="state.captchaEnabled">
				<el-col :span="15">
					<el-input
						ref="codeRef"
						text
						maxlength="4"
						:placeholder="$t('message.account.accountPlaceholder3')"
						v-model="state.ruleForm.code"
						clearable
						autocomplete="off"
						@keyup.enter.native="handleSignIn"
					>
						<template #prefix>
							<el-icon>
								<ele-Position />
							</el-icon>
						</template>
					</el-input>
				</el-col>
				<el-col :span="1"></el-col>
				<el-col :span="8">
					<div :class="[state.expirySeconds > 0 ? 'login-content-code' : 'login-content-code-expired']" @click="getCaptcha">
						<div style="width: 130px; height: 38px; text-align: center; cursor: pointer" v-if="state.captchaImage == ''">
							<el-icon class="is-loading">
								<ele-Loading />
							</el-icon>
						</div>
						<img class="login-content-code-img" width="130px" height="38px" :src="state.captchaImage" style="cursor: pointer" v-else />
					</div>
				</el-col>
			</el-form-item>
			<el-form-item class="login-animation4">
				<el-button type="primary" icon="ele-Promotion" class="login-content-submit" round v-waves @click="handleSignIn" :loading="state.loading.signIn">
					<span>{{ $t('message.account.accountBtnText') }}</span>
				</el-button>
			</el-form-item>
			<div class="font12 mt30 login-animation4 login-msg">{{ $t('message.mobile.msgText') }}</div>
			<!-- <el-button type="primary" round v-waves @click="weixinSignIn" :loading="state.loading.signIn"></el-button> -->
		</el-form>
	</el-tooltip>
	<div class="dialog-header">
		<el-dialog v-model="state.rotateVerifyVisible" :show-close="false">
			<DragVerifyImgRotate
				ref="dragRef"
				:imgsrc="state.rotateVerifyImg"
				v-model:isPassing="state.isPassRotate"
				:text="t('message.account.splitslive')"
				:successText="t('message.account.success')"
				handlerIcon="fa fa-angle-double-right"
				successIcon="fa fa-hand-peace-o"
				@passcallback="passRotateVerify"
			/>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="loginAccount">
import { reactive, computed, ref, onMounted, defineAsyncComponent, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ElMessage, InputInstance } from 'element-plus';
import { useI18n } from 'vue-i18n';
import { initBackEndControlRoutes } from '/@/router/backEnd';
import { Local, Session } from '/@/utils/storage';
import { formatAxis } from '/@/utils/formatTime';
import { NextLoading } from '/@/utils/loading';
import { loadSysInfo } from '/@/utils/sysInfo';
import { sm2 } from 'sm-crypto-v2';
import { useRoutesList } from '/@/stores/routesList';
import { useThemeConfig } from '/@/stores/themeConfig';
import { storeToRefs } from 'pinia';

import { accessTokenKey, clearTokens, feature, getAPI } from '/@/utils/axios-utils';
import { SysAuthApi } from '/@/api-services/system/api';

// 旋转图片滑块组件
const DragVerifyImgRotate = defineAsyncComponent(() => import('/@/components/dragVerify/dragVerifyImgRotate.vue'));

const storesThemeConfig = useThemeConfig();
const { themeConfig } = storeToRefs(storesThemeConfig);

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();

const ruleFormRef = ref();
const accountRef = ref<InputInstance>();
const passwordRef = ref<InputInstance>();
const codeRef = ref<InputInstance>();
const accountNeed = t('message.account.accountneed');
const passwordNeed = t('message.account.passwordneed');
const codeNeed = t('message.account.codeNeed');
const loginFail = t('message.account.loginfail');
const notPrivilege = t('message.account.notprivilege');

const dragRef: any = ref(null);
const state = reactive({
	isShowPassword: false,
	ruleForm: {
		account: window.__env__.VITE_DEFAULT_USER,
		password: window.__env__.VITE_DEFAULT_USER_PASSWORD,
		code: '',
		codeId: 0 as any,
	},
	rules: {
		account: [{ required: true, message: accountNeed, trigger: 'blur' }],
		password: [{ required: true, message: passwordNeed, trigger: 'blur' }],
		code: [{ required: true, message: codeNeed, trigger: 'blur' }],
	},
	loading: {
		signIn: false,
	},
	captchaImage: '',
	rotateVerifyVisible: false,
	// rotateVerifyImg: verifyImg,
	rotateVerifyImg: themeConfig.value.logoUrl,
	secondVerEnabled: false,
	captchaEnabled: false,
	isPassRotate: false,
	capsLockVisible: false,
	expirySeconds: 60 as any, // 验证码过期时间
});

// 验证码过期计时器
let timer: any = null;

// 页面初始化
onMounted(async () => {
	// 若URL带有Token参数（第三方登录）
	const accessToken = route.query.token;
	if (accessToken) await saveTokenAndInitRoutes(accessToken);

	// 若系统配置为空则获取系统配置
	if (themeConfig.value.captcha == undefined) {
		await loadSysInfo(Local.get('tid') ?? 0);
	}

	// 获取登录配置
	state.secondVerEnabled = themeConfig.value.secondVer ?? false;
	state.captchaEnabled = themeConfig.value.captcha ?? true;

	// 获取验证码
	getCaptcha();

	// 注册验证码过期计时器
	if (state.captchaEnabled) {
		timer = setInterval(() => {
			if (state.expirySeconds > 0) state.expirySeconds -= 1;
		}, 1000);
	}

	// 检测大小写按键/CapsLK
	document.addEventListener('keyup', handleKeyPress);
});

// 页面卸载
onUnmounted(() => {
	// 销毁验证码过期计时器
	clearInterval(timer);
	timer = null;

	document.removeEventListener('keyup', handleKeyPress);
});

// 检测大小写按键
const handleKeyPress = (e: KeyboardEvent) => {
	state.capsLockVisible = e.key === 'CapsLock';
};

// 获取验证码
const getCaptcha = async () => {
	if (!state.captchaEnabled) return;

	state.ruleForm.code = '';
	const res = await getAPI(SysAuthApi)
		.apiSysAuthCaptchaGet()
		.then((res) => res.data.result);
	state.captchaImage = 'data:text/html;base64,' + res?.img;
	state.expirySeconds = res?.expirySeconds;
	state.ruleForm.codeId = res?.id;
};

// 获取时间
const currentTime = computed(() => {
	return formatAxis(new Date(), t);
});

// 登录
const onSignIn = async () => {
	ruleFormRef.value.validate(async (valid: boolean) => {
		if (!valid) return false;

		try {
			state.loading.signIn = true;

			// SM2加密密码
			// const keys = SM2.generateKeyPair();
			const publicKey = window.__env__.VITE_SM_PUBLIC_KEY;
			const password = sm2.doEncrypt(state.ruleForm.password, publicKey, 1);

			const tenantid = route.query.tid ?? 0;
			const [err, res] = await feature(getAPI(SysAuthApi).apiSysAuthLoginPost({ ...state.ruleForm, password: password, tenantId: Number(tenantid) }));
			if (err) {
				getCaptcha(); // 重新获取验证码
				return;
			}
			if (res.data.result?.accessToken == undefined) {
				getCaptcha(); // 重新获取验证码
				ElMessage.error(loginFail);
				return;
			}
			await saveTokenAndInitRoutes(res.data.result?.accessToken);
		} finally {
			state.loading.signIn = false;
		}
	});
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

// 打开旋转验证
const openRotateVerify = () => {
	state.rotateVerifyVisible = true;
	state.isPassRotate = false;
	dragRef.value?.reset();
};

// 通过旋转验证
const passRotateVerify = () => {
	state.rotateVerifyVisible = false;
	state.isPassRotate = true;
	onSignIn();
};

// 登录处理
const handleSignIn = () => {
	if (!state.ruleForm.account) {
		accountRef.value?.focus();
	} else if (!state.ruleForm.password) {
		passwordRef.value?.focus();
	} else if (state.captchaEnabled && !state.ruleForm.code) {
		codeRef.value?.focus();
	} else {
		state.secondVerEnabled ? openRotateVerify() : onSignIn();
	}
};

// // 微信登录
// const weixinSignIn = () => {
// 	window.open('http://localhost:5005/api/sysoauth/signin?provider=Gitee&redirectUrl=http://localhost:8888');
// };

//验证码过期显示文本
const captchaExpired = computed(() => `"${t('message.account.captchaExpired')}"`);

// 导出对象
defineExpose({ saveTokenAndInitRoutes });
</script>

<style lang="scss" scoped>
.dialog-header {
	:deep(.el-dialog) {
		width: unset !important;

		.el-dialog__header {
			display: none;
		}

		.el-dialog__wrapper {
			position: absolute !important;
		}

		.v-modal {
			position: absolute !important;
		}
	}
}

.login-content-form {
	margin-top: 20px;

	@for $i from 0 through 4 {
		.login-animation#{$i} {
			opacity: 0;
			animation-name: error-num;
			animation-duration: 0.5s;
			animation-fill-mode: forwards;
			animation-delay: calc($i/10) + s;
		}
	}

	.login-content-password {
		display: inline-block;
		width: 20px;
		cursor: pointer;

		&:hover {
			color: #909399;
		}
	}

	.login-content-code {
		display: flex;
		align-items: center;
		justify-content: space-around;
		position: relative;

		.login-content-code-img {
			width: 100%;
			height: 40px;
			line-height: 40px;
			background-color: #ffffff;
			border: 1px solid rgb(220, 223, 230);
			cursor: pointer;
			transition: all ease 0.2s;
			border-radius: 4px;
			user-select: none;

			&:hover {
				border-color: #c0c4cc;
				transition: all ease 0.2s;
			}
		}
	}

	.login-content-code-expired {
		@extend .login-content-code;

		&::before {
			content: v-bind(captchaExpired);
			position: absolute;
			top: 0;
			left: 0;
			right: 0;
			bottom: 0;
			border-radius: 4px;
			background-color: rgba(0, 0, 0, 0.5);
			color: #ffffff;
			text-align: center;
		}
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
