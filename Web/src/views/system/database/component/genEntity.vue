<template>
	<div class="sys-dbEntity-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="700px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Cpu /> </el-icon>
					<span> 生成实体 </span>
				</div>
			</template>
			<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="表名" prop="tableName" :rules="[{ required: true, message: '表名不能为空', trigger: 'blur' }]">
							<el-input disabled v-model="state.ruleForm.tableName" placeholder="表名" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="实体名称" prop="entityName" :rules="[{ required: false, message: '实体名称不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.entityName" placeholder="实体名称" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="基类" prop="baseClassName">
							<g-sys-dict v-model="state.ruleForm.baseClassName" code="code_gen_base_class" render-as="select" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="存放位置" prop="position">
							<el-select v-model="state.ruleForm.position" filterable clearable class="w100" placeholder="存放位置">
								<el-option v-for="(item, index) in props.applicationNamespaces" :key="index" :label="item" :value="item" />
							</el-select>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="cancel" :disabled="state.loading">取 消</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" v-reclick="3000" @click="submit" :disabled="state.loading">确 定</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="sysGenEntity">
import { reactive, ref } from 'vue';
import { ElNotification } from 'element-plus';
import { camelCase, upperFirst } from 'lodash-es';
import { useUserInfo } from '/@/stores/userInfo';

import { getAPI } from '/@/utils/axios-utils';
import { SysDatabaseApi } from '/@/api-services/system/api';

const emits = defineEmits(['handleQueryColumn']);
const ruleFormRef = ref();
const state = reactive({
	loading: false,
	isShowDialog: false,
	ruleForm: {} as any,
});

const props = defineProps({
	applicationNamespaces: { type: Array },
});

// 打开弹窗
const openDialog = (row: any) => {
	state.ruleForm.configId = row.configId;
	state.ruleForm.tableName = row.tableName;
	state.ruleForm.entityName = upperFirst(camelCase(row.tableName));
	state.ruleForm.baseClassName = 'EntityBase';
	state.ruleForm.position = row.position;
	state.ruleForm.authorName = '支付中心';
	state.isShowDialog = true;
};

// 关闭弹窗
const closeDialog = () => {
	emits('handleQueryColumn');
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
		state.loading = true;
		try {
			await getAPI(SysDatabaseApi).apiSysDatabaseCreateEntityPost(state.ruleForm);
			closeDialog();

			ElNotification({
				title: '提示',
				message: '生成成功，请重启服务加载最新代码',
				type: 'success',
				position: 'bottom-right',
			});
		} catch (e) {
			/* empty */
		}
		state.loading = false;
	});
};

// 导出对象
defineExpose({ openDialog });
</script>
