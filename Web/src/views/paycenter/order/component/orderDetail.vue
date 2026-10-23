<template>
	<el-drawer v-model="state.isShowDialog" :size="state.drawerSize" :close-on-click-modal="false" destroy-on-close>
		<template #header>
			<div style="display: flex; align-items: center; justify-content: space-between; gap: 12px; width: 100%; padding-right: 12px">
				<div>
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Tickets /> </el-icon>
					<span> 订单详情 </span>
				</div>
				<div class="header-actions">
					<el-button
						v-if="canConfirm"
						type="success"
						size="small"
						icon="ele-CircleCheck"
						v-auth="'payOrder/confirmReceived'"
						@click="emitConfirm"
					>
						确认到账
					</el-button>
					<el-button
						v-if="canMarkUnpaid"
						type="danger"
						size="small"
						icon="ele-WarningFilled"
						:loading="state.markingUnpaid"
						v-auth="'payOrder/markUnpaid'"
						@click="markUnpaid"
					>
						确认未到账
					</el-button>
					<el-button
						v-if="canResend"
						type="warning"
						size="small"
						icon="ele-Promotion"
						:loading="state.resending"
						v-auth="'payOrder/resendWebhook'"
						@click="resendWebhook"
					>
						{{ resendButtonText }}
					</el-button>
				</div>
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
					<el-descriptions-item label="收款账号">
						<div class="account-cell">
							<el-image v-if="state.detail.order?.qrImageUrl" :src="state.detail.order.qrImageUrl" :preview-src-list="[state.detail.order.qrImageUrl]" fit="contain" class="qr-preview" preview-teleported />
							<span>{{ state.detail.order?.accountInfo || (state.detail.order?.qrImageUrl ? '收款码' : '-') }}</span>
						</div>
					</el-descriptions-item>
					<el-descriptions-item label="收款类型">{{ state.detail.order?.accountType || '-' }}</el-descriptions-item>
					<el-descriptions-item label="请求金额">{{ money(state.detail.order?.requestAmount) }}</el-descriptions-item>
					<el-descriptions-item label="累计到账">{{ money(state.detail.order?.receivedAmount) }}</el-descriptions-item>
					<el-descriptions-item label="未达成金额">
						<span :class="{ 'text-danger': (state.detail.order?.outstandingAmount ?? 0) < 0 }">{{ money(state.detail.order?.outstandingAmount) }}</span>
						<span v-if="(state.detail.order?.outstandingAmount ?? 0) < 0" class="tip">（负数＝超额到账）</span>
					</el-descriptions-item>
					<el-descriptions-item label="超额到账备注">{{ state.detail.order?.overpayRemark || '-' }}</el-descriptions-item>
					<el-descriptions-item label="确认水单" :span="2">
						<a v-if="state.detail.order?.voucherUrl" :href="state.detail.order.voucherUrl" target="_blank" rel="noopener">查看水单</a>
						<span v-else>-</span>
					</el-descriptions-item>
					<el-descriptions-item label="收银台凭证" :span="2">
						<div v-if="cashierVouchers.length" class="voucher-thumbs">
							<a v-for="(u, i) in cashierVouchers" :key="i" :href="u" target="_blank" rel="noopener" class="thumb-link">
								<img v-if="isImage(u)" :src="u" alt="凭证" class="thumb" />
								<span v-else class="thumb-file">凭证 {{ i + 1 }}</span>
							</a>
						</div>
						<span v-else>-</span>
					</el-descriptions-item>
					<el-descriptions-item label="创建时间">{{ fmtTime(state.detail.order?.createTime) }}</el-descriptions-item>
					<el-descriptions-item label="过期时间">{{ fmtTime(state.detail.order?.expireTime) }}</el-descriptions-item>
					<el-descriptions-item label="完成时间">{{ fmtTime(state.detail.order?.completeTime) || '-' }}</el-descriptions-item>
					<el-descriptions-item label="收银台链接" :span="2">
						<div v-if="cashierUrl" class="cashier-link-row">
							<a class="mono link" :href="cashierUrl" target="_blank" rel="noopener">{{ cashierUrl }}</a>
							<el-button size="small" text type="primary" @click="copyText(cashierUrl)">复制</el-button>
							<el-button size="small" type="primary" plain @click="openCashier">打开</el-button>
						</div>
						<span v-else class="tip">未生成或未配置 PayCenter:CashierPublicBaseUrl</span>
					</el-descriptions-item>
					<el-descriptions-item label="返回地址">{{ (state.detail.order as any)?.returnUrl || '-' }}</el-descriptions-item>
					<el-descriptions-item label="返回文案">{{ (state.detail.order as any)?.returnLabel || '-' }}</el-descriptions-item>
				</el-descriptions>

				<el-divider content-position="left">开放接口调用（{{ openApiCalls.length }}）</el-divider>
				<el-table :data="openApiCalls" size="small" border max-height="240" empty-text="暂无与本单关联的开放调用记录">
					<el-table-column prop="createTime" label="时间" width="160" :formatter="(r: any) => fmtTime(r.createTime)" />
					<el-table-column prop="actionText" label="动作" width="130" />
					<el-table-column prop="clientKey" label="AccessKey" min-width="140" show-overflow-tooltip />
					<el-table-column prop="operatorIp" label="IP" width="120" show-overflow-tooltip />
					<el-table-column prop="remark" label="说明" min-width="220" show-overflow-tooltip />
				</el-table>

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

				<el-divider content-position="left">商户通知（{{ state.detail.webhookRecords.length }}）</el-divider>
				<el-table :data="state.detail.webhookRecords" size="small" border max-height="360" row-key="id">
					<el-table-column type="expand">
						<template #default="{ row }">
							<div class="webhook-expand">
								<div class="expand-row">
									<span class="expand-label">事件 Id</span>
									<code class="mono">{{ row.eventId || '-' }}</code>
								</div>
								<div class="expand-row">
									<span class="expand-label">错误详情</span>
									<span class="expand-error">{{ row.lastError || '—' }}</span>
								</div>
								<div class="expand-row">
									<span class="expand-label">推送报文</span>
									<pre class="payload">{{ prettyJson(row.payload) }}</pre>
								</div>
							</div>
						</template>
					</el-table-column>
					<el-table-column prop="createTime" label="入队时间" width="160" :formatter="(r: any) => fmtTime(r.createTime)" />
					<el-table-column prop="eventType" label="事件" width="110" show-overflow-tooltip />
					<el-table-column prop="webhookUrl" label="目标地址" min-width="180" show-overflow-tooltip />
					<el-table-column label="状态" width="90">
						<template #default="{ row }">
							<el-tag :type="webhookStatusTag(row.status)" size="small">{{ row.statusText || '-' }}</el-tag>
						</template>
					</el-table-column>
					<el-table-column prop="attemptCount" label="尝试" width="64" align="center" />
					<el-table-column label="下次重试" width="160">
						<template #default="{ row }">{{ fmtTime(row.nextRetryTime) || '—' }}</template>
					</el-table-column>
					<el-table-column label="结果摘要" min-width="160" show-overflow-tooltip>
						<template #default="{ row }">
							<span v-if="normalizeWebhookStatus(row.status) === 1">success</span>
							<span v-else-if="row.lastError" class="text-danger">{{ row.lastError }}</span>
							<span v-else>—</span>
						</template>
					</el-table-column>
				</el-table>
				<el-empty
					v-if="!state.detail.webhookRecords.length"
					description="暂无商户通知（确认到账或「通知商户」后会出现）"
					:image-size="56"
					style="padding: 12px 0"
				/>

				<template v-if="state.detail.notifyRecords.length">
					<el-divider content-position="left">
						<span class="legacy-title">历史入站通知（旧接口，已下线 · {{ state.detail.notifyRecords.length }}）</span>
					</el-divider>
					<el-collapse>
						<el-collapse-item title="展开查看旧入站到账明细" name="legacy-notify">
							<el-table :data="state.detail.notifyRecords" size="small" border max-height="240">
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
						</el-collapse-item>
					</el-collapse>
				</template>

				<el-alert
					v-if="!state.detail.events.length && !state.detail.webhookRecords.length && !state.detail.notifyRecords.length"
					style="margin-top: 10px"
					type="info"
					:closable="false"
					title="该订单暂无事件流水与商户通知"
				/>
			</template>
		</el-skeleton>
	</el-drawer>
</template>

<script lang="ts" setup name="payOrderDetail">
import { computed, reactive } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';

import { getAPI } from '/@/utils/axios-utils';
import { PayOrderApi } from '/@/api-services/system/api';
import {
	PayNotifyRecordOutput,
	PayOrderDetailOutput,
	PayOrderEventOutput,
	PayOrderOutput,
	PayWebhookOutboxOutput,
} from '/@/api-services/system/models';

type NormalizedOrderDetail = Omit<PayOrderDetailOutput, 'events' | 'webhookRecords' | 'notifyRecords'> & {
	events: PayOrderEventOutput[];
	webhookRecords: PayWebhookOutboxOutput[];
	notifyRecords: PayNotifyRecordOutput[];
	openApiCalls?: any[];
};

const emits = defineEmits<{
	(e: 'confirm', row: PayOrderOutput): void;
	(e: 'done'): void;
}>();

const state = reactive({
	isShowDialog: false,
	loading: false,
	resending: false,
	markingUnpaid: false,
	drawerSize: window.innerWidth < 1200 ? '100%' : '960px',
	detail: {
		order: undefined,
		events: [],
		webhookRecords: [],
		notifyRecords: [],
		openApiCalls: [],
	} as NormalizedOrderDetail,
});

const cashierVouchers = computed(() => {
	const order = state.detail.order as any;
	const list = order?.voucherUrls;
	if (Array.isArray(list) && list.length) return list.filter((u: string) => !!u);
	return [] as string[];
});

const cashierUrl = computed(() => String((state.detail.order as any)?.cashierUrl || '').trim());
const openApiCalls = computed(() => state.detail.openApiCalls ?? []);

const canConfirm = computed(() => {
	const s = normalizeOrderStatus(state.detail.order?.status);
	return s === 1 || s === 2 || s === 4 || s === 5 || s === 6;
});

const canMarkUnpaid = computed(() => {
	const s = normalizeOrderStatus(state.detail.order?.status);
	return s === 1 || s === 2 || s === 5;
});

/** 仅已完成 / 未到账可人工重推；已过期额度已释放，不提供通知入口 */
const canResend = computed(() => {
	const s = normalizeOrderStatus(state.detail.order?.status);
	return s === 3 || s === 6;
});

const webhookAlreadyNotified = computed(() => !!(state.detail.order as any)?.webhookNotified);

const resendButtonText = computed(() => (webhookAlreadyNotified.value ? '再次通知' : '重试通知'));

const normalizeOrderStatus = (status: any): number => {
	if (typeof status === 'number' && !Number.isNaN(status)) return status;
	const n = Number(status);
	if (!Number.isNaN(n) && String(status).trim() !== '') return n;
	const key = String(status ?? '');
	const map: Record<string, number> = {
		Pending: 1,
		Partial: 2,
		Completed: 3,
		Expired: 4,
		AwaitingConfirm: 5,
		Failed: 6,
	};
	return map[key] ?? -1;
};

const normalizeWebhookStatus = (status: any): number => {
	if (typeof status === 'number' && !Number.isNaN(status)) return status;
	const n = Number(status);
	if (!Number.isNaN(n) && String(status).trim() !== '') return n;
	const key = String(status ?? '');
	const map: Record<string, number> = { Pending: 0, Success: 1, Dead: 2 };
	return map[key] ?? -1;
};

const openDialog = async (id: number) => {
	state.isShowDialog = true;
	state.loading = true;
	try {
		const res = await getAPI(PayOrderApi).apiPayOrderDetailPost(id);
		const r = res.data.result;
		state.detail = {
			...(r ?? {}),
			order: r?.order,
			events: r?.events ?? [],
			webhookRecords: r?.webhookRecords ?? [],
			notifyRecords: r?.notifyRecords ?? [],
			openApiCalls: (r as any)?.openApiCalls ?? [],
		} as NormalizedOrderDetail;
	} finally {
		state.loading = false;
	}
};

const emitConfirm = () => {
	if (!state.detail.order) return;
	emits('confirm', state.detail.order as PayOrderOutput);
};

const copyText = async (text: string) => {
	try {
		await navigator.clipboard.writeText(text);
		ElMessage.success('已复制');
	} catch {
		ElMessage.warning('复制失败，请手动选择');
	}
};

const openCashier = () => {
	if (!cashierUrl.value) return;
	window.open(cashierUrl.value, '_blank', 'noopener');
};

const markUnpaid = async () => {
	const order = state.detail.order;
	if (!order?.id) return;
	try {
		const { value } = await ElMessageBox.prompt(
			`确认订单 ${order.orderNo} 未到账？将通知商户 Failed，额度继续预占，收银台可重新上传凭证。`,
			'确认未到账',
			{
				type: 'warning',
				confirmButtonText: '确认未到账',
				cancelButtonText: '取消',
				inputPlaceholder: '可选：未到账原因',
				inputType: 'textarea',
				inputValidator: () => true,
			}
		);
		state.markingUnpaid = true;
		const res = await getAPI(PayOrderApi).apiPayOrderMarkUnpaidPost({
			id: order.id,
			remark: (value || '').trim() || undefined,
		});
		const msg = res?.data?.result?.webhookMessage || '已标记未到账';
		ElMessage.success(String(msg).replace(/^\[.*?\]\s*/, '') || '已标记未到账');
		emits('done');
		await openDialog(order.id);
	} catch {
		/* 取消 */
	} finally {
		state.markingUnpaid = false;
	}
};

const resendWebhook = async () => {
	const order = state.detail.order;
	if (!order?.id) return;
	const again = webhookAlreadyNotified.value;
	await ElMessageBox.confirm(
		again
			? `商户此前已收到过通知。确认再次向商户推送订单 ${order.orderNo} 当前状态（${order.statusText}）的异步回调？`
			: `向商户重推订单 ${order.orderNo} 当前状态（${order.statusText}）？将重新入队并立即尝试一次。`,
		again ? '再次通知' : '重试通知',
		{ type: 'warning', confirmButtonText: again ? '再次推送' : '立即重试', cancelButtonText: '取消' }
	);
	state.resending = true;
	try {
		await getAPI(PayOrderApi).apiPayOrderResendWebhookPost({ id: order.id });
		ElMessage.success('已通知商户（对方返回 success）');
		emits('done');
		await openDialog(order.id);
	} catch (e: any) {
		// 业务 Bah：已入队但即时失败时仍刷新详情
		const msg = e?.message || e?.response?.data?.message || '';
		if (msg.includes('已入队')) {
			ElMessage.warning(msg.replace(/^\[.*?\]\s*/, '') || msg);
			emits('done');
			await openDialog(order.id!);
		}
		// 其它错误由全局拦截提示
	} finally {
		state.resending = false;
	}
};

const money = (value: any) => (value === undefined || value === null ? '-' : Number(value).toFixed(2));
const fmtTime = (value: any) => {
	if (!value) return '';
	const d = new Date(value);
	if (isNaN(d.getTime())) return String(value);
	const p = (n: number) => String(n).padStart(2, '0');
	return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
};
const isImage = (u: string) => /\.(jpe?g|png|bmp|webp)(\?|$)/i.test(u);
const prettyJson = (raw: string | null | undefined) => {
	if (!raw) return '—';
	try {
		return JSON.stringify(JSON.parse(raw), null, 2);
	} catch {
		return raw;
	}
};
const webhookStatusTag = (status: any) => {
	switch (normalizeWebhookStatus(status)) {
		case 1:
			return 'success';
		case 2:
			return 'danger';
		case 0:
			return 'warning';
		default:
			return 'info';
	}
};
const statusTagType = (status: any) => {
	switch (normalizeOrderStatus(status)) {
		case 1:
			return 'info';
		case 2:
			return 'warning';
		case 3:
			return 'success';
		case 4:
			return 'danger';
		case 5:
			return 'warning';
		case 6:
			return 'danger';
		default:
			return 'info';
	}
};

defineExpose({ openDialog });
</script>

<style lang="scss" scoped>
.mono {
	font-family: Menlo, Monaco, Consolas, monospace;
	letter-spacing: 0.5px;
}
.cashier-link-row {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 6px;
	max-width: 100%;
}
.cashier-link-row .link {
	flex: 1 1 220px;
	min-width: 0;
	word-break: break-all;
	color: var(--el-color-primary);
}
.header-actions {
	display: flex;
	gap: 8px;
	flex-shrink: 0;
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
.account-cell {
	display: flex;
	align-items: center;
	gap: 8px;
}
.qr-preview {
	width: 48px;
	height: 48px;
}
.voucher-thumbs {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
}
.thumb {
	width: 64px;
	height: 64px;
	object-fit: cover;
	border-radius: 4px;
	border: 1px solid var(--el-border-color);
}
.thumb-file {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 64px;
	height: 64px;
	font-size: 12px;
	border: 1px dashed var(--el-border-color);
	border-radius: 4px;
	color: var(--el-color-primary);
}
.thumb-link {
	text-decoration: none;
}
.legacy-title {
	color: var(--el-text-color-secondary);
	font-weight: 400;
}
.webhook-expand {
	padding: 8px 12px 12px 40px;
	display: flex;
	flex-direction: column;
	gap: 10px;
}
.expand-row {
	display: flex;
	gap: 12px;
	align-items: flex-start;
}
.expand-label {
	flex: 0 0 72px;
	color: var(--el-text-color-secondary);
	font-size: 12px;
	line-height: 1.6;
}
.expand-error {
	color: var(--el-color-danger);
	font-size: 12px;
	word-break: break-all;
}
.payload {
	margin: 0;
	padding: 8px 10px;
	max-height: 220px;
	overflow: auto;
	flex: 1;
	font-size: 12px;
	line-height: 1.45;
	font-family: Menlo, Monaco, Consolas, monospace;
	background: var(--el-fill-color-light);
	border-radius: 4px;
	white-space: pre-wrap;
	word-break: break-all;
}
</style>
