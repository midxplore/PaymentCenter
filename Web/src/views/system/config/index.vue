<template>
	<div class="sys-config-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="配置名称" prop="name">
							<el-input v-model="state.queryParams.name" placeholder="配置名称" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="配置编码" prop="code">
							<el-input v-model="state.queryParams.code" placeholder="配置编码" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="分组编码" prop="groupCode">
							<el-select v-model="state.queryParams.groupCode" clearable placeholder="分组编码" @clear="state.queryParams.groupCode = undefined">
								<el-option v-for="item in state.groupList" :key="item" :label="item" :value="item" />
							</el-select>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysConfig/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'sysConfig/add'"> 新增 </el-button>
					<!-- <el-button v-if="state.selectList.length > 0" type="danger" icon="ele-Delete" @click="handleBacthDelete" > 批量删除 </el-button> -->
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_sysFlag="{ row }">
					<el-tag v-if="row.sysFlag === 1" type="success">是</el-tag>
					<el-tag v-else type="danger">否</el-tag>
				</template>
				<template #row_record="{ row }">
					<ModifyRecord :data="row" />
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip content="编辑" placement="top">
						<el-button icon="ele-Edit" text type="primary" v-auth="'sysConfig/update'" @click="handleEdit(row)"> </el-button>
					</el-tooltip>
					<el-tooltip content="删除" placement="top">
						<el-button icon="ele-Delete" text type="danger" v-auth="'sysConfig/delete'" :disabled="row.sysFlag === 1" @click="handleDelete(row)"> </el-button>
					</el-tooltip>
				</template>
			</vxe-grid>
		</el-card>

		<EditConfig ref="editConfigRef" :title="state.title" :groupList="state.groupList" @updateData="updateData" />
	</div>
</template>

<!-- 配置参数 -->
<script lang="ts" setup name="sysConfig">
import { onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { auth } from '/@/utils/authFunction';

import EditConfig from '/@/views/system/config/component/editConfig.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysConfigApi } from '/@/api-services/system/api';
import { SysConfig, PageConfigInput } from '/@/api-services/system/models';

const xGrid = ref<VxeGridInstance>();
const editConfigRef = ref<InstanceType<typeof EditConfig>>();
const state = reactive({
	queryParams: {
		name: undefined,
		code: undefined,
		groupCode: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'orderNo', order: 'asc', descStr: 'desc' },
	},
	visible: false,
	title: '',
	groupList: [] as string[],
	selectList: [] as SysConfig[],
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysConfig';
// 表格参数配置
const options = useVxeTable<SysConfig>(
	{
		id: 'sysConfig',
		name: '参数配置',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'name', title: '配置名称', minWidth: 200, headerAlign: 'center', align: 'left', showOverflow: 'tooltip', sortable: true },
			{ field: 'code', title: '配置编码', minWidth: 200, headerAlign: 'center', align: 'left', showOverflow: 'tooltip', sortable: true },
			{ field: 'value', title: '属性值', minWidth: 200, showOverflow: 'tooltip', sortable: true },
			{ field: 'sysFlag', title: '内置参数', width: 80, showOverflow: 'tooltip', sortable: true, slots: { default: 'row_sysFlag' } },
			{ field: 'groupCode', title: '分组编码', minWidth: 120, showOverflow: 'tooltip', sortable: true },
			{ field: 'orderNo', title: '排序', width: 80, showOverflow: 'tooltip', sortable: true },
			{ field: 'remark', title: '备注', minWidth: 300, headerAlign: 'center', align: 'left', showOverflow: 'tooltip', sortable: true },
			{ field: 'record', title: '修改记录', width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 100, showOverflow: true, slots: { default: 'row_buttons' } },
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
onMounted(async () => {
	state.localPageParam = Local.get(localPageParamKey) || state.localPageParam;
	await fetchGroupData();
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageConfigInput;
	return getAPI(SysConfigApi).apiSysConfigPagePost(params);
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
	state.queryParams.groupCode = undefined;
	// 调用vxe-grid的commitProxy(reload)方法，触发表格重新加载数据
	await xGrid.value?.commitProxy('reload');
};

// 打开新增页面
const handleAdd = () => {
	state.title = '添加配置';
	editConfigRef.value?.openDialog({ sysFlag: 2, orderNo: 100 });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = '编辑配置';
	editConfigRef.value?.openDialog(row);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(`确定删除配置：【${row.name}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysConfigApi).apiSysConfigDeletePost({ id: row.id });
			handleQuery();
			ElMessage.success('删除成功');
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<SysConfig> = {
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
		if (auth('sysConfig/update')) await handleEdit(row);
	},
};

// 获取分组
const fetchGroupData = async () => {
	const res = await getAPI(SysConfigApi).apiSysConfigGroupListGet();
	state.groupList = res.data.result ?? [];
};

// 更新数据
const updateData = async () => {
	await handleQuery();
	fetchGroupData();
};

// // 批量删除
// const handleBacthDelete = () => {
// 	if (state.selectList.length == 0) return false;
// 	ElMessageBox.confirm(`确定批量删除【${state.selectList[0].name}】等${state.selectList.length}个配置?`, '提示', {
// 		confirmButtonText: '确定',
// 		cancelButtonText: '取消',
// 		type: 'warning',
// 	})
// 		.then(async () => {
// 			const ids = state.selectList.map((item: any) => item.id);
// 			var res = await getAPI(SysConfigApi).apiSysConfigBatchDeletePost(ids);
// 			handleQuery();
// 			ElMessage.success('删除成功');
// 		})
// 		.catch(() => {});
// };
</script>
