<template>
	<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="600px">
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
					<el-form-item label="账号信息" prop="accountInfo">
						<el-input v-model="state.ruleForm.accountInfo" placeholder="卡号 / 账号 / 收款码文本，与图片至少填一个" clearable />
					</el-form-item>
				</el-col>
				<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
					<el-form-item label="收款码图片">
						<div class="qr-upload">
							<el-image v-if="state.ruleForm.qrImageUrl" :src="state.ruleForm.qrImageUrl" :preview-src-list="[state.ruleForm.qrImageUrl]" fit="contain" class="qr-preview" preview-teleported />
							<div v-else class="qr-empty">未上传</div>
							<div class="qr-actions">
								<el-upload :show-file-list="false" :auto-upload="false" accept=".jpg,.jpeg,.png,.bmp,.webp" :on-change="onQrChange">
									<el-button type="primary" :loading="state.uploading" v-auth="'payAccount/uploadQr'">{{ state.ruleForm.qrImageUrl ? '更换图片' : '上传图片' }}</el-button>
								</el-upload>
								<el-button v-if="state.ruleForm.qrImageUrl" @click="clearQr">清除</el-button>
							</div>
						</div>
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
						<el-input v-model="state.ruleForm.remark" type="textarea" :rows="3" placeholder="备注（可空）" />
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
import { computed, onMounted, reactive, ref } from 'vue';
import { ElMessage, UploadFile } from 'element-plus';

import { getAPI } from '/@/utils/axios-utils';
import { PayAccountApi } from '/@/api-services/system/api';
import { PayTypeOption } from '/@/api-services/system/models';

const props = defineProps({
	title: String,
});
const emits = defineEmits(['handleQuery']);
const ruleFormRef = ref();
const state = reactive({
	isShowDialog: false,
	submitting: false,
	ruleForm: {} as any,
	typeOptions: [] as Array<PayTypeOption>,
	uploading: false,
});

// 有 Id 即编辑态（类型仍不可改；账号文本 / 收款码 / 备注 / 状态可改）
const isEdit = computed(() => state.ruleForm.id != undefined && state.ruleForm.id > 0);

onMounted(async () => {
	const res = await getAPI(PayAccountApi).apiPayAccountTypeOptionsGet();
	state.typeOptions = res.data.result ?? [];
});

// 打开弹窗
const openDialog = (row: any) => {
	state.ruleForm = JSON.parse(JSON.stringify(row));
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
		const accountInfo = (state.ruleForm.accountInfo || '').trim();
		const qrImageUrl = (state.ruleForm.qrImageUrl || '').trim();
		if (!accountInfo && !qrImageUrl) {
			ElMessage.error('请填写账号信息或上传收款码图片');
			return;
		}
		state.submitting = true;
		try {
			if (isEdit.value) {
				await getAPI(PayAccountApi).apiPayAccountUpdatePost({
					id: state.ruleForm.id,
					accountInfo,
					remark: state.ruleForm.remark,
					status: state.ruleForm.status,
					qrImageUrl,
				});
			} else {
				await getAPI(PayAccountApi).apiPayAccountAddPost({
					type: state.ruleForm.type,
					accountInfo,
					qrImageUrl,
					totalQuota: state.ruleForm.totalQuota,
					remark: state.ruleForm.remark,
				});
			}
			closeDialog();
		} finally {
			state.submitting = false;
		}
	});
};

const onQrChange = async (file: UploadFile) => {
	if (!file.raw) return;
	const allow = ['image/jpeg', 'image/jpg', 'image/png', 'image/bmp', 'image/webp'];
	if (file.raw.type && !allow.includes(file.raw.type)) {
		ElMessage.error('只支持 jpg、png、bmp、webp');
		return;
	}
	state.uploading = true;
	try {
		const res = await getAPI(PayAccountApi).apiPayAccountUploadQrPostForm(file.raw);
		state.ruleForm.qrImageUrl = res.data.result ?? '';
	} finally {
		state.uploading = false;
	}
};

const clearQr = () => {
	state.ruleForm.qrImageUrl = '';
};

// 导出对象
defineExpose({ openDialog });
</script>

<style scoped>
.qr-upload {
	display: flex;
	align-items: center;
	gap: 12px;
}
.qr-preview,
.qr-empty {
	width: 96px;
	height: 96px;
	border: 1px dashed var(--el-border-color);
	border-radius: 4px;
}
.qr-empty {
	display: flex;
	align-items: center;
	justify-content: center;
	color: var(--el-text-color-secondary);
	font-size: 12px;
}
.qr-actions {
	display: flex;
	flex-direction: column;
	gap: 8px;
}
</style>
