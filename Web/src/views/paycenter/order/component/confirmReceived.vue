<template>
	<el-dialog v-model="state.visible" title="确认到账" width="560px" draggable :close-on-click-modal="false" @closed="reset">
		<div v-if="state.row" class="confirm-summary">
			<p><span>订单号</span>{{ state.row.orderNo }}</p>
			<p><span>商家单号</span>{{ state.row.externalNo || '-' }}</p>
			<p><span>收款账号</span>{{ state.row.accountInfo || (state.row.qrImageUrl ? '收款码' : '-') }}（{{ state.row.accountType || '-' }}）</p>
			<p><span>请求金额</span>{{ money(state.row.requestAmount) }}</p>
			<p><span>已到账</span>{{ money(state.row.receivedAmount) }}</p>
			<p><span>本次确认</span><b class="amt">{{ money(gap) }}</b></p>
		</div>

		<el-form label-width="88px" class="mt12">
			<el-form-item label="水单" required>
				<div class="voucher-row">
					<el-upload :show-file-list="false" :auto-upload="false" accept=".jpg,.jpeg,.png,.bmp,.webp,.pdf" :on-change="onVoucherChange">
						<el-button type="primary" :loading="state.uploading" v-auth="'payOrder/uploadVoucher'">{{ state.voucherUrl ? '重新上传' : '上传水单' }}</el-button>
					</el-upload>
					<a v-if="state.voucherUrl" :href="state.voucherUrl" target="_blank" rel="noopener" class="voucher-link">已上传，点击查看</a>
				</div>
			</el-form-item>
			<el-form-item label="备注">
				<el-input v-model="state.remark" type="textarea" :rows="2" maxlength="256" show-word-limit placeholder="可选" />
			</el-form-item>
			<el-form-item>
				<el-checkbox v-model="state.acked">我已核对水单与金额，确认将该订单标记为已完成</el-checkbox>
			</el-form-item>
		</el-form>

		<template #footer>
			<el-button @click="state.visible = false">取消</el-button>
			<el-button type="primary" :loading="state.submitting" :disabled="!canSubmit" @click="submit">确认到账</el-button>
		</template>
	</el-dialog>
</template>

<script lang="ts" setup name="payOrderConfirmReceived">
import { computed, reactive } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import type { UploadFile } from 'element-plus';
import { getAPI } from '/@/utils/axios-utils';
import { PayOrderApi } from '/@/api-services/system/api';
import type { PayOrderOutput } from '/@/api-services/system/models';

const emits = defineEmits<{ (e: 'done'): void }>();

const state = reactive({
	visible: false,
	row: null as PayOrderOutput | null,
	voucherUrl: '' as string,
	remark: '' as string,
	acked: false,
	uploading: false,
	submitting: false,
});

const gap = computed(() => {
	const r = state.row;
	if (!r) return 0;
	return Number(r.requestAmount ?? 0) - Number(r.receivedAmount ?? 0);
});

const canSubmit = computed(() => !!state.voucherUrl && state.acked && gap.value > 0 && !state.submitting);

const money = (v: any) => {
	const n = Number(v ?? 0);
	return n.toFixed(2);
};

const open = (row: PayOrderOutput) => {
	state.row = row;
	state.voucherUrl = '';
	state.remark = '';
	state.acked = false;
	state.visible = true;
};

const reset = () => {
	state.row = null;
	state.voucherUrl = '';
	state.remark = '';
	state.acked = false;
};

const onVoucherChange = async (file: UploadFile) => {
	if (!file.raw) return;
	state.uploading = true;
	try {
		const res = await getAPI(PayOrderApi).apiPayOrderUploadVoucherPostForm(file.raw);
		const url = (res.data as any)?.result ?? '';
		if (!url) throw new Error('上传失败');
		state.voucherUrl = url;
		ElMessage.success('水单已上传');
	} catch (e: any) {
		ElMessage.error(e?.message || '水单上传失败');
	} finally {
		state.uploading = false;
	}
};

const submit = async () => {
	if (!state.row || !canSubmit.value) return;
	await ElMessageBox.confirm(
		`确认订单 ${state.row.orderNo} 到账金额 ${money(gap.value)}？提交后将通知下游 Webhook。`,
		'二次确认',
		{ type: 'warning', confirmButtonText: '确认提交', cancelButtonText: '返回检查' }
	);
	state.submitting = true;
	try {
		await getAPI(PayOrderApi).apiPayOrderConfirmReceivedPost({
			id: state.row.id!,
			voucherUrl: state.voucherUrl,
			remark: state.remark || null,
		});
		ElMessage.success('已确认到账，将通过 Webhook 通知下游');
		state.visible = false;
		emits('done');
	} finally {
		state.submitting = false;
	}
};

defineExpose({ open });
</script>

<style scoped>
.confirm-summary p {
	margin: 6px 0;
	line-height: 1.5;
}
.confirm-summary span {
	display: inline-block;
	width: 88px;
	color: var(--el-text-color-secondary);
}
.amt {
	color: var(--el-color-danger);
}
.mt12 {
	margin-top: 12px;
}
.voucher-row {
	display: flex;
	align-items: center;
	gap: 12px;
}
.voucher-link {
	font-size: 13px;
	color: var(--el-color-primary);
}
</style>
