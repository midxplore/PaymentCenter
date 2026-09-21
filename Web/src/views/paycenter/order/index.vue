<template>
	<div class="pay-order-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="订单号" prop="orderNo">
							<el-input v-model="state.queryParams.orderNo" placeholder="系统订单号（模糊）" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="外部单号" prop="externalNo">
							<el-input v-model="state.queryParams.externalNo" placeholder="外部业务单号（模糊）" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="订单状态" prop="status">
							<el-select v-model="state.queryParams.status" placeholder="订单状态" clearable style="width: 100%">
								<el-option v-for="item in state.statusOptions" :key="item.value" :label="item.label" :value="item.value" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="收款账号" prop="accountId">
							<el-select v-model="state.queryParams.accountId" placeholder="收款账号（账号维度审计）" filterable clearable style="width: 100%">
								<el-option v-for="item in state.accountData" :key="item.id" :label="`${item.type}【${item.accountInfo || (item.qrImageUrl ? '收款码' : '-')}】`" :value="item.id" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="创建时间" prop="startTime">
							<el-date-picker
								v-model="state.queryParams.startTime"
								type="datetime"
								placeholder="创建时间起"
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
								placeholder="创建时间止"
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
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'payOrder/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button icon="ele-FolderOpened" type="primary" @click="exportOrder" v-auth="'payExport/exportOrder'" :loading="state.exporting"> 导出订单 </el-button>
					<el-tooltip content="按当前查询条件的创建时间区间导出，区间与条数有上限" placement="top">
						<el-icon style="margin-left: 6px; color: var(--el-text-color-secondary)"><ele-QuestionFilled /></el-icon>
					</el-tooltip>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_account="{ row }">
					<div class="account-cell">
						<el-image v-if="row.qrImageUrl" :src="row.qrImageUrl" :preview-src-list="[row.qrImageUrl]" fit="cover" class="qr-thumb" preview-teleported />
						<span>{{ row.accountInfo || (row.qrImageUrl ? '收款码' : '-') }}</span>
					</div>
				</template>
				<template #row_orderNo="{ row }">
					<el-link type="primary" @click="handleView(row)">{{ row.orderNo }}</el-link>
				</template>
				<template #row_status="{ row }">
					<el-tag :type="statusTagType(row.status)">{{ row.statusText }}</el-tag>
				</template>
				<template #row_outstandingAmount="{ row }">
					<span :class="{ 'text-danger': row.outstandingAmount < 0 }">{{ money(row.outstandingAmount) }}</span>
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip content="详情" placement="top">
						<el-button icon="ele-InfoFilled" size="small" text type="primary" @click="handleView(row)" v-auth="'payOrder/detail'" />
					</el-tooltip>
				</template>
			</vxe-grid>
		</el-card>

		<OrderDetail ref="detailRef" />
	</div>
</template>

<!-- 收款订单（F7.1 / F7.2） -->
<script lang="ts" setup name="payOrder">
import { onMounted, reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';
import { useDateTimeShortCust } from '/@/hooks/dateTimeShortCust';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { auth } from '/@/utils/authFunction';
import { downloadByData, getFileName } from '/@/utils/download';

import OrderDetail from '/@/views/paycenter/order/component/orderDetail.vue';

import { getAPI } from '/@/utils/axios-utils';
import { PayOrderApi, PayExportApi, PayAccountApi } from '/@/api-services/system/api';
import { PagePayOrderInput, PayOrderOutput, PayOrderExportInput, PayEnumOption, PayAccountOutput } from '/@/api-services/system/models';
import { resolveExportRange, toExportInput } from '/@/views/paycenter/utils/exportParams';

const shortcuts = useDateTimeShortCust();
const xGrid = ref<VxeGridInstance>();
const detailRef = ref<InstanceType<typeof OrderDetail>>();
const state = reactive({
	queryParams: {
		orderNo: undefined,
		externalNo: undefined,
		status: undefined,
		accountId: undefined,
		startTime: undefined,
		endTime: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'createTime', order: 'desc', descStr: 'desc' },
	},
	statusOptions: [] as Array<PayEnumOption>,
	accountData: [] as Array<PayAccountOutput>,
	exporting: false,
});

// 本地存储参数
const localPageParamKey = 'localPageParam:payOrder';
// 表格参数配置
const options = useVxeTable<PayOrderOutput>(
	{
		id: 'payOrder',
		name: '收款订单',
		columns: [
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'orderNo', title: '系统订单号', minWidth: 260, showOverflow: 'tooltip', slots: { default: 'row_orderNo' } },
			{ field: 'externalNo', title: '外部单号', minWidth: 160, showOverflow: 'tooltip' },
			{ field: 'accountInfo', title: '收款账号', minWidth: 200, slots: { default: 'row_account' } },
			{ field: 'accountType', title: '收款类型', minWidth: 110, showOverflow: 'tooltip' },
			{ field: 'requestAmount', title: '请求金额', minWidth: 110, align: 'right', formatter: ({ cellValue }) => money(cellValue) },
			{ field: 'receivedAmount', title: '已到账', minWidth: 110, align: 'right', formatter: ({ cellValue }) => money(cellValue) },
			{ field: 'outstandingAmount', title: '未达成', minWidth: 110, align: 'right', slots: { default: 'row_outstandingAmount' } },
			{ field: 'status', title: '状态', minWidth: 90, slots: { default: 'row_status' } },
			{ field: 'expireTime', title: '过期时间', minWidth: 160, showOverflow: 'tooltip', formatter: ({ cellValue }) => fmtTime(cellValue) },
			{ field: 'completeTime', title: '完成时间', minWidth: 160, showOverflow: 'tooltip', formatter: ({ cellValue }) => fmtTime(cellValue) },
			{ field: 'createTime', title: '创建时间', minWidth: 160, showOverflow: 'tooltip', formatter: ({ cellValue }) => fmtTime(cellValue) },
			{ field: 'overpayRemark', title: '超额备注', minWidth: 140, showOverflow: 'tooltip' },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 80, showOverflow: true, slots: { default: 'row_buttons' } },
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

	// 状态下拉：直接取后端枚举，避免前端再抄一份中文
	const statusRes = await getAPI(PayOrderApi).apiPayOrderStatusOptionsGet();
	state.statusOptions = statusRes.data.result ?? [];

	// 账号下拉：用于「账号维度审计」过滤
	const accountRes = await getAPI(PayAccountApi).apiPayAccountPagePost({ page: 1, pageSize: 1000 });
	state.accountData = accountRes.data.result?.items ?? [];
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PagePayOrderInput;
	return getAPI(PayOrderApi).apiPayOrderPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.orderNo = undefined;
	state.queryParams.externalNo = undefined;
	state.queryParams.status = undefined;
	state.queryParams.accountId = undefined;
	state.queryParams.startTime = undefined;
	state.queryParams.endTime = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 表格事件
const gridEvents: VxeGridListeners<PayOrderOutput> = {
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
		if (auth('payOrder/detail')) await handleView(row);
	},
};

// 查看详情（F7.1 全生命周期）
const handleView = async (row: any) => {
	detailRef.value?.openDialog(row.id);
};

// 导出订单（F7.5）
const exportOrder = async () => {
	// 导出必须带时间区间（服务端按区间限流），没填就按「最近 7 天」兜底，避免用户点了没反应
	// （日期字段为什么是本地格式字符串而不是 Date → 见 utils/exportParams.ts 的文件头说明）
	const params = toExportInput<PayOrderExportInput>({
		...resolveExportRange(state.queryParams),
		accountId: state.queryParams.accountId,
		status: state.queryParams.status,
	});

	state.exporting = true;
	try {
		const res = await getAPI(PayExportApi).apiPayExportExportOrderPost(params, { responseType: 'blob' });
		downloadByData(res.data as any, getFileName(res.headers));
		ElMessage.success('导出成功');
	} finally {
		state.exporting = false;
	}
};

// 金额展示：保留两位小数，避免 vxe 把 12.30 显示成 12.3
const money = (value: any) => (value === undefined || value === null ? '-' : Number(value).toFixed(2));
// 时间展示
const fmtTime = (value: any) => {
	if (!value) return '';
	const d = new Date(value);
	if (isNaN(d.getTime())) return String(value);
	const p = (n: number) => String(n).padStart(2, '0');
	return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
};
// 状态色（值来自 PayOrderStatusEnum：1待到账 2部分到账 3已完成 4已过期）
const statusTagType = (status: any) => {
	switch (Number(status)) {
		case 1:
			return 'info';
		case 2:
			return 'warning';
		case 3:
			return 'success';
		case 4:
			return 'danger';
		default:
			return 'info';
	}
};
</script>

<style lang="scss" scoped>
.text-danger {
	color: var(--el-color-danger);
	font-weight: 600;
}
.account-cell {
	display: flex;
	align-items: center;
	gap: 8px;
}
.qr-thumb {
	width: 36px;
	height: 36px;
	flex: none;
	border-radius: 2px;
}
</style>
