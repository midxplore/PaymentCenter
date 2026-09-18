<template>
	<div class="sysloghttp-container" v-loading="state.loading">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px' }">
			<scEcharts v-if="echartsOption.series.data" height="200px" :option="echartsOption" @clickData="clickData"></scEcharts>
		</el-card>
		<el-card shadow="hover" :body-style="{ padding: '5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="客户端" prop="httpClientName">
							<el-select v-model="state.queryParams.httpClientName" placeholder="请选择客户端" clearable @keyup.enter.native="handleQuery(true)">
								<el-option v-for="item in state.clientNameList" :key="item" :label="item" :value="item" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="模块名" prop="actionName">
							<el-input v-model="state.queryParams.actionName" placeholder="请输入模块名" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="接口描述" prop="httpApiDesc">
							<el-input v-model="state.queryParams.httpApiDesc" placeholder="请输入接口描述" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="请求方式" prop="httpMethod">
							<el-input v-model="state.queryParams.httpMethod" placeholder="请输入请求方式" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="是否成功" prop="isSuccessStatusCode">
							<g-sys-dict v-model="state.queryParams.isSuccessStatusCode" :code="'YesNoEnum'" render-as="select" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="请求地址" prop="requestUrl">
							<el-input v-model="state.queryParams.requestUrl" placeholder="请输入请求地址" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="请求体" prop="requestBody">
							<el-input v-model="state.queryParams.requestBody" placeholder="请输入请求体" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="响应状态码" prop="statusCode">
							<el-input v-model="state.queryParams.statusCode" placeholder="请输入响应状态码" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="响应体" prop="responseBody">
							<el-input v-model="state.queryParams.responseBody" placeholder="请输入响应体" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="异常信息" prop="exception">
							<el-input v-model="state.queryParams.exception" placeholder="请输入异常信息" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="创建时间" prop="createTime">
							<el-date-picker
								type="daterange"
								v-model="state.queryParams.createTimeRange"
								value-format="YYYY-MM-DD HH:mm:ss"
								start-placeholder="开始日期"
								end-placeholder="结束日期"
								:default-time="[new Date('1 00:00:00'), new Date('1 23:59:59')]"
							/>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />
			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysLogHttp/page'"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons> </template>
				<template #toolbar_tools></template>
				<template #empty><el-empty :image-size="200" /></template>
				<template #row_record="{ row }"><ModifyRecord :data="row" /></template>
				<template #row_isSuccessStatusCode="{ row, $index }">
					<g-sys-dict v-model="row.isSuccessStatusCode" :code="'YesNoEnum'" />
				</template>
				<template #row_requestUrl="{ row, $index }">
					<el-button v-if="row.requestUrl" class="ml5" icon="ele-CopyDocument" text type="primary" @click="(event: any) => handleCopyUrl(event, row.requestUrl)" />
					{{ row.requestUrl }}
				</template>
				<template #row_requestBody="{ row, $index }">
					{{ row.requestBody }}
					<el-button v-if="row.requestBody" class="ml5" icon="ele-CopyDocument" text type="primary" @click="commonFun.copyText(row.requestBody?.toString())" />
				</template>
				<template #row_responseBody="{ row, $index }">
					{{ row.responseBody }}
					<el-button v-if="row.responseBody" class="ml5" icon="ele-CopyDocument" text type="primary" @click="commonFun.copyText(row.responseBody?.toString())" />
				</template>
				<template #row_exception="{ row, $index }">
					{{ row.exception }}
					<el-button v-if="row.exception" class="ml5" icon="ele-CopyDocument" text type="primary" @click="commonFun.copyText(row.exception?.toString())" />
				</template>
				<template #row_startTime="{ row, $index }">
					{{ commonFun.dateFormatYMDHMS(row, $index, row.startTime) }}
				</template>
				<template #row_endTime="{ row, $index }">
					{{ commonFun.dateFormatYMDHMS(row, $index, row.endTime) }}
				</template>
				<template #row_buttons="{ row }"> </template>
			</vxe-grid>
		</el-card>
		<logDetail ref="logDetailRef"></logDetail>
	</div>
</template>

<!-- 远程请求日志 -->
<script lang="ts" setup name="sysLogHttp">
import { defineAsyncComponent, onMounted, reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';
import { Local } from '/@/utils/storage';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import commonFunction from '/@/utils/commonFunction';
import 'vue-json-pretty/lib/styles.css';

import logDetail from './component/logDetail.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysLogHttpApi } from '/@/api-services/system/api';
import { PageLogHttpInput, PageLogHttpOutput } from '/@/api-services/system/models';

const scEcharts = defineAsyncComponent(() => import('/@/components/scEcharts/index.vue'));

const commonFun = commonFunction();
const xGrid = ref<VxeGridInstance>();
const logDetailRef = ref<InstanceType<typeof logDetail>>();
const state = reactive({
	loading: false,
	queryParams: {} as PageLogHttpInput,
	localPageParam: {
		pageSize: 20 as number,
		defaultSort: { field: 'id', order: 'asc', descStr: 'asc' },
	},
	visible: false,
	logMaxValue: 1,
	clientNameList: [] as string[],
});
// 本地存储参数
const localPageParamKey = 'localPageParam:sysLogHttp';
// 表格参数配置
const options = useVxeTable<PageLogHttpOutput>(
	{
		id: 'sysLogHttp',
		name: '请求日志',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'createTime', title: '创建时间', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'httpApiDesc', title: '接口描述', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'httpClientName', title: '客户端', minWidth: 110, showOverflow: 'tooltip' },
			{ field: 'actionName', title: '模块名', minWidth: 110, showOverflow: 'tooltip' },
			{ field: 'httpMethod', title: '请求方式', minWidth: 60, showOverflow: 'tooltip' },
			{ field: 'isSuccessStatusCode', title: '是否成功', minWidth: 60, showOverflow: 'tooltip', slots: { default: 'row_isSuccessStatusCode' } },
			{ field: 'requestUrl', title: '请求地址', minWidth: 150, align: 'left', showOverflow: 'tooltip', slots: { default: 'row_requestUrl' } },
			//{ field: 'requestHeaders', title: '请求头', minWidth: 150, showOverflow: 'tooltip' },
			//{ field: 'requestBody', title: '请求体', minWidth: 150, showOverflow: 'tooltip', slots: { default: 'row_requestBody' } },
			{ field: 'statusCode', title: '响应状态码', minWidth: 70, showOverflow: 'tooltip' },
			//{ field: 'responseHeaders', title: '响应头', minWidth: 150, showOverflow: 'tooltip' },
			//{ field: 'responseBody', title: '响应体', minWidth: 150, showOverflow: 'tooltip', slots: { default: 'row_responseBody' } },
			//{ field: 'exception', title: '异常信息', minWidth: 150, showOverflow: 'tooltip', slots: { default: 'row_exception' } },
			{ field: 'startTime', title: '开始时间', minWidth: 150, showOverflow: 'tooltip', slots: { default: 'row_startTime' } },
			{ field: 'endTime', title: '结束时间', minWidth: 150, showOverflow: 'tooltip', slots: { default: 'row_endTime' } },
			{ field: 'elapsed', title: '耗时（毫秒）', minWidth: 90, showOverflow: 'tooltip' },
			{ field: 'createUserName', title: '创建用户', minWidth: 90, showOverflow: 'tooltip' },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: ({ page }) => handleQueryApi(page) } },
		// 排序配置
		sortConfig: { defaultSort: Local.get(localPageParamKey)?.defaultSort || state.localPageParam.defaultSort },
		// 分页配置
		pagerConfig: { pageSize: Local.get(localPageParamKey)?.pageSize || state.localPageParam.pageSize },
		// 导入配置
		// importConfig: { remote: true, importMethod: (options: any) => handleImport(options), slots: { top: 'import_sysLogHttp' } },
		// 工具栏配置
		toolbarConfig: { import: false, export: true },
	}
);

// 页面初始化
onMounted(async () => {
	state.clientNameList = await getAPI(SysLogHttpApi)
		.apiSysLogHttpHttpClientNameGet()
		.then((res) => res.data.result ?? []);
	state.localPageParam = Local.get(localPageParamKey) || state.localPageParam;
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams) => {
	await getYearDayStatsData();
	const params = Object.assign(state.queryParams, {
		page: page.currentPage,
		pageSize: page.pageSize,
		field: state.localPageParam.defaultSort.field,
		order: state.localPageParam.defaultSort.order,
		descStr: 'asc',
	}) as PageLogHttpInput;
	return getAPI(SysLogHttpApi).apiSysLogHttpPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 复制请求地址操作
const handleCopyUrl = (event: PointerEvent, url: string) => {
	event.stopPropagation(); // 阻止事件冒泡
	commonFun.copyText(url);
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.httpMethod = undefined;
	state.queryParams.isSuccessStatusCode = undefined;
	state.queryParams.requestUrl = undefined;
	state.queryParams.requestBody = undefined;
	state.queryParams.statusCode = undefined;
	state.queryParams.responseBody = undefined;
	state.queryParams.exception = undefined;
	state.queryParams.createTime = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 获取主题颜色变量
const colors = ['--el-color-primary-light-9', '--el-color-primary-light-7', '--el-color-primary-light-5', '--el-color-primary-light-3', '--el-color-primary-light-1', '--el-color-primary'].map(
	(variable) => getComputedStyle(document.documentElement).getPropertyValue(variable).trim()
);
const echartsOption = ref({
	title: {
		top: 30,
		left: 'center',
		text: '日志统计',
		show: false,
	},
	tooltip: {
		formatter: function (p: any) {
			return p.data[1] + ' 数据量：' + p.data[0];
		},
	},
	visualMap: {
		show: true,
		inRange: {
			color: colors,
		},
		outOfRange: {
			color: ['#000000'],
		},
		min: 0,
		max: 1000,
		maxOpen: {
			type: 'piecewise',
		},
		type: 'piecewise',
		orient: 'horizontal',
		left: 'right',
	},
	calendar: {
		top: 30,
		left: 30,
		right: 30,
		bottom: 30,
		cellSize: ['auto', 20],
		range: ['', ''],
		splitLine: true,
		dayLabel: {
			firstDay: 1,
			nameMap: 'ZH',
		},
		itemStyle: {
			color: '#ccc',
			borderWidth: 3,
			borderColor: '#fff',
		},
		monthLabel: {
			nameMap: ['一月', '二月', '三月', '四月', '五月', '六月', '七月', '八月', '九月', '十月', '十一月', '十二月'],
		},
		yearLabel: {
			show: false,
		},
	},
	series: {
		type: 'heatmap',
		coordinateSystem: 'calendar',
		data: [],
	},
});

const clickData = (e: any) => {
	if (e[1] < 1) return ElMessage.warning('没有日志数据');
	state.queryParams.createTimeRange = [];
	state.queryParams.createTimeRange.push(e[0]);
	var today = new Date(e[0]);
	let endTime = today.setDate(today.getDate() + 1);
	state.queryParams.createTimeRange.push(new Date(endTime));
	xGrid.value?.commitProxy('query');
};

// 获取统计日志数据
const getYearDayStatsData = async () => {
	let data = [] as any;
	var res = await getAPI(SysLogHttpApi).apiSysLogHttpYearDayStatsGet();
	res.data.result?.forEach((item: any) => {
		data.push([item.date, item.count]);

		if (item.count > state.logMaxValue) state.logMaxValue = item.count; // 计算最大值
	});
	echartsOption.value.visualMap.max = state.logMaxValue;
	echartsOption.value.series.data = data;
	echartsOption.value.calendar.range = [data[0][0], data[data.length - 1][0]];
};

// 表格事件
const gridEvents: VxeGridListeners<PageLogHttpOutput> = {
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
	async cellDblclick({ row, column }) {
		state.loading = true;
		try {
			const res = await getAPI(SysLogHttpApi).apiSysLogHttpDetailGet(row.id as any);
			logDetailRef.value?.openDialog(res.data.result ?? {});
		} finally {
			state.loading = false;
		}
	},
};
</script>

<style lang="less" scoped></style>
