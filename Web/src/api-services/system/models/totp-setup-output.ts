/* tslint:disable */
/* eslint-disable */
/**
 * TOTP 绑定二维码输出（手工补齐，未跑 buildApi）
 */
export interface TotpSetupOutput {
	qrCodeImage?: string | null;
	manualKey?: string | null;
	account?: string | null;
	issuer?: string | null;
}
