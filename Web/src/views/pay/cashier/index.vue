<template>
	<div class="cashier-page">
		<template v-if="state.error && !state.order">
			<div class="empty-wrap">
				<p class="empty-title">无法打开收银台</p>
				<p class="empty-msg">{{ state.error }}</p>
				<button type="button" class="btn-primary" @click="loadOrder">重试</button>
			</div>
		</template>

		<template v-else-if="state.order">
			<!-- 付款进度固定顶部 -->
			<nav class="chevron-steps cashier-sticky-top" aria-label="付款进度">
				<div
					v-for="s in stepItems"
					:key="s.n"
					class="chevron-step"
					:class="{ active: (state.order.step ?? 1) === s.n, done: (state.order.step ?? 1) > s.n }"
				>
					<span class="chevron-text">{{ s.n }} {{ s.label }}</span>
				</div>
			</nav>

			<div class="cashier-scroll">
				<div class="order-bar">
					<span class="order-label">订单号:</span>
					<span class="order-no">{{ state.order.orderNo }}</span>
					<button type="button" class="icon-copy" title="复制订单号" @click="copy(state.order.orderNo)">
						<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
							<rect x="9" y="9" width="11" height="11" rx="2" fill="none" stroke="currentColor" stroke-width="1.8" />
							<path d="M6 15H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v1" fill="none" stroke="currentColor" stroke-width="1.8" />
						</svg>
					</button>
				</div>

				<section class="amount-block">
					<p v-if="showCountdown" class="remain" :class="{ urgent: remainDisplay <= 60 }">
						剩余时间 {{ formatRemain(remainDisplay) }}
					</p>
					<p v-else-if="isTerminal" class="remain muted">{{ state.order.statusText }}</p>
					<p class="pay-hint">请付款</p>
					<p class="amount-line">
						<span class="amount-num">{{ moneyInt(state.order.requestAmount) }}</span>
						<span class="amount-cur">CNY</span>
					</p>
				</section>

				<div v-if="state.order.notice" class="notice-box">
					<span class="notice-icon" aria-hidden="true">!</span>
					<div class="notice-body" v-html="formatNotice(state.order.notice)" />
				</div>

				<!-- 银行卡：单列字段，避免横向溢出 -->
				<section v-if="isBank" class="pay-section">
					<h2 class="section-title">
						<span class="section-ico" aria-hidden="true">
							<img :src="brandIconSrc" alt="" class="brand-ico" width="36" height="24" decoding="async" />
						</span>
						{{ typeTitle }}
					</h2>
					<div class="field-grid bank-col">
						<div v-for="item in bankFields" :key="item.label" class="field-cell">
							<label class="field-label">{{ item.label }}</label>
							<div class="field-box">
								<span class="field-value">{{ item.value || '-' }}</span>
								<button
									v-if="item.value"
									type="button"
									class="icon-copy"
									:title="'复制' + item.label"
									@click="copy(item.value)"
								>
									<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
										<rect x="9" y="9" width="11" height="11" rx="2" fill="none" stroke="currentColor" stroke-width="1.8" />
										<path d="M6 15H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v1" fill="none" stroke="currentColor" stroke-width="1.8" />
									</svg>
								</button>
							</div>
						</div>
					</div>
				</section>

				<!-- 支付宝 / 微信 / 云闪付 / 其它码类 -->
				<section v-else class="pay-section">
					<h2 class="section-title">
						<span class="section-ico" aria-hidden="true">
							<img
								v-if="brandIconSrc"
								:src="brandIconSrc"
								alt=""
								class="brand-ico"
								width="36"
								height="24"
								decoding="async"
							/>
							<span v-else class="section-ico-fallback">📱</span>
						</span>
						{{ typeTitle }}
					</h2>
					<div class="field-grid single-col">
						<div class="field-cell">
							<label class="field-label">卖家姓名</label>
							<div class="field-box">
								<span class="field-value">{{ state.order.payeeName || '-' }}</span>
								<button
									v-if="state.order.payeeName"
									type="button"
									class="icon-copy"
									title="复制卖家姓名"
									@click="copy(state.order.payeeName)"
								>
									<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
										<rect x="9" y="9" width="11" height="11" rx="2" fill="none" stroke="currentColor" stroke-width="1.8" />
										<path d="M6 15H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v1" fill="none" stroke="currentColor" stroke-width="1.8" />
									</svg>
								</button>
							</div>
						</div>
						<div v-if="state.order.accountNo" class="field-cell">
							<label class="field-label">账号</label>
							<div class="field-box">
								<span class="field-value">{{ state.order.accountNo }}</span>
								<button type="button" class="icon-copy" title="复制账号" @click="copy(state.order.accountNo)">
									<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
										<rect x="9" y="9" width="11" height="11" rx="2" fill="none" stroke="currentColor" stroke-width="1.8" />
										<path d="M6 15H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v1" fill="none" stroke="currentColor" stroke-width="1.8" />
									</svg>
								</button>
							</div>
						</div>
					</div>
					<div v-if="qrSrc" class="qr-wrap">
						<img :src="qrSrc" alt="收款码" class="qr-img" />
					</div>
				</section>

				<section v-if="state.order.voucherUrls?.length" class="pay-section">
					<h2 class="section-title">已上传凭证</h2>
					<div class="voucher-thumbs">
						<template v-for="(url, i) in state.order.voucherUrls" :key="url + i">
							<a v-if="isPdf(url)" :href="mediaUrl(url)" target="_blank" rel="noopener" class="voucher-pdf">PDF {{ i + 1 }}</a>
							<el-image
								v-else
								:src="mediaUrl(url)"
								:preview-src-list="imageVoucherUrls"
								fit="cover"
								class="voucher-thumb"
								preview-teleported
							/>
						</template>
					</div>
				</section>

				<section v-if="isFailed && canUpload" class="await-hint-block failed-hint">
					<p>收款方确认未到账，请核对后重新上传付款凭证</p>
				</section>

				<section v-if="isAwaiting" class="await-hint-block">
					<p>凭证已提交，请等待收款方确认；如需更正可重新上传</p>
				</section>

				<section v-if="isTerminal" class="terminal-block" :class="statusTone">
					<p class="terminal-title">{{ terminalTitle }}</p>
					<p class="terminal-msg">{{ terminalMsg }}</p>
				</section>

				<button
					v-if="state.order.canCancel"
					type="button"
					class="link-cancel"
					:disabled="state.cancelling"
					@click="onCancel"
				>
					{{ state.cancelling ? '取消中…' : '取消订单' }}
				</button>
			</div>

			<!-- 底栏固定：左返回，右主操作；等待确认时左返回右刷新，中间可重传 -->
			<footer v-if="state.order.canPay" class="action-bar">
				<button type="button" class="btn-return" @click="onReturn">{{ returnBtnText }}</button>
				<button type="button" class="btn-primary" @click="openVoucherDialog(false)">
					{{ isFailed ? '重新上传' : '我已付款' }}
				</button>
			</footer>

			<footer v-else-if="isAwaiting" class="action-bar">
				<button type="button" class="btn-return" @click="onReturn">{{ returnBtnText }}</button>
				<button v-if="canUpload" type="button" class="btn-primary" @click="openVoucherDialog(true)">重新上传</button>
				<button type="button" class="btn-secondary" :disabled="state.loading" @click="loadOrder">刷新</button>
			</footer>

			<footer v-else-if="isExpired && canUpload" class="action-bar">
				<button type="button" class="btn-return" @click="onReturn">{{ returnBtnText }}</button>
				<button type="button" class="btn-primary" @click="openVoucherDialog(true)">重新上传凭证</button>
			</footer>

			<footer v-else-if="isTerminal || isExpired" class="action-bar">
				<button type="button" class="btn-return btn-return-primary" @click="onReturn">{{ returnBtnText }}</button>
			</footer>
		</template>

		<!-- 上传凭证弹窗（对齐附件） -->
		<div v-if="state.voucherVisible" class="modal-mask" @click.self="onMaskClose">
			<div class="modal-panel" role="dialog" aria-modal="true" aria-labelledby="voucher-title" @click.stop>
				<button type="button" class="modal-close" aria-label="关闭" @click="closeVoucherDialog()">×</button>
				<h3 id="voucher-title" class="modal-title">上传付款凭证</h3>
				<p class="modal-sub">{{ uploadHint }}</p>
				<p v-if="state.reuploadSession" class="modal-sub replace-hint">
					重新选择后点「确认提交」才会替换已有凭证；关闭弹窗不影响原凭证
				</p>

				<div v-if="uploadedList.length" class="voucher-thumbs mb12">
					<template v-for="(url, i) in uploadedList" :key="url + i">
						<a v-if="isPdf(url)" :href="mediaUrl(url)" target="_blank" rel="noopener" class="voucher-pdf">PDF {{ i + 1 }}</a>
						<el-image
							v-else
							:src="mediaUrl(url)"
							:preview-src-list="uploadedImageUrls"
							fit="cover"
							class="voucher-thumb"
							preview-teleported
						/>
					</template>
				</div>

				<el-upload
					v-if="uploadedList.length < 3"
					drag
					class="voucher-uploader"
					:auto-upload="false"
					:show-file-list="false"
					accept=".jpg,.jpeg,.png,.pdf,image/jpeg,image/png,application/pdf"
					:on-change="onPickFile"
					:disabled="state.uploading"
				>
					<div class="upload-zone">
						<svg class="upload-ico" viewBox="0 0 48 48" width="48" height="48" aria-hidden="true">
							<path
								d="M24 8v22M24 8l-7 7M24 8l7 7M10 36h28"
								fill="none"
								stroke="#9aa3ab"
								stroke-width="2.4"
								stroke-linecap="round"
								stroke-linejoin="round"
							/>
						</svg>
						<p class="upload-tip">请确保转账凭证带有银行印章</p>
						<p class="upload-tip">支持 jpg、png、pdf；建议用手机「截图」</p>
						<p class="upload-tip">图片会自动压缩，原图过大请改截图</p>
						<p class="upload-tip">最多可上传 3 个文件</p>
						<span class="btn-select">{{ state.uploading ? '上传中…' : '选择文件' }}</span>
					</div>
				</el-upload>

				<button
					type="button"
					class="btn-primary btn-block"
					:disabled="uploadedList.length === 0 || state.submitting"
					@click="onSubmit"
				>
					{{ state.submitting ? '提交中…' : '确认提交' }}
				</button>
			</div>
		</div>
	</div>
</template>

<script lang="ts" setup name="payCashier">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import type { UploadFile } from 'element-plus';
import {
	buildPreviewOrder,
	cancelCashier,
	fetchCashier,
	isPreviewToken,
	mediaUrl,
	submitCashier,
	uploadVoucher,
	type CashierOrder,
} from '/@/views/pay/cashier/api';
import {
	prepareVoucherFile,
	VoucherPrepareError,
	voucherPrepareMessage,
} from '/@/views/pay/cashier/compressVoucher';
const route = useRoute();
const token = computed(() => String(route.params.token || '').trim());

/** 项目内静态标：public/cashier-icons（同域，无外链）；见 public/cashier-icons/SOURCE.md */
function cashierIcon(file: string): string {
	const base = String(import.meta.env.BASE_URL || '/');
	const prefix = base.endsWith('/') ? base : `${base}/`;
	return `${prefix}cashier-icons/${file}`;
}

const stepItems = [
	{ n: 1, label: '确认付款' },
	{ n: 2, label: '等待确认' },
	{ n: 3, label: '确认收款' },
];

const state = reactive({
	loading: false,
	error: '' as string,
	order: null as CashierOrder | null,
	cancelling: false,
	/** 等待确认后的重传会话：上传仅草稿，确认提交才替换 */
	reuploadSession: false,
	voucherVisible: false,
	uploading: false,
	submitting: false,
});

const remainDisplay = ref(0);
/** 弹层内草稿；重传时置 [] 且不改页面 order.voucherUrls，避免误关弹层后缩略图闪没 */
const draftVoucherUrls = ref<string[] | null>(null);
/** 打开弹层后短时忽略遮罩关闭，避免按钮点击穿透导致「开即关」 */
let maskCloseGuardUntil = 0;
let tickTimer: ReturnType<typeof setInterval> | null = null;
let pollTimer: ReturnType<typeof setInterval> | null = null;

const typeKey = computed(() => String(state.order?.type || '').toLowerCase());
const isBank = computed(() => typeKey.value === 'bank');
const typeTitle = computed(() => {
	switch (typeKey.value) {
		case 'bank':
			return '银行卡收款';
		case 'alipay':
			return '支付宝收款';
		case 'wxpay':
			return '微信收款';
		case 'cloudquickpass':
			return '云闪付收款';
		default:
			return '扫码收款';
	}
});
/** 银行卡 / 支付宝 / 微信 / 云闪付：同域卡面标；未知类型用通用图标 */
const brandIconSrc = computed(() => {
	switch (typeKey.value) {
		case 'bank':
			return cashierIcon('card-generic.svg');
		case 'alipay':
			return cashierIcon('alipay.svg');
		case 'wxpay':
			return cashierIcon('wechat-pay.svg');
		case 'cloudquickpass':
			return cashierIcon('unionpay.svg');
		default:
			return '';
	}
});
const uploadHint = computed(() => {
	if (typeKey.value === 'alipay') return '请上传支付宝转账截图';
	if (typeKey.value === 'wxpay') return '请上传微信转账截图';
	if (isBank.value) return '请上传银行转账回单截图';
	return '请上传付款凭证截图';
});

const qrSrc = computed(() => mediaUrl(state.order?.qrImageUrl));
const statusKey = computed(() => String(state.order?.status ?? ''));
const isAwaiting = computed(() => {
	const s = statusKey.value;
	return s === 'AwaitingConfirm' || s === '5' || Number(s) === 5;
});
const isCompleted = computed(() => {
	const s = statusKey.value;
	return s === 'Completed' || s === '3' || Number(s) === 3;
});
const isExpired = computed(() => {
	const s = statusKey.value;
	return s === 'Expired' || s === '4' || Number(s) === 4;
});
const isFailed = computed(() => {
	const s = statusKey.value;
	return s === 'Failed' || s === '6' || Number(s) === 6;
});
const isTerminal = computed(() => isCompleted.value);
const canUpload = computed(() => {
	if (!state.order) return false;
	if (state.order.canUpload != null) return !!state.order.canUpload;
	return !!state.order.canPay || isAwaiting.value || isExpired.value;
});
const showCountdown = computed(() => !!state.order && !isCompleted.value && !isExpired.value && remainDisplay.value > 0);
const returnBtnText = computed(() => {
	const label = String(state.order?.returnLabel || '').trim();
	return label || '返回';
});

const bankFields = computed(() => {
	const o = state.order;
	if (!o) return [];
	return [
		{ label: '卖家姓名', value: o.payeeName },
		{ label: '账号', value: o.accountNo },
		{ label: '开户银行', value: o.bankName },
		{ label: '支行信息', value: o.branchName },
	];
});

const statusTone = computed(() => {
	if (isCompleted.value) return 'tone-ok';
	if (isExpired.value) return 'tone-bad';
	return 'tone-wait';
});

const terminalTitle = computed(() => (isCompleted.value ? '收款已确认' : '订单已关闭'));
const terminalMsg = computed(() =>
	isCompleted.value ? '对方已确认收款，本页可关闭。' : '订单已过期或已取消，如已付款请联系收款方处理。'
);

const uploadedList = computed(() => draftVoucherUrls.value ?? state.order?.voucherUrls ?? []);
const imageVoucherUrls = computed(() => (state.order?.voucherUrls ?? []).filter((u) => !isPdf(u)).map(mediaUrl));
const uploadedImageUrls = computed(() => uploadedList.value.filter((u) => !isPdf(u)).map(mediaUrl));

/** 附件金额展示：整数不带小数尾零，有小数保留两位 */
const moneyInt = (v: string | number | undefined) => {
	const n = Number(v ?? 0);
	if (!Number.isFinite(n)) return '0';
	return Number.isInteger(n) ? String(n) : n.toFixed(2);
};

const formatRemain = (sec: number) => {
	const s = Math.max(0, Math.floor(sec));
	const m = Math.floor(s / 60);
	const r = s % 60;
	const p = (n: number) => String(n).padStart(2, '0');
	return `${p(m)}:${p(r)}`;
};

const isPdf = (url: string) => /\.pdf(\?|$)/i.test(url);

const formatNotice = (raw: string) => {
	const text = (raw || '').trim();
	if (!text) return '';

	const looksHtml = /<[a-z][\s\S]*>/i.test(text);
	if (looksHtml) {
		// 白名单消毒后直接渲染（后端已消毒；前端再挡一层）
		return sanitizeNoticeHtml(text);
	}

	const esc = text
		.replace(/&/g, '&amp;')
		.replace(/</g, '&lt;')
		.replace(/>/g, '&gt;');
	return esc
		.replace(/\n/g, '<br/>')
		.replace(/【([^】]+)】/g, '<strong class="hl">【$1】</strong>')
		.replace(/(工商银行|中信银行)/g, '<strong class="hl">$1</strong>');
};

const NOTICE_ALLOWED = new Set(['P', 'BR', 'B', 'STRONG', 'I', 'EM', 'U', 'UL', 'OL', 'LI', 'SPAN', 'DIV', 'A']);

const sanitizeNoticeHtml = (html: string) => {
	if (typeof DOMParser === 'undefined') return '';
	const doc = new DOMParser().parseFromString(`<div id="root">${html}</div>`, 'text/html');
	const root = doc.getElementById('root');
	if (!root) return '';

	const walk = (node: Node) => {
		const kids = Array.from(node.childNodes);
		for (const child of kids) {
			if (child.nodeType === Node.ELEMENT_NODE) {
				const el = child as HTMLElement;
				const tag = el.tagName;
				if (!NOTICE_ALLOWED.has(tag)) {
					while (el.firstChild) node.insertBefore(el.firstChild, el);
					node.removeChild(el);
					continue;
				}
				for (const attr of Array.from(el.attributes)) {
					const name = attr.name.toLowerCase();
					if (name.startsWith('on') || name === 'style') {
						el.removeAttribute(attr.name);
						continue;
					}
					if (tag === 'A' && name === 'href') {
						const href = (attr.value || '').trim();
						const ok =
							href.startsWith('https://') ||
							href.startsWith('http://') ||
							href.startsWith('/') ||
							href.startsWith('#');
						if (!ok) el.removeAttribute('href');
						else {
							el.setAttribute('rel', 'noopener noreferrer');
							el.setAttribute('target', '_blank');
						}
						continue;
					}
					if (name !== 'class' && !(tag === 'A' && (name === 'href' || name === 'rel' || name === 'target'))) {
						el.removeAttribute(attr.name);
					}
				}
				walk(el);
			}
		}
	};
	walk(root);
	return root.innerHTML;
};

const copy = async (text: string) => {
	try {
		await navigator.clipboard.writeText(text);
		ElMessage.success('已复制');
	} catch {
		ElMessage.error('复制失败');
	}
};

const clearTimers = () => {
	if (tickTimer) {
		clearInterval(tickTimer);
		tickTimer = null;
	}
	if (pollTimer) {
		clearInterval(pollTimer);
		pollTimer = null;
	}
};

const startCountdown = (seconds: number) => {
	if (tickTimer) clearInterval(tickTimer);
	remainDisplay.value = Math.max(0, seconds | 0);
	tickTimer = setInterval(() => {
		if (remainDisplay.value <= 0) {
			if (tickTimer) clearInterval(tickTimer);
			tickTimer = null;
			loadOrder();
			return;
		}
		remainDisplay.value -= 1;
	}, 1000);
};

const ensurePoll = () => {
	if (pollTimer) return;
	if (!isAwaiting.value) return;
	pollTimer = setInterval(() => {
		if (!isAwaiting.value) {
			if (pollTimer) clearInterval(pollTimer);
			pollTimer = null;
			return;
		}
		loadOrder(true);
	}, 15000);
};

const loadOrder = async (silent = false) => {
	if (!token.value) {
		state.error = '链接无效';
		return;
	}
	if (!silent) state.loading = true;
	try {
		const order = isPreviewToken(token.value) ? buildPreviewOrder(token.value) : await fetchCashier(token.value);
		state.order = {
			...order,
			voucherUrls: order.voucherUrls ?? [],
		};
		state.error = '';
		startCountdown(order.remainSeconds ?? 0);
		if (isAwaiting.value) ensurePoll();
		else if (pollTimer) {
			clearInterval(pollTimer);
			pollTimer = null;
		}
	} catch (e: any) {
		state.error = e?.message || '加载失败';
		if (!silent) state.order = null;
	} finally {
		if (!silent) state.loading = false;
	}
};

const onCancel = async () => {
	try {
		await ElMessageBox.confirm('确认取消该订单？取消后不可恢复。', '取消订单', {
			type: 'warning',
			confirmButtonText: '确认取消',
			cancelButtonText: '返回',
		});
	} catch {
		return;
	}
	state.cancelling = true;
	try {
		if (isPreviewToken(token.value)) {
			if (state.order) {
				state.order = {
					...state.order,
					step: 1,
					status: 'Expired',
					statusText: '已过期',
					canPay: false,
					canCancel: false,
					remainSeconds: 0,
				};
			}
			ElMessage.success('【预览】订单已取消');
			return;
		}
		await cancelCashier(token.value);
		ElMessage.success('订单已取消');
		await loadOrder();
	} catch (e: any) {
		ElMessage.error(e?.message || '取消失败');
	} finally {
		state.cancelling = false;
	}
};

/** reupload=true：弹层内草稿上传，确认提交才替换订单凭证 */
const openVoucherDialog = (reupload = false) => {
	state.reuploadSession = !!reupload;
	draftVoucherUrls.value = reupload ? [] : null;
	maskCloseGuardUntil = Date.now() + 400;
	state.voucherVisible = true;
};

const closeVoucherDialog = () => {
	state.voucherVisible = false;
	draftVoucherUrls.value = null;
	state.reuploadSession = false;
};

/** 仅遮罩点击受防护；右上角关闭 / 提交后关闭不受限 */
const onMaskClose = () => {
	if (Date.now() < maskCloseGuardUntil) return;
	closeVoucherDialog();
};

/** 离开收银台：有 returnUrl 则跳出到商户站；否则通知父页并尝试关窗 */
const onReturn = () => {
	const url = String(state.order?.returnUrl || '').trim();
	const orderNo = state.order?.orderNo || '';
	if (url) {
		try {
			if (window.top && window.top !== window) {
				window.top.location.href = url;
			} else {
				window.location.href = url;
			}
		} catch {
			window.location.href = url;
		}
		return;
	}
	try {
		window.parent?.postMessage(
			{ type: 'pulsepay.cashier.close', orderNo, status: state.order?.status },
			'*'
		);
	} catch {
		/* ignore */
	}
	try {
		window.close();
	} catch {
		/* ignore */
	}
	// iframe / 被拦截关闭时给个提示
	window.setTimeout(() => {
		ElMessage.info('可以关闭本页');
	}, 200);
};

const showUploadGuide = (message: string) => {
	ElMessageBox.alert(message, '换个方式上传', {
		confirmButtonText: '我知道了',
		type: 'warning',
		customClass: 'cashier-upload-guide',
	}).catch(() => undefined);
};

const onPickFile = async (file: UploadFile) => {
	const raw = file.raw;
	if (!raw) return;
	if (uploadedList.value.length >= 3) {
		showUploadGuide(
			state.reuploadSession
				? '最多可上传 3 张付款凭证。可先关闭弹窗后重新打开再选。'
				: '最多可上传 3 张付款凭证。如需更换，请先点「重新上传」。'
		);
		return;
	}
	state.uploading = true;
	try {
		let payload: File;
		try {
			payload = await prepareVoucherFile(raw);
		} catch (e) {
			if (e instanceof VoucherPrepareError) {
				showUploadGuide(voucherPrepareMessage(e.code));
				return;
			}
			showUploadGuide('文件处理失败。请改用手机截图付款凭证后再试。');
			return;
		}
		const reupload = state.reuploadSession;
		const url = isPreviewToken(token.value)
			? URL.createObjectURL(payload)
			: await uploadVoucher(token.value, payload, reupload ? { draft: true } : undefined);
		if (!state.order) return;
		if (reupload) {
			// 仅写弹层草稿，订单上旧凭证保持到确认提交
			draftVoucherUrls.value = [...(draftVoucherUrls.value || []), url];
		} else {
			state.order.voucherUrls = [...(state.order.voucherUrls || []), url];
		}
		ElMessage.success(isPreviewToken(token.value) ? '【预览】上传成功' : '上传成功');
	} catch (e: any) {
		const msg = String(e?.message || '');
		if (/过大|截图|原图|超时|繁忙/.test(msg)) {
			showUploadGuide(msg || '上传失败，请改用手机截图后重试。');
		} else {
			ElMessage.error(msg || '上传失败，请稍后重试');
		}
	} finally {
		state.uploading = false;
	}
};

const onSubmit = async () => {
	if (uploadedList.value.length === 0) {
		ElMessage.warning(state.reuploadSession ? '请先选择新的付款凭证' : '请先上传付款凭证');
		return;
	}
	state.submitting = true;
	try {
		const draft = state.reuploadSession ? [...(draftVoucherUrls.value || [])] : undefined;
		if (isPreviewToken(token.value)) {
			if (state.order) {
				state.order = {
					...state.order,
					step: 3,
					status: 'Completed',
					statusText: '已完成',
					canPay: false,
					canCancel: false,
					canUpload: false,
					voucherUrls: draft?.length ? draft : state.order.voucherUrls,
				};
			}
			ElMessage.success('【预览】已确认到账');
			closeVoucherDialog();
			return;
		}
		const result = await submitCashier(token.value, draft);
		closeVoucherDialog();
		if (result?.paid) {
			ElMessage.success(result.message || '已确认到账');
		} else {
			ElMessage.warning(result?.message || '凭证已提交，请等待处理');
		}
		await loadOrder();
	} catch (e: any) {
		ElMessage.error(e?.message || '提交失败');
	} finally {
		state.submitting = false;
	}
};

watch(token, () => loadOrder());
onMounted(() => {
	document.documentElement.classList.add('pay-cashier-root');
	document.body.classList.add('pay-cashier-root');
	loadOrder();
});
onBeforeUnmount(() => {
	document.documentElement.classList.remove('pay-cashier-root');
	document.body.classList.remove('pay-cashier-root');
	clearTimers();
});
</script>

<style scoped lang="scss">
.cashier-page {
	--c-green: #7ac943;
	--c-green-deep: #5eae2a;
	--c-gray-step: #b8b8b8;
	--c-ink: #222;
	--c-muted: #888;
	--c-red: #e53935;
	--c-line: #e0e0e0;
	--c-bg: #fff;
	--c-bar: #f5f5f5;
	--c-notice-bg: #fff0f0;
	--c-notice-border: #f5c6c6;
	--action-h: 64px;

	min-height: 100dvh;
	height: 100dvh;
	max-width: 720px;
	margin: 0 auto;
	background: var(--c-bg);
	color: var(--c-ink);
	font-family: 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', 'Noto Sans SC', sans-serif;
	box-sizing: border-box;
	display: flex;
	flex-direction: column;
	overflow-x: hidden;
	overflow-y: hidden;
	overscroll-behavior-x: none;
	touch-action: pan-y;
}

.cashier-sticky-top {
	flex-shrink: 0;
	z-index: 20;
	background: var(--c-bg);
	box-shadow: 0 1px 0 rgba(0, 0, 0, 0.06);
}

.cashier-scroll {
	flex: 1;
	min-height: 0;
	overflow-x: hidden;
	overflow-y: auto;
	-webkit-overflow-scrolling: touch;
	overscroll-behavior-x: none;
	padding-bottom: 12px;
	width: 100%;
}

/* ── 人字形进度条 ── */
.chevron-steps {
	display: flex;
	width: 100%;
	height: 40px;
	overflow: hidden;
}
.chevron-step {
	flex: 1;
	position: relative;
	display: flex;
	align-items: center;
	justify-content: center;
	background: var(--c-gray-step);
	color: #fff;
	font-size: 14px;
	font-weight: 500;
	clip-path: polygon(0 0, calc(100% - 14px) 0, 100% 50%, calc(100% - 14px) 100%, 0 100%, 14px 50%);
	margin-left: -10px;
}
.chevron-step:first-child {
	margin-left: 0;
	clip-path: polygon(0 0, calc(100% - 14px) 0, 100% 50%, calc(100% - 14px) 100%, 0 100%);
}
.chevron-step:last-child {
	clip-path: polygon(0 0, 100% 0, 100% 100%, 0 100%, 14px 50%);
}
.chevron-step.active,
.chevron-step.done {
	background: var(--c-green);
	z-index: 1;
}
.chevron-text {
	position: relative;
	z-index: 1;
	padding: 0 8px;
	white-space: nowrap;
}

.order-bar {
	display: flex;
	align-items: center;
	gap: 6px;
	padding: 10px 16px;
	background: var(--c-bar);
	font-size: 14px;
	color: #555;
}
.order-label {
	flex-shrink: 0;
}
.order-no {
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.icon-copy {
	flex-shrink: 0;
	border: none;
	background: transparent;
	color: #9aa3ab;
	cursor: pointer;
	padding: 2px;
	display: inline-flex;
	align-items: center;
	line-height: 1;
}
.icon-copy:hover {
	color: var(--c-green-deep);
}

.amount-block {
	padding: 20px 16px 8px;
	text-align: center;
}
.remain {
	margin: 0 0 10px;
	font-size: 18px;
	font-weight: 700;
	color: var(--c-red);
	font-variant-numeric: tabular-nums;
}
.remain.urgent {
	animation: blink 1s step-end infinite;
}
.remain.muted {
	color: var(--c-muted);
	font-weight: 500;
	font-size: 15px;
}
@keyframes blink {
	50% {
		opacity: 0.4;
	}
}
.pay-hint {
	margin: 0 0 6px;
	font-size: 15px;
	color: var(--c-ink);
}
.amount-line {
	margin: 0;
	line-height: 1.1;
}
.amount-num {
	font-size: 48px;
	font-weight: 700;
	letter-spacing: 0.02em;
}
.amount-cur {
	margin-left: 8px;
	font-size: 18px;
	color: #aaa;
	font-weight: 500;
	vertical-align: 6px;
}

.notice-box {
	display: flex;
	gap: 10px;
	align-items: flex-start;
	margin: 12px 16px;
	padding: 12px 14px;
	background: var(--c-notice-bg);
	border: 1px solid var(--c-notice-border);
	border-radius: 8px;
	font-size: 13px;
	line-height: 1.55;
	color: #444;
	text-align: left;
}
.notice-icon {
	flex-shrink: 0;
	width: 22px;
	height: 22px;
	border-radius: 50%;
	background: #f5c518;
	color: #fff;
	font-weight: 800;
	font-size: 14px;
	display: grid;
	place-items: center;
	margin-top: 1px;
}
.notice-body :deep(.hl) {
	color: var(--c-red);
	font-weight: 700;
}
.notice-body :deep(p) {
	margin: 0 0 6px;
}
.notice-body :deep(p:last-child) {
	margin-bottom: 0;
}
.notice-body :deep(a) {
	color: #1565c0;
	text-decoration: underline;
}

.pay-section {
	padding: 8px 16px 4px;
}
.section-title {
	margin: 0 0 12px;
	font-size: 15px;
	font-weight: 600;
	display: flex;
	align-items: center;
	gap: 8px;
	line-height: 24px;
}
.section-ico {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	flex-shrink: 0;
	height: 24px;
	line-height: 1;
}
/* 项目内卡面标 120×80，统一高度，银行卡与三方支付对齐 */
.brand-ico {
	height: 24px;
	width: auto;
	max-width: 40px;
	display: block;
	object-fit: contain;
	border-radius: 3px;
	box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.08);
	background: #fff;
}
.section-ico-fallback {
	font-size: 16px;
	line-height: 24px;
}

.field-grid {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 12px 14px;
}
.field-grid.single-col,
.field-grid.bank-col {
	grid-template-columns: 1fr;
	max-width: 100%;
}
@media (min-width: 480px) {
	.field-grid.bank-col {
		grid-template-columns: 1fr 1fr;
	}
}
.field-label {
	display: block;
	font-size: 12px;
	color: var(--c-muted);
	margin-bottom: 6px;
}
.field-box {
	display: flex;
	align-items: center;
	gap: 6px;
	min-height: 40px;
	padding: 8px 10px;
	border: 1px solid var(--c-line);
	border-radius: 6px;
	background: #fff;
}
.field-value {
	flex: 1;
	font-size: 14px;
	word-break: break-all;
	line-height: 1.35;
}

.qr-wrap {
	display: flex;
	justify-content: center;
	padding: 16px 0 8px;
}
.qr-img {
	width: min(220px, 70vw);
	height: auto;
	border-radius: 8px;
	border: 1px solid var(--c-line);
}

.voucher-thumbs {
	display: flex;
	flex-wrap: wrap;
	gap: 10px;
}
.voucher-thumb {
	width: 72px;
	height: 72px;
	border-radius: 6px;
	border: 1px solid var(--c-line);
}
.voucher-pdf {
	display: grid;
	place-items: center;
	width: 72px;
	height: 72px;
	border-radius: 6px;
	background: #f7f7f7;
	color: #666;
	font-size: 12px;
	font-weight: 600;
	text-decoration: none;
	border: 1px solid var(--c-line);
}

.action-bar {
	flex-shrink: 0;
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 10px 12px calc(10px + env(safe-area-inset-bottom, 0px));
	min-height: var(--action-h);
	box-sizing: border-box;
	background: #fff;
	border-top: 1px solid var(--c-line);
	width: 100%;
	max-width: 100%;
	z-index: 20;
}
.btn-primary,
.btn-return,
.btn-secondary,
.btn-select {
	border: none;
	cursor: pointer;
	font-size: 15px;
	border-radius: 8px;
	padding: 12px 14px;
	line-height: 1.2;
	min-width: 0;
}
.btn-primary {
	/* 主色 #7ac943 偏亮：默认深绿字保可读；hover 加深底再转白字 */
	background: var(--c-green);
	color: #1a1a1a;
	font-weight: 600;
	flex: 1.35;
}
.btn-primary:hover {
	background: var(--c-green-deep);
	color: #fff;
}
.btn-primary:disabled {
	opacity: 0.55;
	cursor: not-allowed;
}
.btn-secondary {
	flex: 1;
	background: #f0f0f0;
	color: #333;
	font-weight: 500;
}
.btn-return {
	flex: 1;
	background: #fff;
	color: var(--c-ink, #1a1a1a);
	border: 1.5px solid var(--c-line, #dcdfe6);
	font-weight: 500;
}
.btn-return:hover {
	border-color: var(--c-green-deep, #0f59a4);
	color: var(--c-green-deep, #0f59a4);
}
.btn-return-primary {
	flex: 1;
	background: var(--c-green);
	border-color: transparent;
	color: #1a1a1a;
	font-weight: 600;
}
.btn-return-primary:hover {
	background: var(--c-green-deep);
	color: #fff;
}
.link-cancel {
	display: block;
	width: 100%;
	margin: 4px 0 8px;
	padding: 8px;
	border: none;
	background: transparent;
	color: var(--c-muted);
	font-size: 13px;
	cursor: pointer;
	text-align: center;
}
.link-cancel:disabled {
	opacity: 0.6;
	cursor: not-allowed;
}
.btn-block {
	width: 100%;
	margin-top: 16px;
	flex: none;
}

.await-hint-block {
	text-align: center;
	padding: 12px 16px 0;
	color: var(--c-muted, #909399);
	font-size: 14px;
}
.failed-hint {
	color: var(--c-red);
	font-weight: 500;
}
.await-row .link-btn {
	align-self: center;
}
.link-btn {
	border: none;
	background: transparent;
	color: var(--c-green-deep);
	cursor: pointer;
	font-size: 14px;
	margin-top: 4px;
}

.terminal-block {
	margin: 16px;
	padding: 20px;
	text-align: center;
	border-radius: 8px;
	background: #fafafa;
}
.terminal-block.tone-ok {
	background: #f0faf0;
}
.terminal-block.tone-bad {
	background: #fff5f5;
}
.terminal-title {
	margin: 0 0 6px;
	font-size: 17px;
	font-weight: 700;
}
.terminal-msg {
	margin: 0;
	color: var(--c-muted);
	font-size: 13px;
}

.empty-wrap {
	padding: 80px 24px;
	text-align: center;
}
.empty-title {
	margin: 0 0 8px;
	font-size: 18px;
	font-weight: 600;
}
.empty-msg {
	margin: 0 0 16px;
	color: var(--c-muted);
}

/* ── 上传弹窗 ── */
.modal-mask {
	position: fixed;
	inset: 0;
	z-index: 1000;
	background: rgba(0, 0, 0, 0.45);
	display: flex;
	align-items: center;
	justify-content: center;
	padding: 16px;
}
.modal-panel {
	position: relative;
	width: min(420px, 100%);
	background: #fff;
	border-radius: 10px;
	padding: 22px 20px 20px;
	box-shadow: 0 12px 40px rgba(0, 0, 0, 0.18);
}
.modal-close {
	position: absolute;
	top: 10px;
	right: 12px;
	border: none;
	background: transparent;
	font-size: 22px;
	color: #999;
	cursor: pointer;
	line-height: 1;
}
.modal-title {
	margin: 0 0 6px;
	font-size: 18px;
	font-weight: 700;
	text-align: center;
}
.modal-sub {
	margin: 0 0 16px;
	font-size: 13px;
	color: var(--c-muted);
	text-align: center;
}

.voucher-uploader {
	width: 100%;
}
.voucher-uploader :deep(.el-upload),
.voucher-uploader :deep(.el-upload-dragger) {
	width: 100%;
	padding: 0;
	border: none;
	background: transparent;
}
.upload-zone {
	border: 1.5px dashed #c8c8c8;
	border-radius: 8px;
	padding: 20px 16px 18px;
	text-align: center;
	background: #fafafa;
}
.upload-ico {
	margin-bottom: 8px;
}
.upload-tip {
	margin: 0 0 4px;
	font-size: 12px;
	color: #888;
	line-height: 1.5;
}
.btn-select {
	display: inline-block;
	margin-top: 12px;
	padding: 8px 22px;
	border: 1px solid #ccc;
	border-radius: 4px;
	background: #fff;
	color: #333;
	font-size: 13px;
	font-weight: 500;
}
.mb12 {
	margin-bottom: 12px;
}

@media (max-width: 480px) {
	.field-grid {
		grid-template-columns: 1fr;
	}
	.amount-num {
		font-size: 36px;
	}
	.amount-block {
		padding: 12px 16px 4px;
	}
	.chevron-text {
		font-size: 12px;
	}
	.action-bar {
		gap: 8px;
		padding: 8px 10px calc(8px + env(safe-area-inset-bottom, 0px));
	}
	.btn-primary,
	.btn-return,
	.btn-secondary {
		padding: 11px 10px;
		font-size: 14px;
	}
}
</style>

<style lang="scss">
/* 收银台路由：禁止整页横向滚动，仅纵向 */
html.pay-cashier-root,
body.pay-cashier-root {
	overflow-x: hidden;
	overflow-y: hidden;
	height: 100%;
	overscroll-behavior-x: none;
	touch-action: pan-y;
}
#app:has(.cashier-page) {
	height: 100%;
	overflow: hidden;
}
</style>
