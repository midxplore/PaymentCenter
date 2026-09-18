<template>
	<div class="syslogvis-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px' }">
			<scEcharts v-if="echartsOption.series.data" height="200px" :option="echartsOption" @clickData="clickData"></scEcharts>
		</el-card>

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
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="方法名称">
							<el-input v-model="state.queryParams.actionName" placeholder="方法名称" clearable />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="账号名称">
							<el-input v-model="state.queryParams.account" placeholder="账号名称" clearable />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="状态">
							<el-select v-model="state.queryParams.status" placeholder="状态" clearable>
								<el-option label="成功" :value="200" />
								<el-option label="失败" :value="400" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="耗时">
							<el-input v-model="state.queryParams.elapsed" placeholder="耗时>?MS" clearable />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="IP地址">
							<el-input v-model="state.queryParams.remoteIp" placeholder="IP地址" clearable />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysLogVis/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button icon="ele-MapLocation" type="primary" @click="handleMap" v-auth="'sysLogVis/visMap'"> 热力图 </el-button>
					<el-button icon="ele-DeleteFilled" type="danger" @click="handleClear" v-auth="'sysLogVis/clear'"> 清空 </el-button>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_status="{ row }">
					<el-tag v-if="row.status === '200'" type="success">成功</el-tag>
					<el-tag v-else-if="row.status === '304'" type="warning">成功</el-tag>
					<el-tag v-else type="danger">失败</el-tag>
				</template>
			</vxe-grid>
		</el-card>

		<VisMap ref="mapRef" :title="state.title" />
	</div>
</template>

<!-- 访问日志 -->
<script lang="ts" setup name="sysLogVis">
import { onMounted, reactive, ref, defineAsyncComponent } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useDateTimeShortCust } from '/@/hooks/dateTimeShortCust';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { formatDate } from '/@/utils/formatTime';

import { getAPI } from '/@/utils/axios-utils';
import { SysLogVisApi } from '/@/api-services/system/api';
import { SysLogVis, PageLogInput } from '/@/api-services/system/models';

const VisMap = defineAsyncComponent(() => import('./component/visMap.vue'));
const scEcharts = defineAsyncComponent(() => import('/@/components/scEcharts/index.vue'));

const xGrid = ref<VxeGridInstance>();
const mapRef = ref<InstanceType<typeof VisMap>>();
const shortcuts = useDateTimeShortCust();
const state = reactive({
	queryParams: {
		startTime: undefined as any,
		endTime: undefined as any,
		status: undefined,
		actionName: undefined,
		account: undefined,
		elapsed: undefined,
		remoteIp: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'id', order: 'desc', descStr: 'desc' },
	},
	title: '',
	logMaxValue: 1,
});

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

// 本地存储参数
const localPageParamKey = 'localPageParam:sysVisLog';
// 表格参数配置
const options = useVxeTable<SysLogVis>(
	{
		id: 'sysVisLog',
		name: '访问日志',
		columns: [
			// { type: 'checkbox', width: 40 },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'logDateTime', title: '日志时间', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'displayTitle', title: '显示名称', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'controllerName', title: '方法名称', minWidth: 350, showOverflow: 'tooltip' },
			{ field: 'account', title: '账号名称', minWidth: 150, showOverflow: 'tooltip' },
			// { field: 'realName', title: '真实姓名', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'remoteIp', title: 'IP地址', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'location', title: '登录地点', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'longitude', title: '经度', minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'latitude', title: '纬度', minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'browser', title: '浏览器', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'os', title: '操作系统', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'status', title: '状态', minWidth: 70, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'elapsed', title: '耗时(ms)', minWidth: 100, showOverflow: 'tooltip' },
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

	await getYearDayStatsData();
});

// 获取统计日志数据
const getYearDayStatsData = async () => {
	let data = [] as any;
	var res = await getAPI(SysLogVisApi).apiSysLogVisYearDayStatsGet();
	res.data.result?.forEach((item: any) => {
		data.push([item.date, item.count]);

		if (item.count > state.logMaxValue) state.logMaxValue = item.count; // 计算最大值
	});
	echartsOption.value.visualMap.max = state.logMaxValue;
	echartsOption.value.series.data = data;
	echartsOption.value.calendar.range = [data[0][0], data[data.length - 1][0]];
};

// 点击统计日志返回的数据
const clickData = (e: any) => {
	if (e[1] < 1) return ElMessage.warning('没有日志数据');
	state.queryParams.startTime = e[0];
	var today = new Date(state.queryParams.startTime);
	let endTime = today.setDate(today.getDate() + 1);
	state.queryParams.endTime = formatDate(new Date(endTime), 'YYYY-mm-dd');
	xGrid.value?.commitProxy('query');
};

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageLogInput;
	return getAPI(SysLogVisApi).apiSysLogVisPagePost(params);
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
	state.queryParams.status = undefined;
	state.queryParams.actionName = undefined;
	state.queryParams.account = undefined;
	state.queryParams.elapsed = undefined;
	state.queryParams.remoteIp = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 表格事件
const gridEvents: VxeGridListeners<SysLogVis> = {
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

// 清空日志
const handleClear = async () => {
	ElMessageBox.confirm(`确定要清空日志?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			options.loading = true;
			await getAPI(SysLogVisApi).apiSysLogVisClearPost();
			options.loading = false;
			ElMessage.success('清空成功');
			await handleQuery();
		})
		.catch(() => {});
};

// 打开访问热力图
const handleMap = async () => {
	state.title = '访问者热力图';
	mapRef.value?.openDialog();
};
</script>
