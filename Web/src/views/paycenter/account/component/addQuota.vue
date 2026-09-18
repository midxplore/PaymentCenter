<template>
	<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="520px">
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Coin /> </el-icon>
				<span> 追加额度 </span>
			</div>
		</template>

		<el-alert type="info" :closable="false" style="margin-bottom: 12px">
			<template #title>
				追加的是「总额度」。若账号因额度耗尽处于「已用完」，追加后剩余额度恢复为正数会自动回到「启用」重新参与匹配。
			</template>
		</el-alert>

		<el-descriptions :column="1" border size="small" style="margin-bottom: 12px">
			<el-descriptions-item label="账号信息">{{ state.account.accountInfo }}</el-descriptions-item>
			<el-descriptions-item label="总额度">{{ money(state.account.totalQuota) }}</el-descriptions-item>
			<el-descriptions-item label="已用 / 预占">{{ money(state.account.usedQuota) }} / {{ money(state.account.lockedQuota) }}</el-descriptions-item>
			<el-descriptions-item label="剩余可用">{{ money(state.account.remainingQuota) }}</el-descriptions-item>
		</el-descriptions>

		<el-form :model="state.form" ref="ruleFormRef" label-width="auto">
			<el-form-item label="追加金额" prop="quota" :rules="[{ required: true, message: '追加金额不能为空', trigger: 'blur' }]">
				<el-input-number v-model="state.form.quota" :min="0.01" :precision="2" :step="1000" style="width: 100%" />
			</el-form-item>
		</el-form>

		<template #footer>
			<span class="dialog-footer">
				<el-button icon="ele-CircleCloseFilled" @click="cancel">取 消</el-button>
				<el-button type="primary" icon="ele-CircleCheckFilled" :loading="state.submitting" @click="submit">确 定</el-button>
			</span>
		</template>
	</el-dialog>
</template>

<script lang="ts" setup name="payAccountAddQuota">
import { reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';

import { getAPI } from '/@/utils/axios-utils';
import { PayAccountApi } from '/@/api-services/system/api';
import { AddQuotaInput } from '/@/api-services/system/models';

const emits = defineEmits(['handleQuery']);
const ruleFormRef = ref();
const state = reactive({
	isShowDialog: false,
	submitting: false,
	// 被追加的账号（只读展示，用于让操作人确认加的是哪个账号）
	account: {} as any,
	form: { id: 0, quota: 1000 } as AddQuotaInput,
});

// 打开弹窗
const openDialog = (row: any) => {
	state.account = JSON.parse(JSON.stringify(row));
	state.form = { id: row.id, quota: 1000 };
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
		state.submitting = true;
		try {
			await getAPI(PayAccountApi).apiPayAccountAddQuotaPost(state.form);
			ElMessage.success('追加成功');
			closeDialog();
		} finally {
			state.submitting = false;
		}
	});
};

// 金额展示
const money = (value: any) => (value === undefined || value === null ? '-' : Number(value).toFixed(2));

// 导出对象
defineExpose({ openDialog });
</script>
