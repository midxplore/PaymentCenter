<template>
	<el-dialog
		v-model="dialogVisible"
		title="绑定身份验证器"
		width="440px"
		:close-on-click-modal="false"
		:close-on-press-escape="false"
		:show-close="false"
		destroy-on-close
		@closed="onClosed"
	>
		<p class="hint">请使用 Google Authenticator / Microsoft Authenticator 扫描二维码，再输入应用中的 6 位验证码完成绑定。</p>

		<div v-loading="loadingSetup" class="qr-box">
			<img v-if="qrImage" :src="qrImage" alt="TOTP QR" class="qr-img" />
		</div>

		<div v-if="manualKey" class="manual">
			<span class="manual-label">无法扫码时手动输入密钥</span>
			<div class="manual-row">
				<code>{{ manualKey }}</code>
				<button type="button" class="link-btn" @click="copyKey">复制</button>
			</div>
		</div>

		<div class="totp-code-block">
			<label class="code-label" for="totp-setup-code">身份验证器验证码</label>
			<input
				id="totp-setup-code"
				ref="codeInputRef"
				v-model="code"
				class="code-boxes"
				type="text"
				inputmode="numeric"
				autocomplete="one-time-code"
				maxlength="6"
				placeholder="······"
				@input="onCodeInput"
				@keyup.enter="submit"
			/>
		</div>

		<template #footer>
			<el-button @click="onBack" :disabled="submitting">取消</el-button>
			<el-button type="primary" :loading="submitting" @click="submit">完成绑定并登录</el-button>
		</template>
	</el-dialog>
</template>

<script lang="ts" setup>
import { computed, nextTick, ref, watch } from 'vue';
import { ElMessage } from 'element-plus';
import type { LoginOutput } from '/@/api-services/system/models';
import { confirmTotpSetup, fetchTotpSetup } from '../totpApi';

const props = defineProps<{
	modelValue: boolean;
	ticket: string;
}>();

const emit = defineEmits<{
	(e: 'update:modelValue', v: boolean): void;
	(e: 'success', login: LoginOutput): void;
	(e: 'back'): void;
}>();

const dialogVisible = computed({
	get: () => props.modelValue,
	set: (v: boolean) => emit('update:modelValue', v),
});

const loadingSetup = ref(false);
const submitting = ref(false);
const qrImage = ref('');
const manualKey = ref('');
const code = ref('');
const codeInputRef = ref<HTMLInputElement | null>(null);

const loadSetup = async () => {
	if (!props.ticket) return;
	loadingSetup.value = true;
	qrImage.value = '';
	manualKey.value = '';
	try {
		const data = await fetchTotpSetup(props.ticket);
		qrImage.value = data.qrCodeImage || '';
		manualKey.value = data.manualKey || '';
	} catch (e: any) {
		ElMessage.error(e?.message || '加载绑定信息失败');
		dialogVisible.value = false;
		emit('back');
	} finally {
		loadingSetup.value = false;
		await nextTick();
		codeInputRef.value?.focus();
	}
};

watch(
	() => [props.modelValue, props.ticket] as const,
	([open, ticket]) => {
		if (open && ticket) {
			code.value = '';
			loadSetup();
		}
	}
);

const onCodeInput = () => {
	code.value = (code.value || '').replace(/\D/g, '').slice(0, 6);
};

const copyKey = async () => {
	if (!manualKey.value) return;
	try {
		await navigator.clipboard.writeText(manualKey.value);
		ElMessage.success('密钥已复制');
	} catch {
		ElMessage.warning('复制失败，请手动选择');
	}
};

const submit = async () => {
	if (!/^\d{6}$/.test(code.value)) {
		ElMessage.warning('请输入 6 位验证码');
		return;
	}
	submitting.value = true;
	try {
		const login = await confirmTotpSetup(props.ticket, code.value);
		dialogVisible.value = false;
		emit('success', login);
	} catch (e: any) {
		ElMessage.error(e?.message || '绑定失败');
		code.value = '';
		await nextTick();
		codeInputRef.value?.focus();
	} finally {
		submitting.value = false;
	}
};

const onBack = () => {
	dialogVisible.value = false;
	emit('back');
};

const onClosed = () => {
	code.value = '';
	qrImage.value = '';
	manualKey.value = '';
};
</script>

<style scoped lang="scss">
.hint {
	margin: 0 0 16px;
	font-size: 13px;
	line-height: 1.6;
	color: var(--el-text-color-secondary);
}
.qr-box {
	display: flex;
	justify-content: center;
	min-height: 180px;
	margin-bottom: 12px;
}
.qr-img {
	width: 180px;
	height: 180px;
	border-radius: 8px;
	background: #fff;
}
.manual {
	margin-bottom: 16px;
	padding: 10px 12px;
	border-radius: 8px;
	background: var(--el-fill-color-light);
}
.manual-label {
	display: block;
	margin-bottom: 6px;
	font-size: 12px;
	color: var(--el-text-color-secondary);
}
.manual-row {
	display: flex;
	align-items: center;
	gap: 8px;
	code {
		flex: 1;
		font-size: 13px;
		word-break: break-all;
	}
}
.link-btn {
	border: 0;
	background: transparent;
	color: var(--el-color-primary);
	cursor: pointer;
	font-size: 13px;
	padding: 0;
	&:hover {
		opacity: 0.85;
	}
}
.totp-code-block {
	margin-top: 8px;
}
.code-label {
	display: block;
	margin-bottom: 8px;
	font-size: 13px;
	color: var(--el-text-color-regular);
}
.code-boxes {
	width: 100%;
	height: 48px;
	border: 1px solid var(--el-border-color);
	border-radius: 8px;
	padding: 0 14px;
	font-size: 22px;
	letter-spacing: 0.35em;
	text-align: center;
	outline: none;
	background: var(--el-fill-color-blank);
	color: var(--el-text-color-primary);
	&:focus {
		border-color: var(--el-color-primary);
	}
}
</style>
