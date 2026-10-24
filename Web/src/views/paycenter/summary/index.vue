<template>
	<div class="pay-summary-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="业务日起" prop="startDate">
							<el-date-picker
								v-model="state.queryParams.startDate"
								type="date"
								placeholder="业务日起"
								format="YYYY-MM-DD"
								value-format="YYYY-MM-DD"
								:shortcuts="dateShortcuts"
								class="w100"
							/>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="业务日止" prop="endDate">
							<el-date-picker
								v-model="state.queryParams.endDate"
								type="date"
								placeholder="业务日止"
								format="YYYY-MM-DD"
								value-format="YYYY-MM-DD"
								:shortcuts="dateShortcuts"
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
						<el-button type="primary" icon="ele-Search" @click="loadData" v-auth="'paySummary/daily'" :loading="state.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="state.loading"> 重置 </el-button>
						<el-button icon="ele-FolderOpened" type="success" @click="exportDaily" v-auth="'paySummary/exportDaily'" :loading="state.exporting">
							导出
						</el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card shadow="hover" style="margin-top: 5px" v-loading="state.loading">
			<template #header>
				<div style="display: flex; align-items: center; gap: 8px">
					<span>此刻池况</span>
					<el-tooltip content="当前额度池实时合计。" placement="top">
						<el-icon style="color: var(--el-text-color-secondary)"><ele-QuestionFilled /></el-icon>
					</el-tooltip>
				</div>
			</template>
			<el-row :gutter="12">
				<el-col :xs="12" :sm="8" :md="4" class="mb5" v-for="item in kpiItems" :key="item.label">
					<div class="kpi">
						<div class="kpi-label">{{ item.label }}</div>
						<div class="kpi-value">{{ item.value }}</div>
					</div>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<div style="margin-bottom: 8px; color: var(--el-text-color-secondary); font-size: 12px">
				按日流水：投放/预占/实收/释放与码维护。
			</div>
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" @page-change="onPageChange">
				<template #empty>
					<el-empty :image-size="160" description="暂无数据" />
				</template>
				<template #row_money="{ row, column }">
					{{ money(row[column.field]) }}
				</template>
			</vxe-grid>
		</el-card>
	</div>
</template>

<script lang="ts" setup name="paySummary">
import { computed, onMounted, reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridProps, VxeGridListeners } from 'vxe-table';
import { getAPI } from '/@/utils/axios-utils';
import { PaySummaryApi } from '/@/api-services/system/api';
import type { PayOpsDailyRowOutput, PayOpsPoolNowOutput } from '/@/api-services/system/models';
import { downloadByData, getFileName } from '/@/utils/download';

const xGrid = ref<VxeGridInstance>();
const money = (value: any) => (value === undefined || value === null ? '-' : Number(value).toFixed(2));
const PAGE_SIZE = 10;

const fmtDate = (d: Date) => {
	const p = (n: number) => String(n).padStart(2, '0');
	return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
};

const defaultRange = () => {
	const end = new Date();
	const start = new Date(Date.now() - 6 * 24 * 3600 * 1000);
	return { startDate: fmtDate(start), endDate: fmtDate(end) };
};

const state = reactive({
	loading: false,
	exporting: false,
	queryParams: defaultRange(),
	current: null as PayOpsPoolNowOutput | null,
	days: [] as PayOpsDailyRowOutput[],
	page: { currentPage: 1, pageSize: PAGE_SIZE },
});

const dateShortcuts = [
	{ text: '近7天', value: () => [new Date(Date.now() - 6 * 24 * 3600 * 1000), new Date()] },
	{ text: '近30天', value: () => [new Date(Date.now() - 29 * 24 * 3600 * 1000), new Date()] },
];

const kpiItems = computed(() => {
	const c = state.current;
	return [
		{ label: '总额度', value: money(c?.totalQuotaSum) },
		{ label: '已用', value: money(c?.usedQuotaSum) },
		{ label: '在途预占', value: money(c?.lockedQuotaSum) },
		{ label: '可用', value: money(c?.remainingQuotaSum) },
		{ label: '未到账占用', value: money(c?.unpaidLockedSum) },
		{ label: '启用中码', value: String(c?.accountEnabledCount ?? '-') },
	];
});

const slicePage = () => {
	const { currentPage, pageSize } = state.page;
	const start = (currentPage - 1) * pageSize;
	options.data = state.days.slice(start, start + pageSize);
	if (options.pagerConfig) {
		options.pagerConfig.currentPage = currentPage;
		options.pagerConfig.pageSize = pageSize;
		options.pagerConfig.total = state.days.length;
	}
};

const options = reactive<VxeGridProps<PayOpsDailyRowOutput>>({
	border: true,
	showOverflow: true,
	height: 'auto',
	maxHeight: 560,
	rowConfig: { isHover: true },
	pagerConfig: {
		enabled: true,
		currentPage: 1,
		pageSize: PAGE_SIZE,
		pageSizes: [10, 20, 50],
		total: 0,
		layouts: ['PrevPage', 'Number', 'NextPage', 'Sizes', 'Total'],
	},
	columns: [
		{ field: 'bizDate', title: '业务日', minWidth: 110, formatter: ({ cellValue }) => String(cellValue || '').slice(0, 10) },
		{ field: 'quotaInject', title: '额度投放', minWidth: 110, align: 'right', slots: { default: 'row_money' } },
		{ field: 'quotaReclaim', title: '额度回收', minWidth: 110, align: 'right', slots: { default: 'row_money' } },
		{ field: 'quotaNet', title: '投放净额', minWidth: 110, align: 'right', slots: { default: 'row_money' } },
		{ field: 'lockOccurred', title: '预占发生', minWidth: 110, align: 'right', slots: { default: 'row_money' } },
		{ field: 'receivedCompleted', title: '实收到账', minWidth: 110, align: 'right', slots: { default: 'row_money' } },
		{ field: 'completedCount', title: '完成笔数', minWidth: 90, align: 'right' },
		{ field: 'expireReleased', title: '过期释放', minWidth: 110, align: 'right', slots: { default: 'row_money' } },
		{ field: 'markUnpaidCount', title: '未到账笔数', minWidth: 100, align: 'right' },
		{ field: 'accountAddCount', title: '新建码', minWidth: 80, align: 'right' },
		{ field: 'quotaAdjustCount', title: '调整次数', minWidth: 90, align: 'right' },
		{ field: 'accountUpdateCount', title: '编辑次数', minWidth: 90, align: 'right' },
	],
	data: [],
});

const onPageChange: VxeGridListeners['pageChange'] = ({ currentPage, pageSize }) => {
	state.page.currentPage = currentPage;
	state.page.pageSize = pageSize;
	slicePage();
};

const loadData = async () => {
	if (!state.queryParams.startDate || !state.queryParams.endDate) {
		ElMessage.warning('请选择业务日区间');
		return;
	}
	state.loading = true;
	try {
		const res = await getAPI(PaySummaryApi).apiPaySummaryDailyPost({
			startDate: state.queryParams.startDate,
			endDate: state.queryParams.endDate,
		});
		const result = res.data.result;
		state.current = result?.current ?? null;
		state.days = result?.days ?? [];
		state.page.currentPage = 1;
		slicePage();
	} finally {
		state.loading = false;
	}
};

const resetQuery = async () => {
	Object.assign(state.queryParams, defaultRange());
	await loadData();
};

const exportDaily = async () => {
	if (!state.queryParams.startDate || !state.queryParams.endDate) {
		ElMessage.warning('请选择业务日区间');
		return;
	}
	state.exporting = true;
	try {
		const res = await getAPI(PaySummaryApi).apiPaySummaryExportDailyPost(
			{
				startDate: state.queryParams.startDate,
				endDate: state.queryParams.endDate,
			},
			{ responseType: 'blob' }
		);
		downloadByData(res.data as any, getFileName(res.headers) || `运营汇总按日.xlsx`);
	} finally {
		state.exporting = false;
	}
};

onMounted(() => {
	loadData();
});
</script>

<style scoped>
.kpi {
	padding: 10px 12px;
	background: var(--el-fill-color-light);
	border-radius: 4px;
}
.kpi-label {
	font-size: 12px;
	color: var(--el-text-color-secondary);
	margin-bottom: 4px;
}
.kpi-value {
	font-size: 18px;
	font-weight: 600;
	font-variant-numeric: tabular-nums;
}
</style>
