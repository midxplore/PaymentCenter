<template>
	<div class="sys-user-container">
		<splitpanes class="default-theme">
			<pane size="15" style="display: flex">
				<OrgTree ref="orgTreeRef" @node-click="handleNodeChange" />
			</pane>
			<pane size="85" style="display: flex; flex-direction: column">
				<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
					<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
						<el-row :gutter="10">
							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item :label="$t('message.list.account')">
									<el-input v-model="state.queryParams.account" :placeholder="$t('message.list.account')" clearable @keyup.enter.native="handleQuery(true)" />
								</el-form-item>
							</el-col>

							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item :label="$t('message.list.realName')">
									<el-input v-model="state.queryParams.realName" :placeholder="$t('message.list.realName')" clearable @keyup.enter.native="handleQuery(true)" />
								</el-form-item>
							</el-col>
							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item :label="$t('message.list.jobTitle')">
									<el-input v-model="state.queryParams.posName" :placeholder="$t('message.list.jobTitle')" clearable />
								</el-form-item>
							</el-col>

							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item :label="$t('message.list.phoneNumber')">
									<el-input v-model="state.queryParams.phone" :placeholder="$t('message.list.phoneNumber')" clearable @keyup.enter.native="handleQuery(true)" />
								</el-form-item>
							</el-col>
						</el-row>
					</el-form>

					<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

					<el-row>
						<el-col>
							<el-button-group>
								<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysUser/page'" :loading="options.loading"> {{ $t('message.list.query') }} </el-button>
								<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> {{ $t('message.list.reset') }} </el-button>
							</el-button-group>
						</el-col>
					</el-row>
				</el-card>

				<el-card class="full-table" shadow="hover" style="margin-top: 5px; flex: 1">
					<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
						<template #toolbar_buttons>
							<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'sysUser/add'"> {{ $t('message.list.add') }} </el-button>
						</template>
						<template #toolbar_tools> </template>
						<template #empty>
							<el-empty :image-size="200" />
						</template>
						<template #row_avatar="{ row }">
							<el-avatar :src="row.avatar" size="small" :style="{ backgroundColor: row.sex == 1 ? '#409EFF' : '#F56C6C' }"> {{ row.nickName?.slice(0, 1) ?? row.realName?.slice(0, 1) }} </el-avatar>
						</template>
						<template #row_sex="{ row }">
							<el-tag v-if="row.sex === 1" type="primary"> {{ $t('message.list.male') }} </el-tag>
							<el-tag v-else-if="row.sex === 2" type="danger"> {{ $t('message.list.female') }} </el-tag>
							<el-tag v-else-if="row.sex === 0" type="info"> {{ $t('message.list.unknown') }} </el-tag>

							<el-tag v-else-if="row.sex === 9" type="info"> {{ $t('message.list.unspecified') }} </el-tag>
						</template>
						<template #row_accountType="{ row }">
							<el-tag v-if="row.accountType === 888"> {{ $t('message.list.systemAdmin') }} </el-tag>

							<el-tag v-else-if="row.accountType === 777"> {{ $t('message.list.normalAccount') }} </el-tag>
							<el-tag v-else-if="row.accountType === 666"> {{ $t('message.list.member') }} </el-tag>
							<el-tag v-else> {{ $t('message.list.other') }} </el-tag>
						</template>

						<template #row_status="{ row }">
							<el-switch v-model="row.status" :active-value="1" :inactive-value="2" size="small" @change="changeStatus(row)" v-auth="'sysUser/setStatus'" />
						</template>
						<template #row_record="{ row }">
							<ModifyRecord :data="row" />
						</template>
						<template #row_buttons="{ row }">
							<el-tooltip :content="$t('message.list.edit')" placement="top">
								<el-button icon="ele-Edit" text type="primary" v-auth="'sysUser/update'" @click="handleEdit(row)"> </el-button>
							</el-tooltip>
							<el-tooltip :content="$t('message.list.delete')" placement="top">
								<el-button icon="ele-Delete" text type="danger" v-auth="'sysUser/delete'" @click="handleDelete(row)"> </el-button>
							</el-tooltip>
							<el-tooltip :content="$t('message.list.copy')" placement="top">
								<el-button icon="ele-CopyDocument" text type="primary" v-auth="'sysUser/add'" @click="openCopyMenu(row)"> </el-button>
							</el-tooltip>

							<el-button icon="ele-RefreshLeft" text type="danger" v-auth="'sysUser/resetPwd'" @click="resetQueryPwd(row)">{{ $t('message.list.resetPassword') }}</el-button>
							<el-button icon="ele-Unlock" text type="primary" v-auth="'sysUser/unlockLogin'" @click="handleUnlock(row)">{{ $t('message.list.unlockAccount') }}</el-button>
						</template>
					</vxe-grid>
				</el-card>
			</pane>
		</splitpanes>

		<EditUser ref="editUserRef" :title="state.title" :orgData="state.treeData" @handleQuery="handleQuery" />
	</div>
</template>

<!-- 用户管理 -->
<script lang="ts" setup name="sysUser">
import { onMounted, reactive, ref, onActivated } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { useI18n } from 'vue-i18n';
import { auth } from '/@/utils/authFunction';
import { Splitpanes, Pane } from 'splitpanes';
import 'splitpanes/dist/splitpanes.css';

import OrgTree from '/@/views/system/org/component/orgTree.vue';
import EditUser from '/@/views/system/user/component/editUser.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysUserApi, SysOrgApi } from '/@/api-services/system/api';
import { SysOrg, PageTenantInput, UserOutput, UpdateUserInput } from '/@/api-services/system/models';

const { t } = useI18n();
const xGrid = ref<VxeGridInstance>();
// const treeRef = ref<InstanceType<typeof OrgTree>>();
const editUserRef = ref<InstanceType<typeof EditUser>>();
const state = reactive({
	treeData: [] as Array<SysOrg>,
	queryParams: {
		orgId: -1,
		account: undefined,
		realName: undefined,
		posName: undefined,
		phone: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'orderNo', order: 'asc', descStr: 'desc' },
	},
	visible: false,
	title: '',
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysUser';
// 表格参数配置
const options = useVxeTable<UserOutput>(
	{
		id: 'sysUser',
		name: t('message.list.account'),
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: t('message.list.seq'), width: 60, fixed: 'left' },
			{ field: 'avatar', title: t('message.list.avatar'), showOverflow: 'tooltip', slots: { default: 'row_avatar' } },
			{ field: 'account', title: t('message.list.account'), minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'nickName', title: t('message.list.nickname'), minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'realName', title: t('message.list.realName'), minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'phone', title: t('message.list.phoneNumber'), minWidth: 100, showOverflow: 'tooltip' },
			// { field: 'birthday', title: t('message.list.birthDate'), minWidth: 100, showOverflow: 'tooltip', formatter: ({ cellValue }: any) => XEUtils.toDateString(cellValue, 'yyyy-MM-dd') },
			{ field: 'sex', title: t('message.list.gender'), showOverflow: 'tooltip', slots: { default: 'row_sex' } },
			{ field: 'roleName', title: t('message.list.roleSet'), minWidth: 130, showOverflow: 'tooltip' },
			{ field: 'orgName', title: t('message.list.organization'), minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'posName', title: t('message.list.jobTitle'), minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'jobNum', title: t('message.list.jobNumb'), minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'accountType', title: t('message.list.accountType'), minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_accountType' } },
			{ field: 'status', title: t('message.list.status'), width: 80, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'orderNo', title: t('message.list.orderNo'), showOverflow: 'tooltip' },

			{ field: 'record', title: t('message.list.record'), width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: t('message.list.operation'), fixed: 'right', width: 260, showOverflow: true, slots: { default: 'row_buttons' } },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		// height:'400',
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: ({ page, sort }) => handleQueryApi(page, sort) } },
		// 排序配置
		sortConfig: { defaultSort: Local.get(localPageParamKey)?.defaultSort || state.localPageParam.defaultSort },
		// 分页配置
		pagerConfig: { pageSize: Local.get(localPageParamKey)?.pageSize || state.localPageParam.pageSize },
		// 工具栏配置
		toolbarConfig: { export: true },
	}
);

// 页面初始化
onMounted(async () => {
	state.localPageParam = Local.get(localPageParamKey) || state.localPageParam;
	await fetchOrgData();
});

// 组织架构节点切换
onActivated(async () => {
	// await fetchOrgData();
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageTenantInput;
	return getAPI(SysUserApi).apiSysUserPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.orgId = -1;
	state.queryParams.account = undefined;
	state.queryParams.realName = undefined;
	state.queryParams.posName = undefined;
	state.queryParams.phone = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 查询机构数据
const fetchOrgData = async () => {
	var res = await getAPI(SysOrgApi).apiSysOrgListGet(0);
	state.treeData = res.data.result ?? [];
};

// 打开新增页面
const handleAdd = () => {
	state.title = t('message.list.addAccount');
	editUserRef.value?.openDialog({ id: undefined, birthday: '2000-01-01', sex: 1, orderNo: 100, cardType: 0, cultureLevel: 5 });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = t('message.list.editAccount');
	editUserRef.value?.openDialog(row);
};

// 打开复制页面
const openCopyMenu = (row: any) => {
	state.title = t('message.list.copyAccount');
	var copyRow = JSON.parse(JSON.stringify(row)) as UpdateUserInput;
	copyRow.id = 0;
	copyRow.account = '';
	editUserRef.value?.openDialog(copyRow);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(t('message.list.confirmDeleteAccount', { account: row.account }), t('message.list.hint'), {
		confirmButtonText: t('message.list.confirmButtonText'),
		cancelButtonText: t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysUserApi).apiSysUserDeletePost({ id: row.id });
			await handleQuery();
			ElMessage.success(t('message.list.successDelete'));
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<UserOutput> = {
	// 只对 pager-config 配置时有效，分页发生改变时会触发该事件
	async pageChange({ pageSize }) {
		state.localPageParam.pageSize = pageSize;
		Local.set(localPageParamKey, state.localPageParam);
	},
	// 当排序条件发生变化时会触发该事件
	async sortChange({ field, order }) {
		state.localPageParam.defaultSort = { field: field, order: order!, descStr: 'desc' };
		Local.set(localPageParamKey, state.localPageParam);
	},
	// 双击行事件
	async cellDblclick({ row }) {
		if (auth('sysUser/update')) await handleEdit(row);
	},
};

// 修改状态
const changeStatus = (row: any) => {
	getAPI(SysUserApi)
		.apiSysUserSetStatusPost({ id: row.id, status: row.status })
		.then(() => {
			ElMessage.success(t('message.list.accountStatusUpdateSuccess'));
		})
		.catch(() => {
			row.status = row.status == 1 ? 2 : 1;
		});
};

// 重置密码
const resetQueryPwd = async (row: any) => {
	ElMessageBox.prompt(t('message.list.confirmResetPassword', { account: row.account }), t('message.list.resetPassword'), {
		confirmButtonText: t('message.list.confirmButtonText'),
		cancelButtonText: t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async ({ value }) => {
			await getAPI(SysUserApi)
				.apiSysUserResetPwdPost({ id: row.id, newPassword: value })
				.then((res) => {
					ElMessage.success(t('message.list.passwordResetSuccess', { password: res.data.result }));
				});
		})
		.catch(() => {});
};

// 解除登录锁定
const handleUnlock = async (row: any) => {
	ElMessageBox.confirm(t('message.list.confirmUnlockAccount', { account: row.account }), t('message.list.hint'), {
		confirmButtonText: t('message.list.confirmButtonText'),
		cancelButtonText: t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysUserApi)
				.apiSysUserUnlockLoginPost({ id: row.id })
				.then(() => {
					ElMessage.success(t('message.list.unlockSuccess'));
				});
		})
		.catch(() => {});
};

// 树组件点击
const handleNodeChange = async (node: any) => {
	state.queryParams.orgId = node.id;
	state.queryParams.account = undefined;
	state.queryParams.realName = undefined;
	state.queryParams.phone = undefined;
	await handleQuery();
};
</script>
