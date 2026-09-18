import request from '/@/utils/request';
enum Api {
	GetTenantIdByHostname = '/api/sysAuth/tenantIdByHostname',
}
/**
 * （不建议写成 request.post(xxx)，因为这样 post 时，无法 params 与 data 同时传参）
 *
 * 登录api接口集合
 * @method signIn 用户登录
 * @method signOut 用户退出登录
 */
export function useLoginApi() {
	return {
		signIn: (data: object) => {
			return request({
				url: '/user/signIn',
				method: 'post',
				data,
			});
		},
		signOut: (data: object) => {
			return request({
				url: '/user/signOut',
				method: 'post',
				data,
			});
		},
	};
}

export const getTenantIdByHostname = (hostname: string) =>
	request({
		url: `${Api.GetTenantIdByHostname}/${hostname}`,
		method: 'get',
	});
