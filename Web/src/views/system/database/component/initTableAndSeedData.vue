<template>
	<div class="sys-initTableAndSeedData-container">
		<vxe-modal v-model="state.visible" title="初始化库表结构及种子数据" fullscreen show-zoom resize width="100vw" height="100vh">
			<splitpanes class="default-theme">
				<pane size="50">
					<vxe-grid ref="xGridEntity" class="xGrid-style" v-bind="optionsEntity" v-on="gridEventsEntity">
						<template #toolbar_buttons>
							<el-button type="warning" icon="ele-Refresh" @click="handleInitTable">
								初始化表结构<span v-if="state.entitySelectedRows.length > 0">({{ state.entitySelectedRows.length }})</span>
							</el-button>
						</template>
						<template #row_columnCount="{ row }">
							<el-tag type="primary"> {{ row.columnCount }} </el-tag>
						</template>
					</vxe-grid>
				</pane>

				<pane size="50">
					<vxe-grid ref="xGridSeed" class="xGrid-style" v-bind="optionsSeed" v-on="gridEventsSeed">
						<template #toolbar_buttons>
							<el-button type="warning" icon="ele-Refresh" @click="handleInitSeedData">
								初始化种子数据<span v-if="state.seedSelectedRows.length > 0">({{ state.seedSelectedRows.length }})</span>
							</el-button>
						</template>
						<template #row_count="{ row }">
							<el-tag type="primary"> {{ row.count }} </el-tag>
						</template>
					</vxe-grid>
				</pane>
			</splitpanes>
		</vxe-modal>
	</div>
</template>

<script lang="tsx" setup name="sysInitTableAndSeedData">
import { reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Splitpanes, Pane } from 'splitpanes';
import 'splitpanes/dist/splitpanes.css';

import { getAPI } from '/@/utils/axios-utils';
import { SysCodeGenApi, SysDatabaseApi } from '/@/api-services/system/api';

const xGridEntity = ref<VxeGridInstance>();
const xGridSeed = ref<VxeGridInstance>();

const state = reactive({
	visible: false,
	configId: '',
	entityTotalSum: [] as any,
	entitySelectedRows: [] as any[],
	seedTotalSum: [] as any,
	seedSelectedRows: [] as any[],
});

// 表格参数配置-实体数据
const optionsEntity = useVxeTable(
	{
		id: 'entity',
		name: '实体数据',
		columns: [
			{ type: 'checkbox', width: 40, fixed: 'left' },
			{ type: 'seq', title: '序号', width: 50, fixed: 'left' },
			{ field: 'entityName', title: '实体名称', showOverflow: 'tooltip', align: 'left' },
			{ field: 'tableName', title: '库表名称', showOverflow: 'tooltip', align: 'left' },
			{ field: 'tableComment', title: '描述', showOverflow: 'tooltip', align: 'left' },
			{ field: 'columnCount', title: '字段个数', showOverflow: 'tooltip', slots: { default: 'row_columnCount' } },
			{ field: 'assemblyName', title: '所属程序集', showOverflow: 'tooltip', align: 'left' },
		],
	},
	{
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: ({ page, sort }) => getEntityQueryApi(page, sort) } },
		// 分页配置
		pagerConfig: { enabled: false },
		// 工具栏配置
		toolbarConfig: { export: true },
		// 多选配置
		checkboxConfig: { range: true, highlight: false },
		// 表尾配置
		showFooter: true,
	}
);

// 查询所有实体列表
const getEntityQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	return await getAPI(SysCodeGenApi).apiSysCodeGenTableListConfigIdGet(state.configId);
};

// 表格事件-实体数据
const gridEventsEntity: VxeGridListeners = {
	// 多选事件
	checkboxChange({ records }) {
		state.entitySelectedRows = records;
	},
	// 全选/全不选事件
	checkboxAll({ records }) {
		state.entitySelectedRows = records;
	},
};

// 初始化表结构
const handleInitTable = async () => {
	if (!state.configId) {
		ElMessage.warning('请先选择库名');
		return;
	}
	if (!state.entitySelectedRows.length) {
		ElMessage.warning('请至少选择一个实体');
		return;
	}

	try {
		optionsEntity.loading = true;
		const params = {
			configId: state.configId,
			entityNames: state.entitySelectedRows.map((row) => row.entityName),
		};
		await getAPI(SysDatabaseApi).apiSysDatabaseInitTablePost(params);
		ElMessage.success('初始化表结构操作成功');
	} catch (error) {
		ElMessage.error('初始化表结构操作失败');
	} finally {
		optionsEntity.loading = false;
	}
};

// 表格参数配置-种子数据
const optionsSeed = useVxeTable(
	{
		id: 'seed',
		name: '种子数据',
		columns: [
			{ type: 'checkbox', width: 40, fixed: 'left' },
			{ type: 'seq', title: '序号', width: 50, fixed: 'left' },
			{ field: 'name', title: '种子名称', showOverflow: 'tooltip', align: 'left' },
			{ field: 'description', title: '描述', showOverflow: 'tooltip', align: 'left' },
			{ field: 'order', title: '执行顺序', showOverflow: 'tooltip' },
			{ field: 'count', title: '种子个数', showOverflow: 'tooltip', slots: { default: 'row_count' } },
			{ field: 'assemblyName', title: '所属程序集', showOverflow: 'tooltip', align: 'left' },
		],
	},
	{
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: ({ page, sort }) => getSeedQueryApi(page, sort) } },
		// 分页配置
		pagerConfig: { enabled: false },
		// 工具栏配置
		toolbarConfig: { export: true },
		// 多选配置
		checkboxConfig: { range: true, highlight: false },
		// 表尾配置
		showFooter: true,
	}
);

// 查询所有种子列表
const getSeedQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	return await getAPI(SysDatabaseApi).apiSysDatabaseSeedDataListGet(state.configId);
};

// 表格事件-种子数据
const gridEventsSeed: VxeGridListeners = {
	// 多选事件
	checkboxChange({ records }) {
		state.seedSelectedRows = records;
	},
	// 全选/全不选事件
	checkboxAll({ records }) {
		state.seedSelectedRows = records;
	},
};

// 生成种子数据操作
const handleInitSeedData = async () => {
	if (!state.configId) {
		ElMessage.warning('请先选择库名');
		return;
	}
	if (!state.seedSelectedRows.length) {
		ElMessage.warning('请至少选择一个实体');
		return;
	}
	optionsSeed.loading = true;
	try {
		const params = {
			configId: state.configId,
			seedNames: state.seedSelectedRows.map((row) => ({
				name: row.name,
				assemblyName: row.assemblyName,
				order: row.order,
			})),
		};
		await getAPI(SysDatabaseApi).apiSysDatabaseInitSeedDataPost(params);
		ElMessage.success('生成种子数据操作成功');
	} catch (error) {
		ElMessage.error('生成种子数据操作失败');
	} finally {
		optionsSeed.loading = false;
	}
};

// 打开弹窗
const openDialog = (configId: any) => {
	state.configId = configId;
	state.visible = true;
};

// 导出对象
defineExpose({ openDialog });
</script>

<style lang="scss" scoped></style>
