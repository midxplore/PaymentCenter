<!-- <template>
	<div class="sys-app-container">
		<el-dialog v-model="state.isShowDialog" width="300" draggable :close-on-click-modal="false">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Switch /> </el-icon>
					<span>切换应用</span>
				</div>
			</template>
			<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="应用" prop="id" :rules="[{ required: true, message: '应用不能为空', trigger: 'blur' }]">
							<el-select v-model="state.ruleForm.id" @change="changeApp" value-key="id" placeholder="应用" class="w100" clearable>
								<el-option v-for="item in state.appList" :key="item.id" :label="item.label" :value="item.id" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="租户" prop="tenantId" :rules="[{ required: true, message: '租户不能为空', trigger: 'blur' }]">
							<el-select v-model="state.ruleForm.tenantId" value-key="id" placeholder="租户" class="w100" clearable>
								<el-option v-for="item in state.tenantList" :key="item.id" :label="item.label" :value="item.id" />
							</el-select>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="() => (state.isShowDialog = false)">取 消</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit">确 定</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="sysEditApp">
import { reactive, ref } from 'vue';
import { getAPI } from '/@/utils/axios-utils';
import { SysAppApi } from '/@/api-services/system/api';
import { Local } from '/@/utils/storage';
import { accessTokenKey, refreshAccessTokenKey } from '/@/utils/request';

const ruleFormRef = ref();
const state = reactive({
	loading: false,
	isShowDialog: false,
	ruleForm: {} as any,
	appList: [] as Array<any>,
	tenantList: [] as Array<any>,
});

// 打开弹窗
const openDialog = async () => {
	if (state.appList.length == 0)
		state.appList = await getAPI(SysAppApi)
			.apiSysAppChangeAppGet()
			.then((res) => res.data.result ?? []);
	ruleFormRef.value?.resetFields();
	state.isShowDialog = true;
	state.loading = false;
};

// 应用id改变事件
const changeApp = (val: any) => {
	state.tenantList = state.appList.find((u) => u.id === val)?.children ?? [];
	state.ruleForm.tenantId = undefined;
};

// 提交
const submit = () => {
	ruleFormRef.value.validate(async (valid: boolean) => {
		if (!valid) return;
		state.loading = true;
		const newToken = await getAPI(SysAppApi)
			.apiSysAppChangeAppPost(state.ruleForm)
			.then((res) => res.data.result);
		if (newToken) {
			Local.set(accessTokenKey, newToken.accessToken);
			Local.set(refreshAccessTokenKey, newToken.refreshToken);
			location.reload();
		}
		state.loading = false;
		state.isShowDialog = false;
	});
};

// 导出对象
defineExpose({ openDialog });
</script> -->
