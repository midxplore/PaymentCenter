<template>
	<div class="sys-role-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item :label="$t('message.list.roleName')" prop="name">
							<el-input v-model="state.queryParams.name" :placeholder="$t('message.list.roleName')" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item :label="$t('message.list.roleCode')" prop="code">
							<el-input v-model="state.queryParams.code" :placeholder="$t('message.list.roleCode')" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysRole/page'"> {{ $t('message.list.query') }} </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery"> {{ $t('message.list.reset') }} </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'sysRole/add'"> {{ $t('message.list.add') }} </el-button>
				</template>
				<template #toolbar_tools></template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_dataScope="{ row }">
					<el-tag v-if="row.dataScope === 1">{{ $t('message.list.allData') }}</el-tag>
					<el-tag v-else-if="row.dataScope === 2">{{ $t('message.list.deptAndBelowData') }}</el-tag>
					<el-tag v-else-if="row.dataScope === 3">{{ $t('message.list.deptData') }}</el-tag>
					<el-tag v-else-if="row.dataScope === 4">{{ $t('message.list.personalData') }}</el-tag>
					<el-tag v-else-if="row.dataScope === 5">{{ $t('message.list.customData') }}</el-tag>
				</template>
				<template #row_sysFlag="{ row }">
					<g-sys-dict v-model="row.sysFlag" code="YesNoEnum" />
				</template>
				<template #row_status="{ row }">
					<el-tag v-if="row.status === 1" type="success">{{ $t('message.list.enable') }}</el-tag>
					<el-tag v-else type="danger">{{ $t('message.list.disable') }}</el-tag>
				</template>
				<template #row_record="{ row }">
					<ModifyRecord :data="row" />
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip :content="$t('message.list.edit')" placement="top">
						<el-button icon="ele-Edit" size="small" text type="primary" @click="handleEdit(row)" v-auth="'sysRole/update'"></el-button>
					</el-tooltip>
					<el-tooltip :content="$t('message.list.delete')" placement="top">
						<el-button icon="ele-Delete" size="small" text type="danger" @click="handleDelete(row)" v-auth="'sysRole/delete'"></el-button>
					</el-tooltip>
					<el-tooltip :content="$t('message.list.copy')" placement="top">
						<el-button icon="ele-CopyDocument" text type="primary" @click="handleCopy(row)" v-auth="'sysRole/add'"></el-button>
					</el-tooltip>
					<el-button icon="ele-Menu" size="small" text type="primary" @click="openGrantMenu(row)" v-auth="'sysRole/grantMenu'">{{ $t('message.list.authMenu') }}</el-button>
					<el-button icon="ele-OfficeBuilding" size="small" text type="primary" @click="openGrantData(row)" v-auth="'sysRole/grantDataScope'">{{ $t('message.list.authData') }}</el-button>
					<el-button icon="ele-Grid" size="small" text type="primary" @click="openGrantTable(row)" v-auth="'sysRole/grantTable'">{{ $t('message.list.fieldBlacklist') }}</el-button>
					<el-button icon="ele-Link" size="small" text type="primary" @click="openGrantApi(row)" v-auth="'sysRole/grantApi'">{{ $t('message.list.apiBlacklist') }}</el-button>
					<el-button icon="ele-User" size="small" text type="primary" @click="openGrantUser(row)" v-auth="'sysRole/grantApi'">{{ $t('message.list.authAccount') }}</el-button>
				</template>
			</vxe-grid>
		</el-card>

		<EditRole ref="editRoleRef" :title="state.title" @handleQuery="handleQuery" />
		<GrantMenu ref="grantMenuRef" />
		<GrantTable ref="grantTableRef" />
		<GrantData ref="grantDataRef" @handleQuery="handleQuery" />
		<GrantApi ref="grantApiRef" />
		<GrantUser v-model:visible="state.grantUserVisible" @change="closeGrantUser" />
	</div>
</template>

<!-- 角色管理 -->
<script lang="ts" setup name="sysRole">
import { onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { useI18n } from 'vue-i18n';
import { auth } from '/@/utils/authFunction';

import EditRole from '/@/views/system/role/component/editRole.vue';
import GrantMenu from '/@/views/system/role/component/grantMenu.vue';
import GrantTable from '/@/views/system/role/component/grantTable.vue';
import GrantData from '/@/views/system/role/component/grantData.vue';
import GrantApi from '/@/views/system/role/component/grantApi.vue';
import GrantUser from '/@/components/selector/userSelectorDialog.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysRoleApi } from '/@/api-services/system/api';
import { PageRoleInput, PageRoleOutput } from '/@/api-services/system/models';

const xGrid = ref<VxeGridInstance>();
const editRoleRef = ref<InstanceType<typeof EditRole>>();
const grantMenuRef = ref<InstanceType<typeof GrantMenu>>();
const grantTableRef = ref<InstanceType<typeof GrantTable>>();
const grantDataRef = ref<InstanceType<typeof GrantData>>();
const grantApiRef = ref<InstanceType<typeof GrantApi>>();
const state = reactive({
	queryParams: {
		name: undefined,
		code: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'orderNo', order: 'asc', descStr: 'desc' },
	},
	visible: false,
	title: '',
	grantUserVisible: false, // 账号选择器
	roleRow: {} as any, // 当前选择的角色
});

const i18n = useI18n();

// 本地存储参数
const localPageParamKey = 'localPageParam:sysRole';
// 表格参数配置
const options = useVxeTable<PageRoleOutput>(
	{
		id: 'sysRole',
		name: i18n.t('message.list.role'),
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: i18n.t('message.list.seq'), width: 60, fixed: 'left' },
			{ field: 'name', title: i18n.t('message.list.roleName'), minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'code', title: i18n.t('message.list.roleCode'), minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'dataScope', title: i18n.t('message.list.dataScope'), minWidth: 150, showOverflow: 'tooltip', slots: { default: 'row_dataScope' } },
			{ field: 'sysFlag', title: i18n.t('message.list.sysFlag'), width: 80, showOverflow: 'tooltip', slots: { default: 'row_sysFlag' } },
			{ field: 'tenantName', title: i18n.t('message.list.tenantName'), minWidth: 180, showOverflow: 'tooltip' },
			{ field: 'orderNo', title: i18n.t('message.list.orderNo'), width: 80, showOverflow: 'tooltip' },
			{ field: 'status', title: i18n.t('message.list.status'), width: 80, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'record', title: i18n.t('message.list.record'), width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: i18n.t('message.list.operation'), fixed: 'right', width: 520, showOverflow: true, slots: { default: 'row_buttons' } },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
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
onMounted(() => {
	state.localPageParam = Local.get(localPageParamKey) || state.localPageParam;
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageRoleInput;
	return getAPI(SysRoleApi).apiSysRolePagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.code = undefined;
	state.queryParams.name = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 打开新增页面
const handleAdd = () => {
	state.title = i18n.t('message.list.addRole');
	editRoleRef.value?.openDialog({ id: undefined, status: 1, orderNo: 100 });
};

// 打开编辑页面
const handleEdit = async (row: any) => {
	state.title = i18n.t('message.list.editRole');
	editRoleRef.value?.openDialog(row);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(i18n.t('message.list.confirmDeleteRole', { roleName: row.name }), i18n.t('message.list.hint'), {
		confirmButtonText: i18n.t('message.list.confirmButtonText'),
		cancelButtonText: i18n.t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysRoleApi).apiSysRoleDeletePost({ id: row.id });
			await handleQuery();
			ElMessage.success(i18n.t('message.list.successDelete'));
		})
		.catch(() => {});
};

// 复制
const handleCopy = (row: any) => {
	ElMessageBox.confirm(i18n.t('message.list.confirmCopyRole', { roleName: row.name }), i18n.t('message.list.hint'), {
		confirmButtonText: i18n.t('message.list.confirmButtonText'),
		cancelButtonText: i18n.t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysRoleApi).apiSysRoleCopyPost({ id: row.id });
			await handleQuery();
			ElMessage.success(i18n.t('message.list.successCopy'));
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<PageRoleOutput> = {
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
		if (auth('sysRole/update')) await handleEdit(row);
	},
};

// 打开授权菜单页面
const openGrantMenu = (row: any) => {
	grantMenuRef.value?.openDrawer(row);
};

// 打开授权表格页面
const openGrantTable = (row: any) => {
	grantTableRef.value?.openDrawer(row);
};

// 打开授权数据范围页面
const openGrantData = (row: any) => {
	grantDataRef.value?.openDialog(row);
};

// 打开授权接口资源页面
const openGrantApi = (row: any) => {
	grantApiRef.value?.openDrawer(row);
};

// 打开授权账号页面
const openGrantUser = (row: any) => {
	state.roleRow = row;
	state.grantUserVisible = true;
};

// 关闭授权账号页面并授权
const closeGrantUser = (data: any) => {
	var userIds = data.map((u: any) => u.id);
	getAPI(SysRoleApi).apiSysRoleGrantUserPost({ id: state.roleRow.id, userIdList: userIds });
	state.grantUserVisible = false;
};
</script>
