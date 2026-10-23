/** 收银台匿名 API（不附带 JWT） */

export interface CashierOrder {
	orderNo: string;
	type: string;
	step: number;
	status: string | number;
	statusText: string;
	requestAmount: string | number;
	expireTime: string;
	remainSeconds: number;
	canPay: boolean;
	canCancel: boolean;
	/** 可上传/重传凭证（待到账 / 未到账 / 等待确认） */
	canUpload?: boolean;
	payeeName: string;
	accountNo: string;
	bankName: string;
	branchName: string;
	qrImageUrl: string;
	voucherUrls: string[];
	notice: string;
	/** 第三方返回地址；空则点「返回」时关页/通知父窗 */
	returnUrl?: string;
	/** 返回按钮文案；空则前端用「返回」 */
	returnLabel?: string;
}

/** 本地样式预览：token 以 preview 开头时不打后端（如 /#/c/preview、/#/c/preview-wx、/#/c/preview-wait） */
export function isPreviewToken(token: string): boolean {
	return /^preview/i.test((token || '').trim());
}

export function buildPreviewOrder(token: string): CashierOrder {
	const t = (token || '').toLowerCase();
	const isWx = t.includes('wx') || t.includes('ali');
	const awaiting = t.includes('wait') || t.includes('await');
	const done = t.includes('done') || t.includes('ok');
	const expired = t.includes('expir') || t.includes('cancel');

	let step = 1;
	let status: string | number = 'Pending';
	let statusText = '待到账';
	let canPay = true;
	let voucherUrls: string[] = [];

	if (done) {
		step = 3;
		status = 'Completed';
		statusText = '已完成';
		canPay = false;
		voucherUrls = ['/upload/pay-voucher/preview/demo.jpg'];
	} else if (awaiting) {
		step = 2;
		status = 'AwaitingConfirm';
		statusText = '等待确认';
		canPay = false;
		voucherUrls = ['/upload/pay-voucher/preview/demo.jpg'];
	} else if (t.includes('fail') || t.includes('unpaid')) {
		step = 1;
		status = 'Failed';
		statusText = '未到账';
		canPay = true;
	} else if (expired) {
		step = 1;
		status = 'Expired';
		statusText = '已过期';
		canPay = false;
	}

	const expire = new Date(Date.now() + 45 * 60 * 1000);
	const pad = (n: number) => String(n).padStart(2, '0');
	const expireTime = `${expire.getFullYear()}-${pad(expire.getMonth() + 1)}-${pad(expire.getDate())} ${pad(expire.getHours())}:${pad(expire.getMinutes())}:${pad(expire.getSeconds())}`;

	if (isWx) {
		return {
			orderNo: '20260924154800123456789012345678',
			type: t.includes('ali') ? 'alipay' : 'wxpay',
			step,
			status,
			statusText,
			requestAmount: '128.50',
			expireTime,
			remainSeconds: canPay || awaiting ? 45 * 60 : 0,
			canPay,
			canCancel: canPay,
			canUpload: canPay || awaiting,
			payeeName: 'PulsePay演示店',
			accountNo: t.includes('ali') ? 'demo@alipay.com' : 'wxid_demo_cashier',
			bankName: '',
			branchName: '',
			// 占位二维码（SVG data URL），不依赖后端静态资源
			qrImageUrl:
				'data:image/svg+xml,' +
				encodeURIComponent(
					`<svg xmlns="http://www.w3.org/2000/svg" width="240" height="240"><rect fill="#f5f7fa" width="240" height="240"/><rect x="24" y="24" width="72" height="72" fill="#1a1a1a"/><rect x="144" y="24" width="72" height="72" fill="#1a1a1a"/><rect x="24" y="144" width="72" height="72" fill="#1a1a1a"/><rect x="48" y="48" width="24" height="24" fill="#f5f7fa"/><rect x="168" y="48" width="24" height="24" fill="#f5f7fa"/><rect x="48" y="168" width="24" height="24" fill="#f5f7fa"/><rect x="120" y="120" width="40" height="40" fill="#1a1a1a"/><text x="120" y="228" text-anchor="middle" font-size="12" fill="#909399">预览收款码</text></svg>`
				),
			voucherUrls,
			notice: '请勿使用错误方式付款，可能导致无法到账。\n若您已完成付款，请务必点击【我已付款完成】并上传凭证。',
			returnUrl: '',
			returnLabel: '返回',
		};
	}

	return {
		orderNo: '981679067393196032',
		type: 'bank',
		step,
		status,
		statusText,
		requestAmount: '3010',
		expireTime,
		remainSeconds: canPay || awaiting ? 29 * 60 + 2 : 0,
		canPay,
		canCancel: canPay,
		canUpload: canPay || awaiting,
		payeeName: 'PulsePay演示店',
		accountNo: '6222021234567890123',
		bankName: '中信银行',
		branchName: '杭州西湖支行',
		qrImageUrl: '',
		voucherUrls,
		// 预览模拟：等同后台账号「备注」字段
		notice: '请勿使用工商银行卡支付，该银行卡付款可能导致无法到账。\n若您已完成付款，请务必点击【我已付款完成】',
		returnUrl: '',
		returnLabel: '返回',
	};
}

interface ApiEnvelope<T> {
	code?: number;
	type?: string;
	message?: string;
	result?: T;
}

const baseUrl = () => String(window.__env__?.VITE_API_URL ?? '').replace(/\/$/, '');

export function mediaUrl(path?: string | null): string {
	if (!path) return '';
	const p = String(path).trim();
	if (!p) return '';
	if (/^https?:\/\//i.test(p) || p.startsWith('data:')) return p;
	const base = baseUrl();
	return p.startsWith('/') ? `${base}${p}` : `${base}/${p}`;
}

/** 收银台直面用户：把网关/HTTP 技术码转成可行动提示 */
function friendlyHttpError(status: number, serverMessage?: string): string {
	const msg = (serverMessage || '').trim();
	if (status === 413 || /too large|entity too large|request entity|超过\s*3\s*M|不能超过/i.test(msg)) {
		return '文件过大，上传未能完成。请改用手机「截图」付款凭证后再试（不要选相册原图）。';
	}
	if (status === 408 || status === 504) {
		return '上传超时，请检查网络后重试；若图片很大，请改用截图后再传。';
	}
	if (status >= 500) {
		return '服务暂时繁忙，请稍后再试。';
	}
	if (msg && !/请求失败\s*\(\d+\)/.test(msg)) return msg;
	return '上传失败，请稍后重试。若反复失败，请改用手机截图后再传。';
}

async function parseJson<T>(res: Response): Promise<T> {
	let body: ApiEnvelope<T> | null = null;
	try {
		body = (await res.json()) as ApiEnvelope<T>;
	} catch {
		if (!res.ok) throw new Error(friendlyHttpError(res.status));
		throw new Error('响应解析失败，请稍后重试');
	}
	if (!res.ok) {
		throw new Error(friendlyHttpError(res.status, body?.message));
	}
	if (body && body.code !== undefined && body.code !== 200) {
		throw new Error(friendlyHttpError(0, body.message || '请求失败'));
	}
	return body?.result as T;
}

export async function fetchCashier(token: string): Promise<CashierOrder> {
	const res = await fetch(`${baseUrl()}/api/pay/cashier/${encodeURIComponent(token)}`, {
		method: 'GET',
		headers: { Accept: 'application/json' },
	});
	return parseJson<CashierOrder>(res);
}

/** draft=true：只落盘，不改订单凭证；确认提交时再整体替换 */
export async function uploadVoucher(
	token: string,
	file: File,
	opts?: { replace?: boolean; draft?: boolean }
): Promise<string> {
	const form = new FormData();
	form.append('file', file);
	if (opts?.replace) form.append('replace', 'true');
	if (opts?.draft) form.append('draft', 'true');
	const res = await fetch(`${baseUrl()}/api/pay/cashier/${encodeURIComponent(token)}/voucher`, {
		method: 'POST',
		body: form,
	});
	return parseJson<string>(res);
}

export interface CashierSubmitResult {
	status?: string;
	statusText?: string;
	paid?: boolean;
	message?: string;
}

/** voucherUrls：重传确认时传入草稿 URL，服务端此时才替换旧凭证；成功则自动到账 */
export async function submitCashier(token: string, voucherUrls?: string[]): Promise<CashierSubmitResult> {
	const hasReplace = Array.isArray(voucherUrls) && voucherUrls.length > 0;
	const res = await fetch(`${baseUrl()}/api/pay/cashier/${encodeURIComponent(token)}/submit`, {
		method: 'POST',
		headers: {
			Accept: 'application/json',
			...(hasReplace ? { 'Content-Type': 'application/json' } : {}),
		},
		body: hasReplace ? JSON.stringify({ voucherUrls }) : undefined,
	});
	return parseJson<CashierSubmitResult>(res);
}

export async function cancelCashier(token: string): Promise<void> {
	const res = await fetch(`${baseUrl()}/api/pay/cashier/${encodeURIComponent(token)}/cancel`, {
		method: 'POST',
		headers: { Accept: 'application/json' },
	});
	await parseJson<unknown>(res);
}
