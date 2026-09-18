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
					<el-form-item label="账号信息" prop="accountInfo" :rules="[{ required: true, message: '账号信息不能为空', trigger: 'blur' }]">
						<!-- 微信/支付宝收款码填收款码链接或账号；银行卡填卡号 -->
						<el-input v-model="state.ruleForm.accountInfo" placeholder="收款码 / 账号 / 卡号" :disabled="isEdit" clearable />
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
});

// 有 Id 即编辑态（编辑只允许改备注与状态）
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
		state.submitting = true;
		try {
			if (isEdit.value) {
				await getAPI(PayAccountApi).apiPayAccountUpdatePost({
					id: state.ruleForm.id,
					remark: state.ruleForm.remark,
					status: state.ruleForm.status,
				});
			} else {
				await getAPI(PayAccountApi).apiPayAccountAddPost({
					type: state.ruleForm.type,
					accountInfo: state.ruleForm.accountInfo,
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

// 导出对象
defineExpose({ openDialog });
</script>
