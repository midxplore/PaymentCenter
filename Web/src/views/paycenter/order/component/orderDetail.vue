<template>
	<el-drawer v-model="state.isShowDialog" :size="state.drawerSize" :close-on-click-modal="false" destroy-on-close>
		<template #header>
			<div>
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Tickets /> </el-icon>
				<span> 订单详情 </span>
			</div>
		</template>

		<el-skeleton :loading="state.loading" animated :rows="6">
			<template #default>
				<el-descriptions :column="2" border size="small">
					<el-descriptions-item label="系统订单号" :span="2">
						<span class="mono">{{ state.detail.order?.orderNo }}</span>
					</el-descriptions-item>
					<el-descriptions-item label="外部单号">{{ state.detail.order?.externalNo || '-' }}</el-descriptions-item>
					<el-descriptions-item label="订单状态">
						<el-tag :type="statusTagType(state.detail.order?.status)">{{ state.detail.order?.statusText }}</el-tag>
					</el-descriptions-item>
					<el-descriptions-item label="收款账号">{{ state.detail.order?.accountInfo || '-' }}</el-descriptions-item>
					<el-descriptions-item label="收款类型">{{ state.detail.order?.accountType || '-' }}</el-descriptions-item>
					<el-descriptions-item label="请求金额">{{ money(state.detail.order?.requestAmount) }}</el-descriptions-item>
					<el-descriptions-item label="累计到账">{{ money(state.detail.order?.receivedAmount) }}</el-descriptions-item>
					<el-descriptions-item label="未达成金额">
						<span :class="{ 'text-danger': (state.detail.order?.outstandingAmount ?? 0) < 0 }">{{ money(state.detail.order?.outstandingAmount) }}</span>
						<span v-if="(state.detail.order?.outstandingAmount ?? 0) < 0" class="tip">（负数＝超额到账）</span>
					</el-descriptions-item>
					<el-descriptions-item label="超额到账备注">{{ state.detail.order?.overpayRemark || '-' }}</el-descriptions-item>
					<el-descriptions-item label="创建时间">{{ fmtTime(state.detail.order?.createTime) }}</el-descriptions-item>
					<el-descriptions-item label="过期时间">{{ fmtTime(state.detail.order?.expireTime) }}</el-descriptions-item>
					<el-descriptions-item label="完成时间">{{ fmtTime(state.detail.order?.completeTime) || '-' }}</el-descriptions-item>
				</el-descriptions>

				<el-divider content-position="left">事件流水（{{ state.detail.events.length }}）</el-divider>
				<el-table :data="state.detail.events" size="small" border max-height="320">
					<el-table-column prop="createTime" label="发生时间" width="160" :formatter="(r: any) => fmtTime(r.createTime)" />
					<el-table-column prop="eventTypeText" label="事件" width="100" />
					<el-table-column label="状态变更" width="180">
						<template #default="{ row }">
							<span>{{ row.fromStatusText || '—' }}</span>
							<el-icon style="margin: 0 4px; vertical-align: middle"><ele-Right /></el-icon>
							<span>{{ row.toStatusText || '—' }}</span>
						</template>
					</el-table-column>
					<el-table-column label="本次金额" width="110" align="right">
						<template #default="{ row }">{{ money(row.amount) }}</template>
					</el-table-column>
					<el-table-column label="累计到账" width="110" align="right">
						<template #default="{ row }">{{ money(row.receivedTotal) }}</template>
					</el-table-column>
					<el-table-column prop="operatorName" label="操作人" width="100">
						<template #default="{ row }">{{ row.operatorName || '系统' }}</template>
					</el-table-column>
					<el-table-column prop="remark" label="说明" min-width="200" show-overflow-tooltip />
				</el-table>

				<el-divider content-position="left">到账明细（{{ state.detail.notifyRecords.length }}）</el-divider>
				<el-table :data="state.detail.notifyRecords" size="small" border max-height="320">
					<el-table-column prop="notifyTime" label="到账时间" width="160" :formatter="(r: any) => fmtTime(r.notifyTime)" />
					<el-table-column label="到账金额" width="110" align="right">
						<template #default="{ row }">{{ money(row.amount) }}</template>
					</el-table-column>
					<el-table-column prop="voucherNo" label="凭证号" min-width="150" show-overflow-tooltip />
					<el-table-column label="是否已累加" width="110">
						<template #default="{ row }">
							<el-tag :type="row.applied ? 'success' : 'danger'">{{ row.applied ? '已累加' : '未累加' }}</el-tag>
						</template>
					</el-table-column>
					<el-table-column prop="clientId" label="通知方" width="90" />
					<el-table-column prop="createTime" label="接收时间" width="160" :formatter="(r: any) => fmtTime(r.createTime)" />
					<el-table-column prop="rawBody" label="原始报文" min-width="180" show-overflow-tooltip />
				</el-table>

				<el-alert
					v-if="!state.detail.events.length && !state.detail.notifyRecords.length"
					style="margin-top: 10px"
					type="info"
					:closable="false"
					title="该订单暂无事件流水与到账明细"
				/>
			</template>
		</el-skeleton>
	</el-drawer>
</template>

<script lang="ts" setup name="payOrderDetail">
import { reactive } from 'vue';

import { getAPI } from '/@/utils/axios-utils';
import { PayOrderApi } from '/@/api-services/system/api';
import { PayNotifyRecordOutput, PayOrderDetailOutput, PayOrderEventOutput } from '/@/api-services/system/models';

/**
 * 归一化后的详情形状。
 *
 * ★ 为什么不直接用生成的 `PayOrderDetailOutput`：那个模型里 `events` /
 *   `notifyRecords` 是 `Array<...> | null | undefined`（后端字段可空）。
 *   而模板里写的是 `state.detail.events.length` —— 用生成类型的话类型检查会报
 *   TS18049，而且**它报得对**：后端少给一个字段就是运行期 TypeError、抽屉白屏。
 *
 *   本组件在 `openDialog` 里已经把这两个数组归一化成 `[]`（那是唯一的消费点），
 *   所以这里把「已归一化」这个不变式写进类型，模板就不必到处写 `?.` 与 `?? []`
 *   —— 把防御放在数据入口，而不是散在模板的每个取值处。
 */
type NormalizedOrderDetail = Omit<PayOrderDetailOutput, 'events' | 'notifyRecords'> & {
	events: PayOrderEventOutput[];
	notifyRecords: PayNotifyRecordOutput[];
};

const state = reactive({
	isShowDialog: false,
	loading: false,
	// 抽屉宽度跟着窗口走：窄屏用满宽，宽屏给 900px，避免表格被压扁
	drawerSize: window.innerWidth < 1200 ? '100%' : '900px',
	detail: {
		order: undefined,
		events: [],
		notifyRecords: [],
	} as NormalizedOrderDetail,
});

// 打开弹窗（按主键拉详情，一次给全订单 + 事件流水 + 到账明细）
const openDialog = async (id: number) => {
	state.isShowDialog = true;
	state.loading = true;
	try {
		const res = await getAPI(PayOrderApi).apiPayOrderDetailPost(id);
		// ★ 必须把**嵌套数组**也归一化，只对 result 本身做 `??` 兜底是不够的。
		//   后端这两个字段可空，所以「result 有值、但 events 缺失」是可能的；
		//   模板里写的是 `state.detail.events.length`，少一个就是 TypeError、抽屉白屏。
		//   （这是装上 `vue-tsc` 后报出来的 TS18049；在此之前构建期完全看不见。）
		const r = res.data.result;
		state.detail = {
			...(r ?? {}),
			order: r?.order,
			events: r?.events ?? [],
			notifyRecords: r?.notifyRecords ?? [],
		} as NormalizedOrderDetail;
	} finally {
		state.loading = false;
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
// 状态色
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

// 导出对象
defineExpose({ openDialog });
</script>

<style lang="scss" scoped>
.mono {
	font-family: Menlo, Monaco, Consolas, monospace;
	letter-spacing: 0.5px;
}

.text-danger {
	color: var(--el-color-danger);
	font-weight: 600;
}

.tip {
	margin-left: 4px;
	font-size: 12px;
	color: var(--el-color-danger);
}
</style>
