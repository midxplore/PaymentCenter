<template>
	<div class="syslogdiff-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="开始时间" prop="name">
							<el-date-picker
								v-model="state.queryParams.startTime"
								type="datetime"
								placeholder="开始时间"
								format="YYYY-MM-DD HH:mm:ss"
								value-format="YYYY-MM-DD HH:mm:ss"
								:shortcuts="shortcuts"
								class="w100"
							/>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="结束时间" prop="code">
							<el-date-picker
								v-model="state.queryParams.endTime"
								type="datetime"
								placeholder="结束时间"
								format="YYYY-MM-DD HH:mm:ss"
								value-format="YYYY-MM-DD HH:mm:ss"
								:shortcuts="shortcuts"
								class="w100"
							/>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysLogDiff/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents" @cell-dblclick="handleView">
				<template #toolbar_buttons> </template>
				<template #toolbar_tools>
					<vxe-button circle icon="vxe-icon-upload" name="导入" code="showImport" class="mr12" />
				</template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_diffType="{ row }">
					<el-tag v-if="row.diffType == 'INSERT'" type="primary"> {{ row.diffType }} </el-tag>
					<el-tag v-else-if="row.diffType == 'UPDATE'" type="success"> {{ row.diffType }} </el-tag>
					<el-tag v-else type="danger"> {{ row.diffType }} </el-tag>
				</template>
				<template #row_buttons="{ row }">
					<el-button icon="ele-InfoFilled" text type="primary" @click="handleView({ row })">详情</el-button>
				</template>
			</vxe-grid>
		</el-card>

		<el-dialog v-model="state.visible" draggable overflow destroy-on-close>
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Document /> </el-icon>
					<span> 日志详情 </span>
				</div>
			</template>
			<el-tabs v-model="state.activeTab">
				<el-tab-pane label="SQL" name="sql">
					<el-scrollbar height="calc(100vh - 250px)">
						<vue-json-pretty :data="state.detail.sql" showLength showIcon showLineNumber showSelectController />
					</el-scrollbar>
				</el-tab-pane>
				<el-tab-pane label="参数" name="parameters" :lazy="true">
					<el-scrollbar height="calc(100vh - 250px)">
						<vue-json-pretty :data="state.detail.parameters" showLength showIcon showLineNumber showSelectController />
					</el-scrollbar>
				</el-tab-pane>
				<el-tab-pane label="操作前记录" name="beforeData" :lazy="true">
					<el-scrollbar height="calc(100vh - 250px)">
						<vue-json-pretty :data="state.detail.beforeData" showLength showIcon showLineNumber showSelectController />
					</el-scrollbar>
				</el-tab-pane>
				<el-tab-pane label="操作后记录" name="afterData" :lazy="true">
					<el-scrollbar height="calc(100vh - 250px)">
						<vue-json-pretty :data="state.detail.afterData" showLength showIcon showLineNumber showSelectController />
					</el-scrollbar>
				</el-tab-pane>
			</el-tabs>
		</el-dialog>
	</div>
</template>

<!-- 差异日志 -->
<script lang="ts" setup name="sysLogDiff">
import { onMounted, reactive, ref } from 'vue';
import { useDateTimeShortCust } from '/@/hooks/dateTimeShortCust';

import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';

import VueJsonPretty from 'vue-json-pretty';
import 'vue-json-pretty/lib/styles.css';
import { StringToObj } from '/@/utils/json-utils';

import { getAPI } from '/@/utils/axios-utils';
import { SysLogDiffApi } from '/@/api-services/system/api';
import { SysLogDiff, PageLogInput } from '/@/api-services/system/models';

const xGrid = ref<VxeGridInstance>();
const shortcuts = useDateTimeShortCust();
const state = reactive({
	queryParams: {
		startTime: undefined,
		endTime: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'id', order: 'desc', descStr: 'desc' },
	},
	visible: false,
	detail: {
		sql: undefined,
		parameters: undefined,
		afterData: undefined,
		beforeData: undefined,
	},
	activeTab: 'sql',
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysDiffLog';
// 表格参数配置
const options = useVxeTable<SysLogDiff>(
	{
		id: 'sysDiffLog',
		name: '差异日志',
		columns: [
			// { type: 'checkbox', width: 40 },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'createTime', title: '操作时间', width: 150, showOverflow: 'tooltip' },
			{ field: 'businessData', title: '业务对象', width: 150, showOverflow: 'tooltip' },
			{ field: 'diffType', title: '操作类型', width: 150, showOverflow: 'tooltip', slots: { default: 'row_diffType' } },
			{ field: 'sql', title: 'Sql语句', minWidth: 250, showOverflow: 'tooltip' },
			{ field: 'parameters', title: '参数', minWidth: 250, showOverflow: 'tooltip' },
			{ field: 'elapsed', title: '耗时(ms)', width: 100, showOverflow: 'tooltip' },
			// { field: 'beforeData', title: '操作前记录', minWidth: 150, showOverflow: 'tooltip' },
			// { field: 'afterData', title: '操作后记录', minWidth: 150, showOverflow: 'tooltip' },
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
onMounted(() => {
	state.localPageParam = Local.get(localPageParamKey) || state.localPageParam;
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageLogInput;
	return getAPI(SysLogDiffApi).apiSysLogDiffPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.startTime = undefined;
	state.queryParams.endTime = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 表格事件
const gridEvents: VxeGridListeners<SysLogDiff> = {
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
};

// 查看详情
const handleView = async ({ row }: any) => {
	const { data } = await getAPI(SysLogDiffApi).apiSysLogDiffDetailIdGet(row.id);
	// 如果请求参数是JSON字符串，则尝试转为JSON对象
	state.detail.sql = StringToObj(data?.result?.sql);
	state.detail.parameters = StringToObj(data?.result?.parameters);
	state.detail.afterData = StringToObj(data?.result?.afterData);
	state.detail.beforeData = StringToObj(data?.result?.beforeData);
	state.visible = true;
};
</script>

<style lang="scss" scoped></style>
