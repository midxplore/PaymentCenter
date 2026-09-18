<template>
	<div class="sys-user-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="700px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span>{{ props.title }}</span>
				</div>
			</template>
			<el-tabs v-loading="state.loading" v-model="state.selectedTabName">
				<el-tab-pane :label="$t('message.list.basicInfo')" style="height: 600px; overflow-y: auto; overflow-x: hidden">
					<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto">
						<el-row :gutter="10">
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.account')" prop="account" :rules="[{ required: true, message: $t('message.list.accountNameRequired'), trigger: 'blur' }]">
									<el-input v-model="state.ruleForm.account" :placeholder="$t('message.list.account')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.nickname')">
									<el-input v-model="state.ruleForm.nickName" :placeholder="$t('message.list.nickname')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.phoneNumber')" prop="phone" :rules="[{ required: true, message: $t('message.list.phoneRequired'), trigger: 'blur' }]">
									<el-input v-model="state.ruleForm.phone" :placeholder="$t('message.list.phoneNumber')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.realName')" prop="realName" :rules="[{ required: true, message: $t('message.list.realNameRequired'), trigger: 'blur' }]">
									<el-input v-model="state.ruleForm.realName" :placeholder="$t('message.list.realName')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.accountType')" prop="accountType" :rules="[{ required: true, message: $t('message.list.accountTypeRequired'), trigger: 'blur' }]">
									<el-select v-model="state.ruleForm.accountType" :placeholder="$t('message.list.accountType')" collapse-tags collapse-tags-tooltip class="w100">
										<el-option :label="$t('message.list.systemAdmin')" :value="888" :disabled="userInfos.accountType != 888 && userInfos.accountType != 999" />
										<el-option :label="$t('message.list.normalAccount')" :value="777" />
										<el-option :label="$t('message.list.member')" :value="666" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.email')">
									<el-input v-model="state.ruleForm.email" :placeholder="$t('message.list.email')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.entryDate')">
									<el-date-picker v-model="state.ruleForm.joinDate" type="date" :placeholder="$t('message.list.entryDate')" format="YYYY-MM-DD" value-format="YYYY-MM-DD" class="w100" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb5">
								<el-form-item :label="$t('message.list.orderNo')">
									<el-input-number v-model="state.ruleForm.orderNo" :placeholder="$t('message.list.orderNo')" class="w100" />
								</el-form-item>
							</el-col>
							<el-divider border-style="dashed" content-position="center">
								<div style="color: #b1b3b8">{{ $t('message.list.orgStructure') }}</div>
							</el-divider>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.organization')" prop="orgId" :rules="[{ required: true, message: $t('message.list.organizationRequired'), trigger: 'blur' }]">
									<el-cascader :options="props.orgData" :props="cascaderProps" :placeholder="$t('message.list.organization')" clearable filterable class="w100" v-model="state.ruleForm.orgId">
										<template #default="{ node, data }">
											<span>{{ data.name }}</span>
											<span v-if="!node.isLeaf"> ({{ data.children.length }}) </span>
										</template>
									</el-cascader>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.employeeId')">
									<el-input v-model="state.ruleForm.jobNum" :placeholder="$t('message.list.employeeId')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.domainAccount')">
									<el-input v-model="state.ruleForm.domainAccount" :placeholder="$t('message.list.domainAccount')" clearable />
								</el-form-item>
							</el-col>
							<el-divider border-style="dashed" content-position="center">
								<div style="color: #b1b3b8">{{ $t('message.list.affiliatedOrg') }}</div>
							</el-divider>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb10">
								<el-button icon="ele-Plus" type="primary" plain @click="addExtOrgRow"> {{ $t('message.list.addAffiliatedOrg') }} </el-button>
								<span style="font-size: 12px; color: gray; padding-left: 5px"> {{ $t('message.list.orgDataPermission') }} </span>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<template v-if="state.ruleForm.extOrgIdList != undefined && state.ruleForm.extOrgIdList.length > 0">
									<el-row :gutter="10" v-for="(v, k) in state.ruleForm.extOrgIdList" :key="k">
										<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
											<el-form-item :label="$t('message.list.organization')" :prop="`extOrgIdList[${k}].orgId`" :rules="[{ required: true, message: $t('message.list.orgRequired'), trigger: 'blur' }]">
												<template #label>
													<el-button icon="ele-Delete" type="danger" circle plain size="small" @click="deleteExtOrgRow(k)" />
													<span class="ml5">{{ $t('message.list.organization') }}</span>
												</template>
												<el-cascader
													:options="props.orgData"
													:props="cascaderProps"
													:placeholder="$t('message.list.organization')"
													clearable
													filterable
													class="w100"
													v-model="state.ruleForm.extOrgIdList[k].orgId"
												>
													<template #default="{ node, data }">
														<span>{{ data.name }}</span>
														<span v-if="!node.isLeaf"> ({{ data.children.length }}) </span>
													</template>
												</el-cascader>
											</el-form-item>
										</el-col>
									</el-row>
								</template>
								<el-empty :image-size="50" :description="$t('message.list.noData')" v-else></el-empty>
							</el-col>
						</el-row>
					</el-form>
				</el-tab-pane>
				<el-tab-pane :label="$t('message.list.roleAuth')" style="height: 600px; overflow-y: auto; overflow-x: hidden">
					<Transfer :left-title="$t('message.list.unauthorized')" :right-title="$t('message.list.authorized')" v-model:leftData="state.availableRoles" v-model:rightData="state.grantedRoles" />
				</el-tab-pane>
				<el-tab-pane :label="$t('message.list.profileInfo')" style="height: 600px; overflow-y: auto; overflow-x: hidden">
					<el-form :model="state.ruleForm" label-width="auto">
						<el-row :gutter="10">
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.idType')" prop="cardType">
									<el-select v-model="state.ruleForm.cardType" :placeholder="$t('message.list.idType')" class="w100">
										<el-option :label="$t('message.list.idCard')" :value="0" />
										<el-option :label="$t('message.list.passport')" :value="1" />
										<el-option :label="$t('message.list.birthCertificate')" :value="2" />
										<el-option :label="$t('message.list.hkMacauPass')" :value="3" />
										<el-option :label="$t('message.list.foreignerCard')" :value="4" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.idNumber')">
									<el-input v-model="state.ruleForm.idCardNum" :placeholder="$t('message.list.idNumber')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.birthDate')" prop="birthday">
									<el-date-picker v-model="state.ruleForm.birthday" type="date" :placeholder="$t('message.list.birthDate')" format="YYYY-MM-DD" value-format="YYYY-MM-DD" class="w100" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.gender')">
									<el-radio-group v-model="state.ruleForm.sex">
										<el-radio :value="1">{{ $t('message.list.male') }}</el-radio>
										<el-radio :value="2">{{ $t('message.list.female') }}</el-radio>
										<el-radio :value="0">{{ $t('message.list.unknown') }}</el-radio>
										<el-radio :value="9">{{ $t('message.list.unspecified') }}</el-radio>
									</el-radio-group>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb5">
								<el-form-item :label="$t('message.list.age')">
									<el-input-number v-model="state.ruleForm.age" :placeholder="$t('message.list.age')" class="w100" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.ethnicity')">
									<el-input v-model="state.ruleForm.nation" :placeholder="$t('message.list.ethnicity')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item :label="$t('message.list.address')">
									<el-input v-model="state.ruleForm.address" :placeholder="$t('message.list.address')" clearable type="textarea" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.graduateSchool')">
									<el-input v-model="state.ruleForm.college" :placeholder="$t('message.list.graduateSchool')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.educationLevel')">
									<el-select v-model="state.ruleForm.cultureLevel" :placeholder="$t('message.list.educationLevel')" class="w100">
										<el-option :label="$t('message.list.other')" :value="0" />
										<el-option :label="$t('message.list.primarySchool')" :value="1" />
										<el-option :label="$t('message.list.juniorHigh')" :value="2" />
										<el-option :label="$t('message.list.seniorHigh')" :value="3" />
										<el-option :label="$t('message.list.technicalSchool')" :value="4" />
										<el-option :label="$t('message.list.vocationalEdu')" :value="5" />
										<el-option :label="$t('message.list.vocationalHigh')" :value="6" />
										<el-option :label="$t('message.list.technicalCollege')" :value="7" />
										<el-option :label="$t('message.list.juniorCollege')" :value="8" />
										<el-option :label="$t('message.list.undergraduate')" :value="9" />
										<el-option :label="$t('message.list.master')" :value="10" />
										<el-option :label="$t('message.list.doctor')" :value="11" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.politicalStatus')">
									<el-input v-model="state.ruleForm.politicalOutlook" :placeholder="$t('message.list.politicalStatus')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.officePhone')">
									<el-input v-model="state.ruleForm.officePhone" :placeholder="$t('message.list.officePhone')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.emergencyContact')">
									<el-input v-model="state.ruleForm.emergencyContact" :placeholder="$t('message.list.emergencyContact')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item :label="$t('message.list.contactPhone')">
									<el-input v-model="state.ruleForm.emergencyPhone" :placeholder="$t('message.list.contactPhone')" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item :label="$t('message.list.contactAddress')">
									<el-input v-model="state.ruleForm.emergencyAddress" :placeholder="$t('message.list.contactAddress')" clearable type="textarea" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item :label="$t('message.list.remark')">
									<el-input v-model="state.ruleForm.remark" :placeholder="$t('message.list.needInputRemark')" clearable type="textarea" />
								</el-form-item>
							</el-col>
						</el-row>
					</el-form>
				</el-tab-pane>
			</el-tabs>
			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="cancel">{{ $t('message.list.cancelButtonText') }}</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit">{{ $t('message.list.confirmButtonText') }}</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="sysEditUser">
import { reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';
import { storeToRefs } from 'pinia';
import { useUserInfo } from '/@/stores/userInfo';
import Transfer from '/@/components/transfer/index.vue';
import { useI18n } from 'vue-i18n';

import { getAPI } from '/@/utils/axios-utils';
import { SysRoleApi, SysUserApi } from '/@/api-services/system/api';
import { SysOrg, UpdateUserInput } from '/@/api-services/system/models';

const { t } = useI18n();
const props = defineProps({
	title: String,
	orgData: Array<SysOrg>,
});
const emits = defineEmits(['handleQuery']);
const ruleFormRef = ref();
const storesUserInfo = useUserInfo();
const { userInfos } = storeToRefs(storesUserInfo);
const state = reactive({
	loading: false,
	isShowDialog: false,
	selectedTabName: '0', // 选中的 tab 页
	ruleForm: {} as UpdateUserInput,
	availableRoles: [] as any, // 可授权角色
	grantedRoles: [] as any, // 已授权角色
});
// 级联选择器配置选项
const cascaderProps = { checkStrictly: true, emitPath: false, value: 'id', label: 'name', expandTrigger: 'hover' };

// 打开弹窗
const openDialog = async (row: any) => {
	ruleFormRef.value?.resetFields();

	state.selectedTabName = '0'; // 重置为第一个 tab 页
	state.ruleForm = JSON.parse(JSON.stringify(row));
	if (row.id != undefined) {
		const { data } = await getAPI(SysUserApi).apiSysUserOwnRoleListUserIdGet(row.id);
		state.availableRoles = data.result?.availableRoles;
		state.grantedRoles = data.result?.grantedRoles;
		var resExtOrg = await getAPI(SysUserApi).apiSysUserOwnExtOrgListUserIdGet(row.id);
		state.ruleForm.extOrgIdList = resExtOrg.data.result;
	} else {
		state.ruleForm.accountType = 777; // 默认普通账号类型
		const { data } = await getAPI(SysRoleApi).apiSysRoleListGet();
		state.availableRoles = data.result ?? [];
		state.grantedRoles = [];
		// state.grantedRoles = [{ id: 1300000000104, name: '仅本人数据' }]; // 默认仅本人数据角色
	}
	state.isShowDialog = true;
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
		if (state.grantedRoles?.length > 0) state.ruleForm.roleIdList = state.grantedRoles.map((e: any) => e.id);
		else {
			ElMessage.error(t('message.list.pleaseAssignRole'));
			return;
		}
		if (state.ruleForm.id != undefined && state.ruleForm.id > 0) {
			await getAPI(SysUserApi).apiSysUserUpdatePost(state.ruleForm);
		} else {
			await getAPI(SysUserApi).apiSysUserAddPost(state.ruleForm);
		}
		closeDialog();
	});
};

// 增加附属机构行
const addExtOrgRow = () => {
	if (state.ruleForm.extOrgIdList == undefined) state.ruleForm.extOrgIdList = [];
	state.ruleForm.extOrgIdList?.push({});
};

// 删除附属机构行
const deleteExtOrgRow = (k: number) => {
	state.ruleForm.extOrgIdList?.splice(k, 1);
};

// 导出对象
defineExpose({ openDialog });
</script>
