<template>
	<div class="sysLdap-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="关键字">
							<el-input v-model="state.queryParams.keyword" placeholder="请输入模糊查询关键字" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysLdap/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
					<el-button icon="ele-Filter" type="primary" :loading="options.loading" @click="state.visible = true" v-auth="'sysLdap/page'" style="margin-left: 12px"> 高级查询</el-button>
				</el-col>
			</el-row>
		</el-card>

		<el-dialog v-model="state.visible" :width="600" draggable overflow>
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Filter /> </el-icon>
					<span>高级查询</span>
				</div>
			</template>
			<el-form :model="state.queryParams" ref="formRef" label-width="auto" style="height: 300px">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb8">
						<el-form-item label="主机">
							<el-input v-model="state.queryParams.host" clearable placeholder="请输入主机" @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<template #footer>
				<div class="filter-dialog-footer">
					<el-text type="danger"></el-text>
					<el-button icon="ele-Close" type="info" :loading="options.loading" @click="state.visible = false">关闭</el-button>
					<el-button icon="ele-Search" type="primary" :loading="options.loading" @click="handleQuery">查询</el-button>
					<el-button icon="ele-RefreshRight" type="primary" :loading="options.loading" @click="resetQueryFliter">重置</el-button>
				</div>
			</template>
		</el-dialog>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'sysLdap/add'"> 新增 </el-button>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_status="{ row }">
					<el-tag v-if="row.status === 1">启用</el-tag>
					<el-tag v-else type="danger">禁用</el-tag>
				</template>
				<template #row_record="{ row }">
					<ModifyRecord :data="row" />
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip content="编辑" placement="top">
						<el-button icon="ele-Edit" text type="primary" v-auth="'sysLdap/update'" @click="handleEdit(row)"> </el-button>
					</el-tooltip>
					<el-tooltip content="删除" placement="top">
						<el-button icon="ele-Delete" text type="danger" v-auth="'sysLdap/delete'" @click="handleDelete(row)"> </el-button>
					</el-tooltip>
					<el-button icon="ele-Refresh" text type="danger" v-auth="'sysLdap/syncUser'" @click="handleSync(row)">同步域账户</el-button>
				</template>
			</vxe-grid>
		</el-card>

		<PrintDialog ref="printRef" :title="state.title" @reloadTable="handleQuery" />
		<EditDialog ref="editRef" :title="state.title" @handleQuery="handleQuery" />
	</div>
</template>

<!-- 域用户管理 -->
<script lang="ts" setup name="sysLdap">
import { defineAsyncComponent, ref, reactive, onMounted } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { auths } from '/@/utils/authFunction';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { auth } from '/@/utils/authFunction';

import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysLdapApi } from '/@/api-services/system/api';
import { SysLdap, PageLdapInput } from '/@/api-services/system/models';

// 异步引用组件
const EditDialog = defineAsyncComponent(() => import('/@/views/system/ldap/component/editLdap.vue'));

const xGrid = ref<VxeGridInstance>();
const editRef = ref<InstanceType<typeof EditDialog>>();
const state = reactive({
	queryParams: {
		keyword: '',
		host: '',
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'id', order: 'asc', descStr: 'desc' },
	},
	visible: false,
	title: '',
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysLdap';
// 表格参数配置
const options = useVxeTable<SysLdap>(
	{
		id: 'sysLdap',
		name: '系统域登录信息配置',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'host', title: '主机', minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'port', title: '端口', minWidth: 90, showOverflow: 'tooltip' },
			{ field: 'baseDn', title: '用户搜索基准', minWidth: 140, showOverflow: 'tooltip' },
			{ field: 'bindDn', title: '绑定DN', minWidth: 140, showOverflow: 'tooltip' },
			{ field: 'authFilter', title: '用户过滤规则', minWidth: 140, showOverflow: 'tooltip' },
			{ field: 'version', title: 'Ldap版本', minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'status', title: '状态', width: 80, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'record', title: '修改记录', width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 200, showOverflow: true, slots: { default: 'row_buttons' }, visible: auths(['sysLdap/update', 'sysLdap/delete', 'sysLdap/syncUser']) },
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
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageLdapInput;
	return getAPI(SysLdapApi).apiSysLdapPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.keyword = '';
	await xGrid.value?.commitProxy('reload');
};

// 重置高级查询
const resetQueryFliter = async () => {
	state.queryParams.host = '';
	await handleQuery();
};

// 打开新增页面
const handleAdd = () => {
	state.title = '添加系统域登录信息配置';
	editRef.value?.showDialog({ status: 1 });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = '编辑系统域登录信息配置';
	editRef.value?.showDialog(row);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(`确定要删除吗?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysLdapApi).apiSysLdapDeletePost(row);
			await handleQuery();
			ElMessage.success('删除成功');
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<SysLdap> = {
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
		if (auth('sysLdap/update')) await handleEdit(row);
	},
};

// 同步域账户
const handleSync = (row: any) => {
	ElMessageBox.confirm(`确定要同步域登录信息配置：【${row.host}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysLdapApi).apiSysLdapSyncUserPost({ id: row.id });
			handleQuery();
			ElMessage.success('同步成功');
		})
		.catch(() => {});
};

// 打开打印页面
const handlePrint = async (row: any) => {
	state.title = '打印系统域登录信息配置表';
};
</script>

<style lang="scss" scoped>
:deep(.el-input),
:deep(.el-select),
:deep(.el-input-number) {
	width: 100%;
}
</style>
