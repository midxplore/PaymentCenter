<template>
	<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="720px">
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
				<span> {{ props.title }} </span>
			</div>
		</template>
		<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto">
			<el-row :gutter="10">
				<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="收款类型" prop="type" :rules="[{ required: true, message: '收款类型不能为空', trigger: 'change' }]">
						<!-- 编辑时类型不可改：类型参与匹配语义，改了会让历史订单的账号口径漂移 -->
						<el-select v-model="state.ruleForm.type" placeholder="收款类型" filterable :disabled="isEdit" style="width: 100%">
							<el-option v-for="item in state.typeOptions" :key="item.value" :label="item.label" :value="item.value" />
						</el-select>
					</el-form-item>
				</el-col>
				<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="卖家姓名" prop="payeeName" :rules="[{ required: true, message: '卖家姓名不能为空', trigger: 'blur' }]">
						<el-input v-model="state.ruleForm.payeeName" placeholder="收款人 / 卖家姓名" clearable maxlength="64" />
					</el-form-item>
				</el-col>
				<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="收款账号" prop="accountNo" :rules="accountNoRules">
						<el-input
							v-model="state.ruleForm.accountNo"
							:placeholder="isBank ? '银行卡号' : '账号 / 账号文本（可与收款码二选一）'"
							clearable
							maxlength="128"
							@input="syncAccountInfo"
						/>
					</el-form-item>
				</el-col>
				<template v-if="isBank">
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="开户银行" prop="bankName" :rules="[{ required: true, message: '开户银行不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.bankName" placeholder="开户银行" clearable maxlength="64" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="支行" prop="branchName" :rules="[{ required: true, message: '支行不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.branchName" placeholder="开户支行" clearable maxlength="64" />
						</el-form-item>
					</el-col>
				</template>
				<el-col v-if="!isBank" :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="收款码图片">
						<div
							ref="qrWrapRef"
							class="qr-upload-wrap"
							tabindex="0"
							@paste="onQrPaste"
							@mousedown="focusQrZone"
						>
							<div v-if="state.ruleForm.qrImageUrl" class="qr-preview-row">
								<el-image
									:src="state.ruleForm.qrImageUrl"
									:preview-src-list="[state.ruleForm.qrImageUrl]"
									fit="contain"
									class="qr-preview"
									preview-teleported
								/>
								<el-button @click="clearQr">清除</el-button>
							</div>
							<el-upload
								drag
								class="qr-uploader"
								:show-file-list="false"
								:auto-upload="false"
								accept=".jpg,.jpeg,.png,.bmp,.webp"
								:on-change="onQrChange"
								:disabled="state.uploading"
							>
								<div class="qr-drag-zone">
									<svg class="qr-drag-ico" viewBox="0 0 48 48" width="40" height="40" aria-hidden="true">
										<path
											d="M24 8v22M24 8l-7 7M24 8l7 7M10 36h28"
											fill="none"
											stroke="#9aa3ab"
											stroke-width="2.4"
											stroke-linecap="round"
											stroke-linejoin="round"
										/>
									</svg>
									<p class="qr-drag-title">{{ state.uploading ? '上传中…' : '拖拽、点击选择，或 Ctrl / ⌘ + V 粘贴' }}</p>
									<p class="qr-drag-tip">支持 jpg / png / bmp / webp（可直接粘贴截图）</p>
								</div>
							</el-upload>
						</div>
					</el-form-item>
				</el-col>
				<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="账号类型" prop="accountTier" :rules="[{ required: true, message: '账号类型不能为空', trigger: 'change' }]">
						<el-select v-model="state.ruleForm.accountTier" placeholder="账号类型" style="width: 100%">
							<el-option label="普通账号" :value="1" />
							<el-option label="大额度账号" :value="2" />
						</el-select>
					</el-form-item>
				</el-col>
				<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="最小金额" prop="minAmount">
						<el-input-number v-model="state.ruleForm.minAmount" :min="0" :precision="2" :step="100" style="width: 100%" />
						<div class="field-tip">0 或不填表示不限制；大于 0 时，低于该金额的订单不会匹配到本账号。</div>
					</el-form-item>
				</el-col>
				<el-col v-if="!isEdit" :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="初始总额度" prop="totalQuota" :rules="[{ required: true, message: '初始总额度不能为空', trigger: 'blur' }]">
						<el-input-number v-model="state.ruleForm.totalQuota" :min="0" :precision="2" :step="1000" style="width: 100%" />
					</el-form-item>
				</el-col>
				<el-col v-else :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="账号状态" prop="status" :rules="[{ required: true, message: '账号状态不能为空', trigger: 'change' }]">
						<el-select v-model="state.ruleForm.status" placeholder="账号状态" style="width: 100%">
							<!-- 「已用完」由系统按额度自动判定，不提供手工设置 -->
							<el-option label="启用" :value="1" />
							<el-option label="停用" :value="2" />
						</el-select>
					</el-form-item>
				</el-col>
				<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="备注" prop="remark">
						<Editor
							v-model:get-html="state.ruleForm.remark"
							mode="simple"
							height="200px"
							placeholder="显示在收银台的付款提示（可空，支持简单排版）。例：请勿使用工商银行卡…若已付款请点击【我已付款完成】"
						/>
					</el-form-item>
				</el-col>
			</el-row>
		</el-form>
		<template #footer>
			<span class="dialog-footer">
				<el-button icon="ele-CircleCloseFilled" @click="cancel">取 消</el-button>
				<el-button type="primary" icon="ele-CircleCheckFilled" :loading="state.submitting" @click="submit">确 定</el-button>
			</span>
		</template>
	</el-dialog>
</template>

<script lang="ts" setup name="payAccountEdit">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';
import { ElMessage, UploadFile } from 'element-plus';
import Editor from '/@/components/editor/index.vue';

import { getAPI } from '/@/utils/axios-utils';
import { PayAccountApi } from '/@/api-services/system/api';
import { PayTypeOption } from '/@/api-services/system/models';
import { prepareQrFile, QrPrepareError, qrPrepareMessage } from '../compressQr';

const props = defineProps({
	title: String,
});
const emits = defineEmits(['handleQuery']);
const ruleFormRef = ref();
const qrWrapRef = ref<HTMLElement>();
const state = reactive({
	isShowDialog: false,
	submitting: false,
	ruleForm: {} as any,
	typeOptions: [] as Array<PayTypeOption>,
	uploading: false,
});

// 有 Id 即编辑态（类型仍不可改；账号文本 / 收款码 / 备注 / 状态可改）
const isEdit = computed(() => state.ruleForm.id != undefined && state.ruleForm.id > 0);
const isBank = computed(() => String(state.ruleForm.type || '').toLowerCase() === 'bank');
const accountNoRules = computed(() =>
	isBank.value ? [{ required: true, message: '银行卡账号不能为空', trigger: 'blur' }] : []
);

const QR_ALLOW = ['image/jpeg', 'image/jpg', 'image/png', 'image/bmp', 'image/webp'];

const normalizeRemark = (raw: unknown) => {
	const t = String(raw ?? '').trim();
	if (!t || t === '<p><br></p>' || t === '<p><br/></p>' || t === '<p></p>') return '';
	return t;
};

const focusQrZone = () => {
	qrWrapRef.value?.focus({ preventScroll: true });
};

/** 弹窗打开且非银行卡时：在弹窗内任意处粘贴图片即可上传收款码（编辑器内粘贴文字不受影响） */
const onDialogPaste = (e: ClipboardEvent) => {
	if (!state.isShowDialog || isBank.value || state.uploading) return;
	const target = e.target as HTMLElement | null;
	// 备注编辑器里粘贴文字/HTML 时不抢；只有剪贴板含图片才处理
	const items = e.clipboardData?.items;
	if (!items?.length) return;
	let imageItem: DataTransferItem | null = null;
	for (let i = 0; i < items.length; i++) {
		const it = items[i];
		if (it.kind === 'file' && it.type.startsWith('image/')) {
			imageItem = it;
			break;
		}
	}
	if (!imageItem) return;
	// 已在可编辑区且剪贴板同时有文本时，留给编辑器
	if (target?.closest?.('[contenteditable="true"], textarea, input') && e.clipboardData?.getData('text/plain')) {
		return;
	}
	e.preventDefault();
	void uploadClipboardImage(imageItem);
};

watch(
	() => state.isShowDialog,
	(open) => {
		if (open) {
			document.addEventListener('paste', onDialogPaste, true);
			nextTick(() => {
				if (!isBank.value) focusQrZone();
			});
		} else {
			document.removeEventListener('paste', onDialogPaste, true);
		}
	}
);

onBeforeUnmount(() => {
	document.removeEventListener('paste', onDialogPaste, true);
});

onMounted(async () => {
	const res = await getAPI(PayAccountApi).apiPayAccountTypeOptionsGet();
	state.typeOptions = res.data.result ?? [];
});

const syncAccountInfo = () => {
	state.ruleForm.accountInfo = (state.ruleForm.accountNo || '').trim();
};

// 打开弹窗
const openDialog = (row: any) => {
	state.ruleForm = JSON.parse(JSON.stringify(row));
	// 兼容旧数据：无 accountNo 时回落 accountInfo
	if (!state.ruleForm.accountNo && state.ruleForm.accountInfo) {
		state.ruleForm.accountNo = state.ruleForm.accountInfo;
	}
	if (state.ruleForm.remark == null) state.ruleForm.remark = '';
	if (!state.ruleForm.accountTier) state.ruleForm.accountTier = 1;
	if (state.ruleForm.minAmount == null || state.ruleForm.minAmount === '') state.ruleForm.minAmount = 0;
	syncAccountInfo();
	state.isShowDialog = true;
	ruleFormRef.value?.resetFields();
};

// 关闭弹窗
const closeDialog = () => {
	emits('handleQuery');
	state.isShowDialog = false;
};

// 取消
const cancel = () => {
	state.isShowDialog = false;
};

// 提交
const submit = () => {
	ruleFormRef.value.validate(async (valid: boolean) => {
		if (!valid) return;
		const payeeName = (state.ruleForm.payeeName || '').trim();
		const accountNo = (state.ruleForm.accountNo || '').trim();
		const bankName = (state.ruleForm.bankName || '').trim();
		const branchName = (state.ruleForm.branchName || '').trim();
		const qrImageUrl = (state.ruleForm.qrImageUrl || '').trim();
		const accountInfo = accountNo;
		const remark = normalizeRemark(state.ruleForm.remark);

		if (!payeeName) {
			ElMessage.error('卖家姓名不能为空');
			return;
		}
		if (isBank.value) {
			if (!accountNo || !bankName || !branchName) {
				ElMessage.error('银行卡需填写账号、开户银行与支行');
				return;
			}
		} else if (!accountNo && !qrImageUrl) {
			ElMessage.error('请填写收款账号或上传收款码图片');
			return;
		}

		state.submitting = true;
		try {
			if (isEdit.value) {
				await getAPI(PayAccountApi).apiPayAccountUpdatePost({
					id: state.ruleForm.id,
					payeeName,
					accountNo,
					bankName: isBank.value ? bankName : bankName || null,
					branchName: isBank.value ? branchName : branchName || null,
					accountInfo,
					remark,
					status: state.ruleForm.status,
					accountTier: state.ruleForm.accountTier || 1,
					minAmount: Number(state.ruleForm.minAmount) || 0,
					qrImageUrl,
				});
			} else {
				await getAPI(PayAccountApi).apiPayAccountAddPost({
					type: state.ruleForm.type,
					payeeName,
					accountNo,
					bankName: isBank.value ? bankName : undefined,
					branchName: isBank.value ? branchName : undefined,
					accountInfo,
					qrImageUrl,
					totalQuota: state.ruleForm.totalQuota,
					accountTier: state.ruleForm.accountTier || 1,
					minAmount: Number(state.ruleForm.minAmount) || 0,
					remark,
				});
			}
			closeDialog();
		} finally {
			state.submitting = false;
		}
	});
};

const uploadQrFile = async (raw: File) => {
	const type = (raw.type || '').toLowerCase();
	if (type && !QR_ALLOW.includes(type)) {
		ElMessage.error('只支持 jpg、png、bmp、webp');
		return;
	}
	state.uploading = true;
	try {
		const file = await prepareQrFile(raw);
		const res = await getAPI(PayAccountApi).apiPayAccountUploadQrPostForm(file);
		state.ruleForm.qrImageUrl = res.data.result ?? '';
		ElMessage.success('收款码已上传');
	} catch (e) {
		if (e instanceof QrPrepareError) {
			ElMessage.error(qrPrepareMessage(e.code));
			return;
		}
		throw e;
	} finally {
		state.uploading = false;
	}
};

const onQrChange = async (file: UploadFile) => {
	if (!file.raw) return;
	await uploadQrFile(file.raw);
};

const onQrPaste = async (e: ClipboardEvent) => {
	if (isBank.value || state.uploading) return;
	const items = e.clipboardData?.items;
	if (!items?.length) return;
	for (let i = 0; i < items.length; i++) {
		const it = items[i];
		if (it.kind === 'file' && it.type.startsWith('image/')) {
			e.preventDefault();
			await uploadClipboardImage(it);
			return;
		}
	}
};

const uploadClipboardImage = async (item: DataTransferItem) => {
	const blob = item.getAsFile();
	if (!blob) {
		ElMessage.error('未能读取剪贴板图片');
		return;
	}
	const mime = (blob.type || item.type || 'image/png').toLowerCase();
	if (!QR_ALLOW.includes(mime) && mime !== 'image/jpg') {
		// 部分环境截图是 image/png，偶发无 type：仍按 png 上传
		if (mime && mime !== 'application/octet-stream') {
			ElMessage.error('只支持 jpg、png、bmp、webp');
			return;
		}
	}
	const ext = mime.includes('jpeg') || mime.includes('jpg') ? 'jpg'
		: mime.includes('webp') ? 'webp'
		: mime.includes('bmp') ? 'bmp'
		: 'png';
	const file = new File([blob], `clipboard-qr.${ext}`, { type: mime.startsWith('image/') ? mime : 'image/png' });
	await uploadQrFile(file);
};

const clearQr = () => {
	state.ruleForm.qrImageUrl = '';
};

// 导出对象
defineExpose({ openDialog });
</script>

<style scoped>
.qr-upload-wrap {
	width: 100%;
	outline: none;
}
.qr-upload-wrap:focus .qr-drag-zone {
	border-color: var(--el-color-primary);
}
.qr-preview-row {
	display: flex;
	align-items: center;
	gap: 12px;
	margin-bottom: 10px;
}
.qr-preview {
	width: 96px;
	height: 96px;
	border: 1px dashed var(--el-border-color);
	border-radius: 4px;
}
.qr-uploader {
	width: 100%;
}
.qr-uploader :deep(.el-upload),
.qr-uploader :deep(.el-upload-dragger) {
	width: 100%;
	padding: 0;
	border: none;
	background: transparent;
}
.qr-drag-zone {
	border: 1.5px dashed var(--el-border-color);
	border-radius: 8px;
	padding: 18px 12px;
	text-align: center;
	background: var(--el-fill-color-blank);
	transition: border-color 0.15s ease;
}
.qr-uploader:hover .qr-drag-zone {
	border-color: var(--el-color-primary);
}
.qr-drag-ico {
	margin-bottom: 6px;
}
.qr-drag-title {
	margin: 0 0 4px;
	font-size: 13px;
	color: var(--el-text-color-regular);
}
.qr-drag-tip {
	margin: 0;
	font-size: 12px;
	color: var(--el-text-color-secondary);
}
.field-tip {
	margin-top: 6px;
	font-size: 12px;
	line-height: 1.4;
	color: var(--el-text-color-secondary);
}
</style>
