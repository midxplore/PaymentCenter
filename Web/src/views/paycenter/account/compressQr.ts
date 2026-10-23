import Compressor from 'compressorjs';

/** 与后端 PayConst.QrImageMaxBytes 对齐 */
export const QR_MAX_BYTES = 5 * 1024 * 1024;
/** 原图允许更大，上传前再压 */
export const QR_RAW_MAX_BYTES = 20 * 1024 * 1024;
/** 压完目标：收银台加载快，同时保留扫码清晰度 */
const COMPRESS_SOFT_TARGET = 450 * 1024;

export type QrPrepareErrorCode = 'BAD_TYPE' | 'IMAGE_TOO_LARGE' | 'STILL_TOO_LARGE';

export class QrPrepareError extends Error {
	readonly code: QrPrepareErrorCode;
	constructor(code: QrPrepareErrorCode) {
		super(code);
		this.code = code;
		this.name = 'QrPrepareError';
	}
}

export function isQrImage(file: File): boolean {
	if (file.type && /^image\/(jpe?g|png|bmp|webp)$/i.test(file.type)) return true;
	return /\.(jpe?g|png|bmp|webp)$/i.test(file.name || '');
}

function compressOnce(file: File, quality: number, maxSide: number): Promise<File> {
	return new Promise((resolve, reject) => {
		new Compressor(file, {
			quality,
			maxWidth: maxSide,
			maxHeight: maxSide,
			mimeType: 'image/jpeg',
			convertTypes: ['image/png', 'image/jpeg', 'image/jpg', 'image/bmp', 'image/webp'],
			convertSize: 100 * 1024,
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
	const base = (name || 'qr').replace(/\.[^.]+$/, '');
	return `${base || 'qr'}.jpg`;
}

/**
 * 收款码：限边长 + 高质量 JPEG，兼顾扫码与加载速度。
 */
export async function prepareQrFile(file: File): Promise<File> {
	if (!isQrImage(file)) throw new QrPrepareError('BAD_TYPE');
	if (file.size > QR_RAW_MAX_BYTES) throw new QrPrepareError('IMAGE_TOO_LARGE');

	if (file.size <= COMPRESS_SOFT_TARGET) {
		if (file.size > QR_MAX_BYTES) throw new QrPrepareError('STILL_TOO_LARGE');
		return file;
	}

	let current = file;
	const passes: Array<{ quality: number; maxSide: number }> = [
		{ quality: 0.9, maxSide: 1200 },
		{ quality: 0.82, maxSide: 1000 },
		{ quality: 0.75, maxSide: 800 },
	];
	for (const pass of passes) {
		current = await compressOnce(current, pass.quality, pass.maxSide);
		if (current.size <= COMPRESS_SOFT_TARGET) break;
	}

	if (current.size > QR_MAX_BYTES) throw new QrPrepareError('STILL_TOO_LARGE');
	return current;
}

export function qrPrepareMessage(code: QrPrepareErrorCode): string {
	switch (code) {
		case 'BAD_TYPE':
			return '只支持 jpg、png、bmp、webp';
		case 'IMAGE_TOO_LARGE':
			return '所选图片过大。请改用截图或压缩后再上传。';
		case 'STILL_TOO_LARGE':
			return '图片仍然偏大，未能上传。请改用截图收款码后再试。';
		default:
			return '图片不符合要求，请重试。';
	}
}
