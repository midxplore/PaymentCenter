<template>
	<div class="sys-tenant-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="700px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span> {{ props.title }} </span>
				</div>
			</template>
			<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="租户类型" prop="tenantType" :rules="[{ required: true, message: '租户类型不能为空', trigger: 'blur' }]">
							<el-radio-group v-model="state.ruleForm.tenantType" :disabled="state.ruleForm.id != undefined">
								<el-radio :value="0">ID隔离</el-radio>
								<el-radio :value="1">库隔离</el-radio>
							</el-radio-group>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="所属机构">
							<el-cascader :options="state.orgData" :props="cascaderProps" placeholder="所属机构" clearable filterable class="w100" v-model="state.ruleForm.orgPid">
								<template #default="{ node, data }">
									<span>{{ data.name }}</span>
									<span v-if="!node.isLeaf"> ({{ data.children.length }}) </span>
								</template>
							</el-cascader>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="租户名称" prop="name" :rules="[{ required: true, message: '租户名称不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.name" placeholder="租户名称" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="租管账号" prop="adminAccount" :rules="[{ required: true, message: '租管账号不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.adminAccount" placeholder="租管账号" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="租管姓名" prop="realName" :rules="[{ required: true, message: '租管姓名不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.realName" placeholder="租管姓名" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="电话" prop="phone" :rules="[{ required: true, message: '电话号码不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.phone" placeholder="电话" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="邮箱">
							<el-input v-model="state.ruleForm.email" placeholder="邮箱" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="绑定域名">
							<el-input v-model="state.ruleForm.host" placeholder="例如：https://gitee.com" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="过期时间">
							<el-date-picker v-model="state.ruleForm.expirationTime" type="datetime" placeholder="过期时间" format="YYYY-MM-DD HH:mm:ss" value-format="YYYY-MM-DD HH:mm:ss" class="w100" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="排序">
							<el-input-number v-model="state.ruleForm.orderNo" placeholder="排序" class="w100" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="库定位器">
							<el-select v-model="state.ruleForm.configId" placeholder="库定位器" filterable @change="dbChanged()" class="w100" :disabled="state.ruleForm.id != undefined">
								<el-option v-for="item in state.tenantDbData" :key="item.dbName" :label="item.dbName" :value="item.configId">
									<span style="float: left">{{ item.dbName }}</span>
									<span style="float: right; color: var(--el-text-color-secondary)"> {{ item.configId }} </span>
								</el-option>
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="数据库类型" prop="dbType" :rules="[{ required: true, message: '数据库类型不能为空', trigger: 'blur' }]">
							<g-sys-dict v-model="state.ruleForm.dbType" code="db_type" render-as="select" :disabled="state.ruleForm.tenantType == 0 && state.ruleForm.tenantType != undefined" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="连接字符串" prop="connection" :rules="[{ required: true, message: '连接字符串不能为空', trigger: 'blur' }]">
							<el-input
								v-model="state.ruleForm.connection"
								placeholder="连接字符串"
								clearable
								type="textarea"
								:disabled="state.ruleForm.tenantType == 0 && state.ruleForm.tenantType != undefined"
								:autosize="{ minRows: 4 }"
							/>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="从库连接串">
							<template v-slot:label>
								<div>
									从库连接串
									<el-tooltip raw-content content='格式：[{"HitRate":10, "ConnectionString":"xxx"},{"HitRate":10, "ConnectionString":"xxx"}]' placement="top">
										<SvgIcon name="fa fa-question-circle-o" :size="16" style="vertical-align: middle" />
									</el-tooltip>
								</div>
							</template>
							<el-input
								v-model="state.ruleForm.slaveConnections"
								placeholder='格式：[{"HitRate":10, "ConnectionString":"xxx"},{"HitRate":10, "ConnectionString":"xxx"}]'
								clearable
								type="textarea"
								:disabled="state.ruleForm.tenantType == 0 && state.ruleForm.tenantType != undefined"
							/>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="备注">
							<el-input v-model="state.ruleForm.remark" placeholder="请输入备注内容" clearable type="textarea" :autosize="{ minRows: 4 }" />
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

<script lang="ts" setup name="sysEditTenant">
import { reactive, ref } from 'vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysOrgApi, SysTenantApi } from '/@/api-services/system/api';
import { DbOutput, SysOrg, UpdateTenantInput } from '/@/api-services/system/models';

const props = defineProps({
	title: String,
});
const emits = defineEmits(['handleQuery']);
const ruleFormRef = ref();
const state = reactive({
	isShowDialog: false,
	ruleForm: {} as UpdateTenantInput,
	orgData: [] as Array<SysOrg>,
	tenantDbData: [] as Array<DbOutput>,
});
// 级联选择器配置选项
const cascaderProps = { checkStrictly: true, emitPath: false, value: 'id', label: 'name', expandTrigger: 'hover' };

// 打开弹窗
const openDialog = async (row: any) => {
	// 获取机构数据
	state.orgData = await getAPI(SysOrgApi)
		.apiSysOrgListGet(0)
		.then((res) => res.data.result ?? []);

	// 获取租户库列表
	state.tenantDbData = await getAPI(SysTenantApi)
		.apiSysTenantTenantDbListGet()
		.then((res) => res.data.result ?? []);

	state.ruleForm = JSON.parse(JSON.stringify(row));
	state.isShowDialog = true;
	ruleFormRef.value?.resetFields();
};

// db改变
const dbChanged = async () => {
	if (state.ruleForm.configId === '' || state.ruleForm.configId == null) return;

	let db = state.tenantDbData.find((u: any) => u.configId == state.ruleForm.configId);
	state.ruleForm.connection = db?.connection;
	state.ruleForm.dbType = db?.dbType;
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
		if (state.ruleForm.id != undefined && state.ruleForm.id > 0) {
			await getAPI(SysTenantApi).apiSysTenantUpdatePost(state.ruleForm);
		} else {
			await getAPI(SysTenantApi).apiSysTenantAddPost(state.ruleForm);
		}
		closeDialog();
	});
};

// 导出对象
defineExpose({ openDialog });
</script>
