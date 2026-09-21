<template>
	<div class="sys-open-access-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="身份标识" prop="accessKey">
							<el-input v-model="state.queryParams.accessKey" placeholder="身份标识" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysOpenAccess/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'sysOpenAccess/add'"> 新增 </el-button>
					<el-button icon="ele-QuestionFilled" @click="openHelp"> 说明 </el-button>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_record="{ row }">
					<ModifyRecord :data="row" />
				</template>
				<template #row_status="{ row }">
					<el-tag :type="row.status === 2 ? 'danger' : 'success'">{{ row.status === 2 ? '停用' : '启用' }}</el-tag>
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip content="编辑" placement="top">
						<el-button icon="ele-Edit" size="small" text type="primary" @click="handleEdit(row)" v-auth="'sysOpenAccess/update'" />
					</el-tooltip>
					<el-tooltip content="删除" placement="top">
						<el-button icon="ele-Delete" size="small" text type="danger" @click="handleDelete(row)" v-auth="'sysOpenAccess/delete'" />
					</el-tooltip>
					<el-tooltip content="生成签名" placement="top">
						<el-button icon="ele-EditPen" size="small" text type="warning" @click="handleSign(row)" v-auth="'sysOpenAccess/generateSignature'" />
					</el-tooltip>
				</template>
			</vxe-grid>
		</el-card>

		<EditOpenAccess ref="editRef" :title="state.title" @handleQuery="handleQuery" />
		<HelpView ref="helpRef" />
		<GenerateSign ref="signRef" />
	</div>
</template>

<!-- 开放接口（平台管理 → 开放接口）-->
<script lang="ts" setup name="sysOpenAccess">
import { onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { auth } from '/@/utils/authFunction';

import EditOpenAccess from '/@/views/system/openAccess/component/editOpenAccess.vue';
import HelpView from '/@/views/system/openAccess/component/helpView.vue';
import GenerateSign from '/@/views/system/openAccess/component/generateSign.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysOpenAccessApi } from '/@/api-services/system/api';
import { PageOpenAccessInput, OpenAccessOutput } from '/@/api-services/system/models';

const xGrid = ref<VxeGridInstance>();
const editRef = ref<InstanceType<typeof EditOpenAccess>>();
const helpRef = ref<InstanceType<typeof HelpView>>();
const signRef = ref<InstanceType<typeof GenerateSign>>();
const state = reactive({
	queryParams: {
		accessKey: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		// 按 Id 倒序：OpenAccessOutput 上没有 orderNo 字段（原值是从其它页面复制来的，排序实际无效）
		defaultSort: { field: 'id', order: 'desc', descStr: 'desc' },
	},
	title: '',
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysOpenAccess';
// 表格参数配置
const options = useVxeTable<OpenAccessOutput>(
	{
		id: 'sysOpenAccess',
		name: '开放接口身份',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'accessKey', title: '身份标识', minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'accessSecret', title: '密钥（掩码）', minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'scopes', title: '权限范围', minWidth: 160, showOverflow: 'tooltip' },
			{ field: 'status', title: '状态', width: 90, slots: { default: 'row_status' } },
			{ field: 'bindUserAccount', title: '绑定用户账号', minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'bindTenantName', title: '绑定租户名称', minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'record', title: '修改记录', width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: '操作', fixed: 'right', minWidth: 100, showOverflow: true, slots: { default: 'row_buttons' } },
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
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageOpenAccessInput;
	return getAPI(SysOpenAccessApi).apiSysOpenAccessPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.accessKey = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 打开新增页面
const handleAdd = () => {
	state.title = '添加开放接口身份';
	editRef.value?.openDialog({ type: 1 });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = '编辑开放接口身份';
	editRef.value?.openDialog(row);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(`确定删除开放接口身份：【${row.accessKey}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysOpenAccessApi).apiSysOpenAccessDeletePost({ id: row.id });
			handleQuery();
			ElMessage.success('删除成功');
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<OpenAccessOutput> = {
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
		if (auth('sysOpenAccess/update')) await handleEdit(row);
	},
};

// 打开说明页面
const openHelp = () => {
	helpRef.value?.openDialog();
};

// 打开生成签名
const handleSign = (row: any) => {
	signRef.value?.openDialog(row);
};
</script>
