<template>
	<div class="sys-grantData-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="500px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span> {{ state.title }} </span>
				</div>
			</template>
			<el-form :model="state.ruleForm" label-position="top">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl1="24" class="mb10">
						<el-form-item :label="$t('message.list.dataScope') + '：'">
							<el-select v-model="state.ruleForm.dataScope" :placeholder="$t('message.list.dataScope')" style="width: 100%">
								<el-option v-for="d in state.dataScopeType" :key="d.value" :label="d.label" :value="d.value" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl1="24" v-show="state.ruleForm.dataScope === 5">
						<el-form-item :label="$t('message.list.orgList') + '：'" class="tree-box">
							<div style="display: flex; height: 100%; width: 100%">
								<OrgTree ref="orgTreeRef" :check-strictly="false" class="w100" />
							</div>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="cancel">{{ $t('message.list.cancelButtonText') }}</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit">{{ $t('message.list.confirmButtonText') }}</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="sysGrantData">
import { reactive, ref } from 'vue';
import OrgTree from '/@/views/system/org/component/orgTree.vue';
import { useI18n } from 'vue-i18n';

import { getAPI } from '/@/utils/axios-utils';
import { SysRoleApi } from '/@/api-services/system/api';
import { RoleOrgInput } from '/@/api-services/system/models';

const emits = defineEmits(['handleQuery']);
const orgTreeRef = ref();
const { t } = useI18n();
const state = reactive({
	isShowDialog: false,
	title: '',
	ruleForm: {} as RoleOrgInput,
	dataScopeType: [
		{ value: 1, label: t('message.list.allData') },
		{ value: 2, label: t('message.list.deptAndBelowData') },
		{ value: 3, label: t('message.list.deptData') },
		{ value: 4, label: t('message.list.personalData') },
		{ value: 5, label: t('message.list.customData') },
	],
});

// 打开弹窗
const openDialog = async (row: any) => {
	state.title = t('message.list.authRoleDataScopeWithName', { name: row.name });

	state.ruleForm = JSON.parse(JSON.stringify(row));
	var res = await getAPI(SysRoleApi).apiSysRoleOwnOrgListGet(row.id);
	setTimeout(() => {
		orgTreeRef.value?.setCheckedKeys(res.data.result);
	}, 100);
	state.isShowDialog = true;
};

// 取消
const cancel = () => {
	state.isShowDialog = false;
};

// 关闭弹窗
const closeDialog = () => {
	emits('handleQuery');
	state.isShowDialog = false;
};

// 提交
const submit = async () => {
	if (state.ruleForm.dataScope === 5) state.ruleForm.orgIdList = orgTreeRef.value?.getCheckedKeys();
	await getAPI(SysRoleApi).apiSysRoleGrantDataScopePost(state.ruleForm);
	closeDialog();
};

// 导出对象
defineExpose({ openDialog });
</script>

<!-- <style lang="scss" scoped>
:deep(.el-dialog__body) {
	min-height: 615px;
}
.tree-box {
	:deep(.el-form-item__content) {
		height: 500px;
	}
}
</style> -->
