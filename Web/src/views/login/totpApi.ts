import { axiosInstance, serveConfig } from '/@/utils/axios-utils';
import type { LoginOutput } from '/@/api-services/system/models';
import type { TotpSetupOutput } from '/@/api-services/system/models/totp-setup-output';

const base = () => serveConfig.basePath || '';

type Wrap<T> = { code?: number; type?: string; message?: string; result?: T };

const opt = { customCatch: true } as any;

export async function fetchTotpSetup(totpTicket: string): Promise<TotpSetupOutput> {
	const res = await axiosInstance.post<Wrap<TotpSetupOutput>>(`${base()}/api/sysAuth/totpSetup`, { totpTicket }, opt);
	if (res.data?.code !== undefined && res.data.code !== 200) throw new Error(res.data.message || '获取二维码失败');
	return res.data?.result ?? {};
}

export async function confirmTotpSetup(totpTicket: string, code: string): Promise<LoginOutput> {
	const res = await axiosInstance.post<Wrap<LoginOutput>>(`${base()}/api/sysAuth/totpConfirmSetup`, { totpTicket, code }, opt);
	if (res.data?.code !== undefined && res.data.code !== 200) throw new Error(res.data.message || '绑定失败');
	return res.data?.result ?? {};
}

export async function verifyTotp(totpTicket: string, code: string): Promise<LoginOutput> {
	const res = await axiosInstance.post<Wrap<LoginOutput>>(`${base()}/api/sysAuth/totpVerify`, { totpTicket, code }, opt);
	if (res.data?.code !== undefined && res.data.code !== 200) throw new Error(res.data.message || '验码失败');
	return res.data?.result ?? {};
}

export async function resetUserTotp(id: number): Promise<void> {
	const res = await axiosInstance.post<Wrap<unknown>>(`${base()}/api/sysUser/resetTotp`, { id }, opt);
	if (res.data?.code !== undefined && res.data.code !== 200) throw new Error(res.data.message || '重置失败');
}
