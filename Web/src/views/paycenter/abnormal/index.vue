<template>
	<div class="pay-abnormal-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="上报订单号" prop="reportOrderNo">
							<el-input v-model="state.queryParams.reportOrderNo" placeholder="上报的订单号" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="凭证号" prop="voucherNo">
							<el-input v-model="state.queryParams.voucherNo" placeholder="凭证号" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="异常原因" prop="reason">
							<el-select v-model="state.queryParams.reason" placeholder="异常原因" clearable style="width: 100%">
								<el-option v-for="item in reasonOptions" :key="item.value" :label="item.label" :value="item.value" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="处理状态" prop="handleStatus">
							<el-select v-model="state.queryParams.handleStatus" placeholder="处理状态" clearable style="width: 100%">
								<el-option v-for="item in handleStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="到账时间" prop="startTime">
							<el-date-picker
								v-model="state.queryParams.startTime"
								type="datetime"
								placeholder="到账时间起"
								format="YYYY-MM-DD HH:mm:ss"
								value-format="YYYY-MM-DD HH:mm:ss"
								:shortcuts="shortcuts"
								class="w100"
							/>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="" prop="endTime">
							<el-date-picker
								v-model="state.queryParams.endTime"
								type="datetime"
								placeholder="到账时间止"
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
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'payAbnormal/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button icon="ele-FolderOpened" type="primary" @click="exportAbnormal" v-auth="'payExport/exportAbnormal'" :loading="state.exporting"> 导出台账 </el-button>
					<el-tooltip content="异常到账是「钱到了但没能记到订单上」的记录，需要人工确认后关联订单或标记无需处理" placement="top">
						<el-icon style="margin-left: 6px; color: var(--el-text-color-secondary)"><ele-QuestionFilled /></el-icon>
					</el-tooltip>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_handleStatus="{ row }">
					<el-tag :type="handleTagType(row.handleStatus)">{{ row.handleStatusText }}</el-tag>
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip content="人工关联到订单" placement="top">
						<el-button
							icon="ele-Connection"
							size="small"
							text
							type="primary"
							:disabled="row.handleStatus !== 1"
							@click="handleLink(row)"
							v-auth="'payAbnormal/link'"
						/>
					</el-tooltip>
					<el-tooltip content="确认无需处理" placement="top">
						<el-button icon="ele-CircleClose" size="small" text type="warning" :disabled="row.handleStatus !== 1" @click="handleIgnore(row)" v-auth="'payAbnormal/ignore'" />
					</el-tooltip>
				</template>
			</vxe-grid>
		</el-card>

		<LinkOrder ref="linkRef" @handleQuery="handleQuery" />
	</div>
</template>

<!-- 异常到账台账（F5） -->
<script lang="ts" setup name="payAbnormal">
import { onMounted, reactive, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useDateTimeShortCust } from '/@/hooks/dateTimeShortCust';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { downloadByData, getFileName } from '/@/utils/download';

import LinkOrder from '/@/views/paycenter/abnormal/component/linkOrder.vue';

import { getAPI } from '/@/utils/axios-utils';
import { PayAbnormalApi, PayExportApi } from '/@/api-services/system/api';
import { PagePayAbnormalInput, PayAbnormalOutput, PayExportInput } from '/@/api-services/system/models';
import { resolveExportRange, toExportInput } from '/@/views/paycenter/utils/exportParams';

const shortcuts = useDateTimeShortCust();
const xGrid = ref<VxeGridInstance>();
const linkRef = ref<InstanceType<typeof LinkOrder>>();
const state = reactive({
	queryParams: {
		reportOrderNo: undefined,
		voucherNo: undefined,
		reason: undefined,
		handleStatus: undefined,
		startTime: undefined,
		endTime: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'createTime', order: 'desc', descStr: 'desc' },
	},
	exporting: false,
});

// 枚举下拉：值取自后端枚举（PayAbnormalReasonEnum / PayHandleStatusEnum），
// 中文与后端 [Description] 保持一致，避免两处各写一份漂移
const reasonOptions = [
	{ label: '无匹配订单', value: 1 },
	{ label: '订单已过期', value: 2 },
	{ label: '订单已完成', value: 3 },
];
const handleStatusOptions = [
	{ label: '待处理', value: 1 },
	{ label: '已关联', value: 2 },
	{ label: '确认无需处理', value: 3 },
];

// 本地存储参数
const localPageParamKey = 'localPageParam:payAbnormal';
// 表格参数配置
const options = useVxeTable<PayAbnormalOutput>(
	{
		id: 'payAbnormal',
		name: '异常到账台账',
		columns: [
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'reportOrderNo', title: '上报订单号', minWidth: 240, showOverflow: 'tooltip' },
			{ field: 'amount', title: '到账金额', minWidth: 110, align: 'right', formatter: ({ cellValue }) => money(cellValue) },
			{ field: 'notifyTime', title: '到账时间', minWidth: 160, showOverflow: 'tooltip', formatter: ({ cellValue }) => fmtTime(cellValue) },
			{ field: 'voucherNo', title: '凭证号', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'reasonText', title: '异常原因', minWidth: 110, showOverflow: 'tooltip' },
			{ field: 'handleStatus', title: '处理状态', minWidth: 110, slots: { default: 'row_handleStatus' } },
			{ field: 'relatedOrderNo', title: '关联订单号', minWidth: 240, showOverflow: 'tooltip' },
			{ field: 'handlerName', title: '处理人', minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'handleTime', title: '处理时间', minWidth: 160, showOverflow: 'tooltip', formatter: ({ cellValue }) => fmtTime(cellValue) },
			{ field: 'handleRemark', title: '处理说明', minWidth: 160, showOverflow: 'tooltip' },
			{ field: 'createTime', title: '接收时间', minWidth: 160, showOverflow: 'tooltip', formatter: ({ cellValue }) => fmtTime(cellValue) },
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
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PagePayAbnormalInput;
	return getAPI(PayAbnormalApi).apiPayAbnormalPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.reportOrderNo = undefined;
	state.queryParams.voucherNo = undefined;
	state.queryParams.reason = undefined;
	state.queryParams.handleStatus = undefined;
	state.queryParams.startTime = undefined;
	state.queryParams.endTime = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 表格事件
const gridEvents: VxeGridListeners<PayAbnormalOutput> = {
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

// 人工关联（把这条到账挂到某笔订单上）
const handleLink = (row: any) => {
	linkRef.value?.openDialog(row);
};

// 确认无需处理
const handleIgnore = (row: any) => {
	ElMessageBox.prompt(`确认「${row.reportOrderNo}」这笔到账无需处理？可填写原因。`, '确认无需处理', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		inputPlaceholder: '处理说明（可空）',
		inputValue: '',
	})
		.then(async ({ value }) => {
			await getAPI(PayAbnormalApi).apiPayAbnormalIgnorePost({ id: row.id, handleRemark: value || undefined });
			ElMessage.success('已标记为无需处理');
			await handleQuery();
		})
		.catch(() => {});
};

// 导出台账（F7.5）
const exportAbnormal = async () => {
	// 台账没有账号维度，导出必须给时间区间；没填按最近 7 天兜底（见 utils/exportParams.ts）
	const params = toExportInput<PayExportInput>(resolveExportRange(state.queryParams));

	state.exporting = true;
	try {
		const res = await getAPI(PayExportApi).apiPayExportExportAbnormalPost(params, { responseType: 'blob' });
		downloadByData(res.data as any, getFileName(res.headers));
		ElMessage.success('导出成功');
	} finally {
		state.exporting = false;
	}
};

// 金额展示
const money = (value: any) => (value === undefined || value === null ? '-' : Number(value).toFixed(2));
// 时间展示
const fmtTime = (value: any) => {
	if (!value) return '';
	const d = new Date(value);
	if (isNaN(d.getTime())) return String(value);
	const p = (n: number) => String(n).padStart(2, '0');
	return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
};
// 处理状态色（1待处理 2已关联 3确认无需处理）
const handleTagType = (status: any) => {
	switch (Number(status)) {
		case 1:
			return 'danger';
		case 2:
			return 'success';
		case 3:
			return 'info';
		default:
			return 'info';
	}
};
</script>
