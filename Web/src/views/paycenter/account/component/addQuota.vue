<template>
	<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="560px">
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Coin /> </el-icon>
				<span> 额度调整 </span>
			</div>
		</template>

		<el-alert :type="reduceBlocked ? 'warning' : 'info'" :closable="false" style="margin-bottom: 12px">
			<template #title>
				<span v-if="reduceBlocked">当前有预占或占用中订单，只能调增，不能调减；请待订单完结或过期后再调减。</span>
				<span v-else>调整的是「总额度」。调增后若原为「已用完」且剩余恢复为正，会自动回到「启用」。调减后不得低于已用额度与最小匹配金额。</span>
			</template>
		</el-alert>

		<el-descriptions :column="1" border size="small" style="margin-bottom: 12px">
			<el-descriptions-item label="账号信息">{{ state.account.accountInfo || (state.account.qrImageUrl ? '收款码' : '-') }}</el-descriptions-item>
			<el-descriptions-item label="总额度">{{ money(state.account.totalQuota) }}</el-descriptions-item>
			<el-descriptions-item label="已用 / 预占">{{ money(state.account.usedQuota) }} / {{ money(state.account.lockedQuota) }}</el-descriptions-item>
			<el-descriptions-item label="剩余可用">{{ money(state.account.remainingQuota) }}</el-descriptions-item>
			<el-descriptions-item label="最小金额">{{ Number(state.account.minAmount) > 0 ? money(state.account.minAmount) : '不限制' }}</el-descriptions-item>
			<el-descriptions-item v-if="!state.form.increase" label="最多可减">{{ money(maxReduce) }}</el-descriptions-item>
		</el-descriptions>

		<el-form :model="state.form" ref="ruleFormRef" label-width="auto">
			<el-form-item label="调整方向" prop="increase">
				<el-radio-group v-model="state.form.increase" @change="onDirectionChange">
					<el-radio :value="true">增加</el-radio>
					<el-radio :value="false" :disabled="reduceBlocked">减少</el-radio>
				</el-radio-group>
			</el-form-item>
			<el-form-item label="调整金额" prop="quota" :rules="[{ required: true, message: '调整金额不能为空', trigger: 'blur' }]">
				<el-input-number
					v-model="state.form.quota"
					:min="0.01"
					:max="state.form.increase ? undefined : Math.max(maxReduce, 0.01)"
					:precision="2"
					:step="1000"
					style="width: 100%"
				/>
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
import { computed, reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';

import { getAPI } from '/@/utils/axios-utils';
import { PayAccountApi } from '/@/api-services/system/api';
import { AddQuotaInput } from '/@/api-services/system/models';

const emits = defineEmits(['handleQuery']);
const ruleFormRef = ref();
const state = reactive({
	isShowDialog: false,
	submitting: false,
	account: {} as any,
	form: { id: 0, quota: 1000, increase: true } as AddQuotaInput,
});

/** 有预占或历史占用中 → 禁止调减 */
const reduceBlocked = computed(() => {
	const locked = Number(state.account.lockedQuota || 0);
	const status = Number(state.account.status || 0);
	return locked > 0 || status === 4; // InUse = 4
});

/** 可调减上限：总额 − max(已用, 最小金额)；有预占时为 0 */
const maxReduce = computed(() => {
	if (reduceBlocked.value) return 0;
	const total = Number(state.account.totalQuota || 0);
	const used = Number(state.account.usedQuota || 0);
	const minAmount = Number(state.account.minAmount || 0);
	const floor = Math.max(used, minAmount > 0 ? minAmount : 0);
	return Math.max(0, Number((total - floor).toFixed(2)));
});

const onDirectionChange = () => {
	if (!state.form.increase && maxReduce.value > 0 && Number(state.form.quota) > maxReduce.value) {
		state.form.quota = maxReduce.value;
	}
};

const openDialog = (row: any) => {
	state.account = JSON.parse(JSON.stringify(row));
	state.form = { id: row.id, quota: 1000, increase: true };
	state.isShowDialog = true;
	ruleFormRef.value?.resetFields();
};

const closeDialog = () => {
	emits('handleQuery');
	state.isShowDialog = false;
};

const cancel = () => {
	state.isShowDialog = false;
};

const submit = () => {
	ruleFormRef.value.validate(async (valid: boolean) => {
		if (!valid) return;
		if (!state.form.increase) {
			if (reduceBlocked.value) {
				ElMessage.warning('存在预占或占用中，不能调减');
				return;
			}
			if (Number(state.form.quota) > maxReduce.value) {
				ElMessage.warning(`调减金额不能超过 ${maxReduce.value.toFixed(2)}`);
				return;
			}
		}
		state.submitting = true;
		try {
			await getAPI(PayAccountApi).apiPayAccountAddQuotaPost({
				id: state.form.id,
				quota: state.form.quota,
				increase: state.form.increase,
			});
			ElMessage.success(state.form.increase ? '调增成功' : '调减成功');
			closeDialog();
		} finally {
			state.submitting = false;
		}
	});
};

const money = (value: any) => (value === undefined || value === null ? '-' : Number(value).toFixed(2));

defineExpose({ openDialog });
</script>
