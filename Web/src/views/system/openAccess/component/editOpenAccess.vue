<template>
	<div class="sys-open-access-container">
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
						<el-form-item label="身份标识" prop="accessKey" :rules="[{ required: true, message: '身份标识不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.accessKey" placeholder="身份标识" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="密钥" prop="accessSecret" :rules="secretRules">
							<el-input v-model="state.ruleForm.accessSecret" :placeholder="isEdit ? '留空或保持掩码即不修改' : '密钥'" clearable>
								<template #append>
									<el-button @click="createSecret" v-auth="'sysOpenAccess/secret'">生成密钥</el-button>
								</template>
							</el-input>
							<div class="scope-tip" v-if="isEdit">列表接口只回显掩码（形如 abcd****wxyz）。保持掩码或留空 = <b>不修改</b>；点「生成密钥」才会换成新密钥。</div>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="绑定租户" prop="bindTenantId" :rules="[{ required: true, message: '绑定租户不能为空', trigger: 'blur' }]">
							<el-select v-model="state.ruleForm.bindTenantId" placeholder="绑定租户" filterable default-first-option style="width: 100%" @change="tenantChange">
								<el-option v-for="item in state.tenantData" :key="item.id" :label="item.name" :value="item.id" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="绑定用户" prop="bindUserId" :rules="[{ required: true, message: '绑定用户不能为空', trigger: 'blur' }]">
							<el-select v-model="state.ruleForm.bindUserId" placeholder="绑定用户" filterable default-first-option style="width: 100%">
								<!-- ★ 停用的用户标注并禁止选择：绑定到停用用户会建出「签名正确但鉴权必失败」的凭证 -->
								<el-option
									v-for="item in state.userData"
									:key="item.id"
									:label="`${item.account}【${item.realName}】${item.status === 2 ? '（已停用，不可选）' : ''}`"
									:value="item.id"
									:disabled="item.status === 2"
								>
									<span style="float: left">{{ item.account }}</span>
									<span style="float: right; color: var(--el-text-color-secondary)">
										{{ item.status === 2 ? '已停用' : item.realName }}
									</span>
								</el-option>
							</el-select>
							<div class="scope-tip">
								<b>绑定用户必须是启用状态</b>：该凭证的调用会以这个用户的身份记账；
								用户被停用后，凭证会<b>立即失效</b>（对外报「accessKey 无效」）。
							</div>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="权限范围" prop="scopes">
							<el-select v-model="scopeValue" placeholder="请选择权限范围（留空将拒绝一切调用）" multiple filterable allow-create default-first-option style="width: 100%">
								<el-option v-for="item in scopeOptions" :key="item.value" :label="item.label" :value="item.value" />
							</el-select>
							<div class="scope-tip">
								仅对开放接口生效；逗号分隔，可自定义。收款匹配填 <b>allocate</b>，到账通知填 <b>notify</b>。
								<br />
								<b>留空 = 拒绝一切调用</b>（不是「不限制」）：未勾选任何范围的凭证，所有开放接口都会被拒绝。
							</div>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="状态" prop="status">
							<el-select v-model="state.ruleForm.status" placeholder="状态" style="width: 100%">
								<el-option label="启用" :value="1" />
								<el-option label="停用" :value="2" />
							</el-select>
							<div class="scope-tip">停用后立即无法通过签名鉴权（对外与「身份标识无效」不可区分），但记录保留，历史订单与审计仍可追溯。不要用删除代替停用。</div>
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

<script lang="ts" setup name="sysOpenAccessEdit">
import { computed, onMounted, reactive, ref } from 'vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysOpenAccessApi, SysTenantApi } from '/@/api-services/system/api';
import { AddOpenAccessInput, SysUser, TenantOutput, UpdateOpenAccessInput } from '/@/api-services/system/models';

const props = defineProps({
	title: String,
});
const emits = defineEmits(['handleQuery']);
const ruleFormRef = ref();
const state = reactive({
	isShowDialog: false,
	ruleForm: {} as UpdateOpenAccessInput,
	tenantData: [] as Array<TenantOutput>, // 租户数据
	userData: [] as Array<SysUser>, // 用户数据
});

// 是否在编辑既有记录：决定密钥是否必填、以及掩码提示是否显示
const isEdit = computed(() => (state.ruleForm.id ?? 0) > 0);

// 新增时密钥必填；编辑时「留空」与「保持掩码」都表示不修改，故不做必填校验
const secretRules = computed(() => (isEdit.value ? [] : [{ required: true, message: '密钥不能为空', trigger: 'blur' }]));

// 权限范围可选项（支持手动输入自定义值）
const scopeOptions = [
	{ label: 'allocate（收款匹配：下单 / 查状态）', value: 'allocate' },
	{ label: 'notify（到账通知）', value: 'notify' },
];

// Scopes 在接口上是「逗号分隔字符串」，界面用多选数组，这里做双向转换
const scopeValue = computed<string[]>({
	get: () =>
		state.ruleForm.scopes
			? state.ruleForm.scopes
					.split(',')
					.map((item) => item.trim())
					.filter((item) => item)
			: [],
	set: (value: string[]) => {
		state.ruleForm.scopes = value?.length ? value.join(',') : null;
	},
});

onMounted(async () => {
	let res = await getAPI(SysTenantApi).apiSysTenantPagePost({ page: 1, pageSize: 10000, includeDefault: true });
	state.tenantData = res.data.result?.items ?? [];
});

// 打开弹窗
const openDialog = (row: any) => {
	state.ruleForm = JSON.parse(JSON.stringify(row));
	// 新增时补默认状态=启用（与后端 SysOpenAccess.Status 的默认值保持一致）
	if (state.ruleForm.status == undefined) state.ruleForm.status = 1;
	state.isShowDialog = true;
	ruleFormRef.value?.resetFields();
	tenantChange(false);
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
			await getAPI(SysOpenAccessApi).apiSysOpenAccessUpdatePost(state.ruleForm);
		} else {
			// 新增与编辑的**密钥语义刻意不同**，别把两者合并：
			//   AddOpenAccessInput.accessSecret    必填 —— 新增必须给出密钥；
			//   UpdateOpenAccessInput.accessSecret 可选 —— 留空 / 原样回传掩码都表示「不修改」。
			// 表单的 secretRules 已在新增时强制校验非空；这里显式带上该字段，
			// 让「新增必有密钥」这条不变式体现在**类型**上，而不是只存在于校验规则里。
			const addInput: AddOpenAccessInput = { ...state.ruleForm, accessSecret: state.ruleForm.accessSecret! };
			await getAPI(SysOpenAccessApi).apiSysOpenAccessAddPost(addInput);
		}
		closeDialog();
	});
};

/**
 * 租户值变更
 * @param clearBindUserId 是否清空绑定用户
 */
const tenantChange = async (clearBindUserId: boolean = true) => {
	var res = await getAPI(SysTenantApi).apiSysTenantUserListPost({ tenantId: state.ruleForm.bindTenantId ?? 0 });
	state.userData = res.data.result ?? [];
	if (clearBindUserId) {
		state.ruleForm.bindUserId = undefined!;
	}
};

/** 生成密钥 */
const createSecret = async () => {
	var res = await getAPI(SysOpenAccessApi).apiSysOpenAccessSecretPost();
	state.ruleForm.accessSecret = res.data.result!;
};

// 导出对象
defineExpose({ openDialog });
</script>

<style scoped lang="scss">
.scope-tip {
	margin-top: 4px;
	font-size: 12px;
	line-height: 1.4;
	color: var(--el-text-color-secondary);
}
</style>
