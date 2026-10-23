import Compressor from 'compressorjs';

/** 与后端 PayConst.CashierVoucherMaxBytes 对齐 */
export const VOUCHER_MAX_BYTES = 3 * 1024 * 1024;
/** 原图允许更大，上传前再压；超过则引导截图 */
export const VOUCHER_RAW_IMAGE_MAX_BYTES = 20 * 1024 * 1024;
/** 压完目标：兼顾清晰度与常见网关默认 1MB 限制 */
const COMPRESS_SOFT_TARGET = 900 * 1024;

export type VoucherPrepareErrorCode = 'BAD_TYPE' | 'PDF_TOO_LARGE' | 'IMAGE_TOO_LARGE' | 'STILL_TOO_LARGE';

export class VoucherPrepareError extends Error {
	readonly code: VoucherPrepareErrorCode;
	constructor(code: VoucherPrepareErrorCode) {
		super(code);
		this.code = code;
		this.name = 'VoucherPrepareError';
	}
}

export function isVoucherImage(file: File): boolean {
	if (file.type && /^image\/(jpe?g|png)$/i.test(file.type)) return true;
	return /\.(jpe?g|png)$/i.test(file.name || '');
}

export function isVoucherPdf(file: File): boolean {
	if (file.type === 'application/pdf') return true;
	return /\.pdf$/i.test(file.name || '');
}

function compressOnce(file: File, quality: number, maxSide: number): Promise<File> {
	return new Promise((resolve, reject) => {
		new Compressor(file, {
			quality,
			maxWidth: maxSide,
			maxHeight: maxSide,
			mimeType: 'image/jpeg',
			convertTypes: ['image/png', 'image/jpeg', 'image/jpg'],
			convertSize: 200 * 1024,
			success(result) {
				const out =
					result instanceof File
						? result
						: new File([result], renameJpeg(file.name), { type: 'image/jpeg', lastModified: Date.now() });
				resolve(out);
			},
			error(err) {
				reject(err);
			},
		});
	});
}

function renameJpeg(name: string): string {
	const base = (name || 'voucher').replace(/\.[^.]+$/, '');
	return `${base || 'voucher'}.jpg`;
}

/**
 * 收银台凭证：图片自动压缩；PDF 仅校验大小。
 * 抛 VoucherPrepareError，由 UI 转成对用户友好的引导。
 */
export async function prepareVoucherFile(file: File): Promise<File> {
	if (isVoucherPdf(file)) {
		if (file.size > VOUCHER_MAX_BYTES) throw new VoucherPrepareError('PDF_TOO_LARGE');
		return file;
	}
	if (!isVoucherImage(file)) throw new VoucherPrepareError('BAD_TYPE');
	if (file.size > VOUCHER_RAW_IMAGE_MAX_BYTES) throw new VoucherPrepareError('IMAGE_TOO_LARGE');

	// 已很小则原样上传，避免二次损失
	if (file.size <= COMPRESS_SOFT_TARGET) {
		if (file.size > VOUCHER_MAX_BYTES) throw new VoucherPrepareError('STILL_TOO_LARGE');
		return file;
	}

	let current = file;
	const passes: Array<{ quality: number; maxSide: number }> = [
		{ quality: 0.82, maxSide: 1600 },
		{ quality: 0.7, maxSide: 1280 },
		{ quality: 0.58, maxSide: 1080 },
	];
	for (const pass of passes) {
		current = await compressOnce(current, pass.quality, pass.maxSide);
		if (current.size <= COMPRESS_SOFT_TARGET) break;
	}

	if (current.size > VOUCHER_MAX_BYTES) throw new VoucherPrepareError('STILL_TOO_LARGE');
	return current;
}

export function voucherPrepareMessage(code: VoucherPrepareErrorCode): string {
	switch (code) {
		case 'BAD_TYPE':
			return '仅支持 jpg、png、pdf 格式。建议直接用手机截取付款成功页面。';
		case 'PDF_TOO_LARGE':
			return 'PDF 文件过大。请改用手机「截图」付款凭证后再上传，通常更小也更清晰。';
		case 'IMAGE_TOO_LARGE':
			return '所选图片过大。请不要直接选相册原图，改用手机「截图」付款凭证后再上传。';
		case 'STILL_TOO_LARGE':
			return '图片仍然偏大，未能上传。请改用手机截图付款凭证（不要选原图），再试一次。';
		default:
			return '文件不符合要求，请改用手机截图后重试。';
	}
}
