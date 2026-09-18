<template>
	<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="560px">
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Connection /> </el-icon>
				<span> 人工关联到订单 </span>
			</div>
		</template>

		<el-alert type="warning" :closable="false" style="margin-bottom: 12px">
			<template #title>
				关联后这笔到账会按正常到账流程累加到目标订单上，并推进订单状态。请确认金额与订单一致。
			</template>
		</el-alert>

		<el-descriptions :column="1" border size="small" style="margin-bottom: 12px">
			<el-descriptions-item label="上报订单号">
				<span class="mono">{{ state.ruleForm.reportOrderNo }}</span>
			</el-descriptions-item>
			<el-descriptions-item label="到账金额">{{ money(state.ruleForm.amount) }}</el-descriptions-item>
			<el-descriptions-item label="到账时间">{{ fmtTime(state.ruleForm.notifyTime) }}</el-descriptions-item>
			<el-descriptions-item label="凭证号">{{ state.ruleForm.voucherNo || '-' }}</el-descriptions-item>
			<el-descriptions-item label="异常原因">{{ state.ruleForm.reasonText || '-' }}</el-descriptions-item>
		</el-descriptions>

		<el-form :model="state.form" ref="ruleFormRef" label-width="auto">
			<el-form-item label="目标订单号" prop="targetOrderNo" :rules="[{ required: true, message: '目标订单号不能为空', trigger: 'blur' }]">
				<el-input v-model="state.form.targetOrderNo" placeholder="要关联到的系统订单号（精确匹配）" clearable />
			</el-form-item>
			<el-form-item label="处理说明" prop="handleRemark">
				<el-input v-model="state.form.handleRemark" type="textarea" :rows="3" placeholder="说明为什么关联到该订单（可空，便于日后追溯）" />
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

<script lang="ts" setup name="payAbnormalLinkOrder">
import { reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';

import { getAPI } from '/@/utils/axios-utils';
import { PayAbnormalApi } from '/@/api-services/system/api';
import { LinkAbnormalInput } from '/@/api-services/system/models';

const emits = defineEmits(['handleQuery']);
const ruleFormRef = ref();
const state = reactive({
	isShowDialog: false,
	submitting: false,
	// 被关联的异常到账记录（只读展示）
	ruleForm: {} as any,
	// 提交体
	form: {} as LinkAbnormalInput,
});

// 打开弹窗
const openDialog = (row: any) => {
	state.ruleForm = JSON.parse(JSON.stringify(row));
	state.form = { id: row.id, targetOrderNo: '', handleRemark: undefined };
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
			await getAPI(PayAbnormalApi).apiPayAbnormalLinkPost({ ...state.form, targetOrderNo: state.form.targetOrderNo.trim() });
			ElMessage.success('关联成功');
			closeDialog();
		} finally {
			state.submitting = false;
		}
	});
};

// 金额展示
const money = (value: any) => (value === undefined || value === null ? '-' : Number(value).toFixed(2));
// 时间展示
const fmtTime = (value: any) => {
	if (!value) return '';
	const d = new Date(value);
	if (isNaN(d.getTime())) return String(value);
	const p = (n: number) => String(n).padStart(2, '0');
	return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
};

// 导出对象
defineExpose({ openDialog });
</script>

<style lang="scss" scoped>
.mono {
	font-family: Menlo, Monaco, Consolas, monospace;
	letter-spacing: 0.5px;
}
</style>
