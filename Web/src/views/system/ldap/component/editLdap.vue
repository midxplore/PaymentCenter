<template>
	<div class="sysLdap-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="900px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span>{{ props.title }}</span>
				</div>
			</template>
			<el-form :model="formData" ref="formRef" label-width="auto" :rules="rules">
				<el-row :gutter="10">
					<el-form-item v-show="false">
						<el-input v-model="formData.id" />
					</el-form-item>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="主机" prop="host">
							<el-input v-model="formData.host" placeholder="请输入主机" maxlength="128" show-word-limit clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="端口" prop="port">
							<el-input-number v-model="formData.port" :min="0" :precision="0" placeholder="请输入端口" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="搜索基准" prop="baseDn">
							<el-input v-model="formData.baseDn" placeholder="请输入搜索基准" maxlength="128" show-word-limit clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="过滤规则" prop="authFilter">
							<el-input v-model="formData.authFilter" placeholder="请输入过滤规则" maxlength="128" show-word-limit clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="绑定DN" prop="bindDn">
							<el-input v-model="formData.bindDn" placeholder="请输入绑定DN" maxlength="128" show-word-limit clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="绑定密码" prop="bindPass">
							<el-input v-model="formData.bindPass" placeholder="请输入绑定密码" maxlength="512" show-word-limit clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="字段属性" prop="bindAttrAccount">
							<el-input v-model="formData.bindAttrAccount" placeholder="请输入字段属性" maxlength="24" show-word-limit clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="用户属性" prop="bindAttrEmployeeId">
							<el-input v-model="formData.bindAttrEmployeeId" placeholder="请输入绑定用户EmployeeId属性" maxlength="24" show-word-limit clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="Ldap版本" prop="version">
							<el-input-number v-model="formData.version" :min="0" :precision="0" placeholder="请输入Ldap版本" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="状态" prop="status">
							<el-switch v-model="formData.status" :active-value="1" :inactive-value="2" active-text="启用" inactive-text="禁用" />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="cancel">取 消</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit">确 定</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="editSysLdap">
import { ref, onMounted, reactive } from 'vue';
import { ElMessage } from 'element-plus';
import type { ElForm, FormRules } from 'element-plus';

import { getAPI } from '/@/utils/axios-utils';
import { SysLdapApi } from '/@/api-services/system/api';

// 父级传递来的参数
var props = defineProps({
	title: { type: String, default: '' },
});
// 父级传递来的函数，用于回调
const emit = defineEmits(['handleQuery']);
const formRef = ref<InstanceType<typeof ElForm>>();
const state = reactive({
	isShowDialog: false,
	loading: false,
});
const formData = ref<any>({});
// 自行添加其他规则
const rules = ref<FormRules>({
	host: [{ required: true, message: '请输入主机！', trigger: 'blur' }],
	port: [{ required: true, message: '请输入端口！', trigger: 'blur' }],
	baseDn: [{ required: true, message: '请输入搜索基准！', trigger: 'blur' }],
	bindDn: [{ required: true, message: '请输入绑定DN！', trigger: 'blur' }],
	bindPass: [{ required: true, message: '请输入绑定密码！', trigger: 'blur' }],
	authFilter: [{ required: true, message: '请输入过滤规则！', trigger: 'blur' }],
	version: [{ required: true, message: '请输入Ldap版本！', trigger: 'blur' }],
	bindAttrAccount: [{ required: true, message: '请输入字段属性！', trigger: 'blur' }],
	bindAttrEmployeeId: [{ required: true, message: '请输入绑定用户EmployeeId属性！', trigger: 'blur' }],
	status: [{ required: true, message: '请输入状态！', trigger: 'blur' }],
});

// 页面初始化
onMounted(async () => {});

// 打开弹窗
const showDialog = async (row: any) => {
	formRef.value?.resetFields();
	formRef.value?.clearValidate();
	let data = JSON.parse(JSON.stringify(row));
	if (data.id) formData.value = (await getAPI(SysLdapApi).apiSysLdapDetailGet(data.id)).data.result;
	else formData.value = data;
	state.isShowDialog = true;
};

// 关闭弹窗
const closeDialog = () => {
	emit('handleQuery');
	state.isShowDialog = false;
};

// 取消
const cancel = () => {
	state.isShowDialog = false;
};

// 提交
const submit = async () => {
	formRef.value?.validate(async (isValid: boolean, fields?: any) => {
		if (isValid) {
			let values = formData.value;
			state.loading = true;
			if (formData.value.id == undefined || formData.value.id == null || formData.value.id == '' || formData.value.id == 0) {
				await getAPI(SysLdapApi).apiSysLdapAddPost(values);
			} else await getAPI(SysLdapApi).apiSysLdapUpdatePost(values);
			state.loading = false;
			closeDialog();
		} else ElMessage({ message: `表单有${Object.keys(fields).length}处验证失败，请修改后再提交`, type: 'error' });
	});
};

// 将属性或者函数暴露给父组件
defineExpose({ showDialog });
</script>

<style scoped>
:deep(.el-select),
:deep(.el-input-number) {
	width: 100%;
}
</style>
