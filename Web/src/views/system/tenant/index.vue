<template>
	<div class="sys-tenant-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="租户名称" prop="name">
							<el-input v-model="state.queryParams.name" placeholder="租户名称" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="联系电话" prop="code">
							<el-input v-model="state.queryParams.phone" placeholder="联系电话" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysTenant/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents" :loading="state.isLoading">
				<template #toolbar_buttons>
					<el-button icon="ele-Plus" type="primary" @click="handleAdd" v-auth="'sysTenant/add'"> 新增 </el-button>
					<el-button type="danger" icon="ele-Refresh" @click="syncTenantDb"> 同步所有租户数据库 </el-button>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_tenantType="{ row }">
					<el-tag v-if="row.tenantType === 0" type="info">ID隔离</el-tag>
					<el-tag v-else type="primary">库隔离</el-tag>
				</template>
				<template #row_status="scope">
					<el-switch v-model="scope.row.status" :active-value="1" :inactive-value="2" size="small" @change="changeStatus(scope)" :disabled="scope.row.id == 1300000000001" />
				</template>
				<template #row_dbType="{ row }">
					<g-sys-dict v-model="row.dbType" code="db_type" render-as="tag" />
				</template>
				<template #row_record="{ row }">
					<ModifyRecord :data="row" />
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip content="编辑" placement="top">
						<el-button icon="ele-Edit" size="small" text type="primary" @click="handleEdit(row)" v-auth="'sysTenant/update'" />
					</el-tooltip>
					<el-tooltip content="删除" placement="top">
						<el-button icon="ele-Delete" text type="danger" @click="handleDelete(row)" v-auth="'sysTenant/delete'" :disabled="row.id == 1300000000001"> </el-button>
					</el-tooltip>
					<el-tooltip content="重置密码" placement="top">
						<el-button icon="ele-RefreshLeft" text type="danger" @click="resetTenantPwd(row)" v-auth="'sysTenant/resetPwd'"> </el-button>
					</el-tooltip>

					<el-button icon="ele-Coin" size="small" text type="danger" @click="createTenantData(row)" :v-auth="'sysTenant/createDb'" v-if="row.tenantType === 0"> 创建租户数据 </el-button>
					<el-button icon="ele-Coin" size="small" text type="danger" @click="createTenantDb(row)" :v-auth="'sysTenant/createDb'" v-else> 创建租户库表 </el-button>
					<el-button icon="ele-Menu" size="small" text type="primary" @click="openGrantMenu(row)" v-auth="'sysTenant/grantMenu'"> 授权菜单 </el-button>
					<el-button icon="ele-Link" size="small" text type="primary" @click="openGrantApi(row)"> 接口黑名单 </el-button>
				</template>
			</vxe-grid>
		</el-card>

		<EditTenant ref="editTenantRef" :title="state.title" @handleQuery="handleQuery" />
		<GrantMenu ref="grantMenuRef" />
		<GrantApi ref="grantApiRef" />
	</div>
</template>

<!-- 租户管理 -->
<script lang="ts" setup name="sysTenant">
import { onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage, ElButton } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { auth } from '/@/utils/authFunction';

import EditTenant from '/@/views/system/tenant/component/editTenant.vue';
import GrantMenu from '/@/views/system/tenant/component/grantMenu.vue';
import GrantApi from '/@/views/system/role/component/grantApi.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysTenantApi } from '/@/api-services/system/api';
import { PageTenantInput, TenantOutput } from '/@/api-services/system/models';

const xGrid = ref<VxeGridInstance>();
const editTenantRef = ref<InstanceType<typeof EditTenant>>();
const grantMenuRef = ref<InstanceType<typeof GrantMenu>>();
const grantApiRef = ref<InstanceType<typeof GrantApi>>();
const state = reactive({
	queryParams: {
		name: undefined,
		phone: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'orderNo', order: 'asc', descStr: 'desc' },
	},
	title: '',
	/** 是否加载中 */
	isLoading: false,
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysTenant';
// 表格参数配置
const options = useVxeTable<TenantOutput>(
	{
		id: 'sysTenant',
		name: '租户信息',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'name', title: '租户名称', minWidth: 160, showOverflow: 'tooltip' },
			{ field: 'adminAccount', title: '租管账号', minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'phone', title: '电话', minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'host', title: '域名', showOverflow: 'tooltip' },
			{ field: 'email', title: '邮箱', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'expirationTime', title: '过期时间', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'tenantType', title: '租户类型', minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_tenantType' } },
			{ field: 'status', title: '状态', minWidth: 100, slots: { default: 'row_status' } },
			{ field: 'configId', title: '数据库标识', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'dbType', title: '数据库类型', minWidth: 120, showOverflow: 'tooltip', slots: { default: 'row_dbType' } },
			{ field: 'connection', title: '数据库连接', minWidth: 300, showOverflow: 'tooltip' },
			{ field: 'slaveConnections', title: '从库连接', minWidth: 300, showOverflow: 'tooltip' },
			{ field: 'orderNo', title: '排序', width: 80, showOverflow: 'tooltip' },
			{ field: 'record', title: '修改记录', width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 'auto', showOverflow: true, slots: { default: 'row_buttons' } },
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
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageTenantInput;
	return getAPI(SysTenantApi).apiSysTenantPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.name = undefined;
	state.queryParams.phone = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 打开新增页面
const handleAdd = () => {
	state.title = '添加租户';
	editTenantRef.value?.openDialog({ tenantType: 0, orderNo: 100, host: '' });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = '编辑租户';
	editTenantRef.value?.openDialog(row);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(`确定删除租户：【${row.name}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysTenantApi).apiSysTenantDeletePost({ id: row.id });
			await handleQuery();
			ElMessage.success('删除成功');
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<TenantOutput> = {
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
		if (auth('sysTenant/update')) await handleEdit(row);
	},
};

// 打开授权菜单页面
const openGrantMenu = async (row: any) => {
	grantMenuRef.value?.openDialog(row);
};

// 打开授权接口页面
const openGrantApi = async (row: any) => {
	grantApiRef.value?.openDrawer(row);
};

// 创建租户库
const createTenantDb = (row: any) => {
	ElMessageBox.confirm(`确定创建/更新租户数据库：【${row.name}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			try {
				state.isLoading = true;
				await getAPI(SysTenantApi).apiSysTenantInitTenantDbPost({ id: row.id });
				ElMessage.success('创建/更新租户数据库成功');
			} finally {
				state.isLoading = false;
			}
		})
		.catch(() => {});
};

// 创建租户数据
const createTenantData = (row: any) => {
	ElMessageBox.confirm(`确定创建/更新租户数据：【${row.name}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysTenantApi).apiSysTenantInitTenantDataPost({ id: row.id });
			ElMessage.success('创建/更新租户数据成功');
		})
		.catch(() => {});
};

// 修改状态
const changeStatus = async (scope: any) => {
	getAPI(SysTenantApi)
		.apiSysTenantSetStatusPost({ id: scope.row.id, status: scope.row.status })
		.then(() => {
			ElMessage.success('租户状态设置成功');
		})
		.catch(async () => {
			scope.row.status = scope.row.status === 1 ? 2 : 1;
			await xGrid.value?.updateStatus(scope);
		});
};

// 重置密码
const resetTenantPwd = async (row: any) => {
	ElMessageBox.confirm(`确定重置密码：【${row.name}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysTenantApi)
				.apiSysTenantResetPwdPost({ userId: row.userId })
				.then((res) => {
					ElMessage.success(`密码重置成功为：${res.data.result}`);
				});
		})
		.catch(() => {});
};

// 同步所有租户数据库
const syncTenantDb = () => {
	ElMessageBox.confirm(`确定同步所有租户数据库?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			options.loading = true;
			await getAPI(SysTenantApi).apiSysTenantSyncTenantDbPost();
			ElMessage.success('同步成功');
			options.loading = false;
		})
		.catch(() => {});
};
</script>
