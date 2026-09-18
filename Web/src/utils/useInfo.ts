import { useUserInfo } from '/@/stores/userInfo';

const { userInfos } = useUserInfo();

/**
 * 判断是否是超级管理员
 * @returns
 */
export const isSupperAdmin = (): boolean => {
	return userInfos?.accountType === 999;
};

/**
 * 判断是否是租户管理员
 * @returns
 */
export const isTenantAdmin = (): boolean => {
	return userInfos?.accountType === 888;
};

/**
 * 判断是否是管理员
 * @returns
 */
export const isAdmin = (): boolean => {
	return isSupperAdmin() || isTenantAdmin();
};

/**
 * 判断是否是普通用户
 * @returns
 */
export const isNormalUser = (): boolean => {
	return userInfos?.accountType === 777;
};

/**
 * 判断是否是会员
 * @returns
 */
export const isMember = (): boolean => {
	return userInfos?.accountType === 666;
};

/**
 * 获取邮件
 * @returns 获取邮件
 */
export const userEmail = (): string => {
	return userInfos?.email;
};

/**
 * 获取用户id
 * @returns 获取用户id
 */
export const userName = (): string => {
	return userInfos?.userName;
};

/**
 * 获取用户好友名称
 * @returns 获取用户好友名称
 */
export const userFriendName = (): string => (userInfos?.realName ? userInfos?.realName : userInfos?.account ? userInfos?.account : userInfos?.email);

/**
 * 获取租户id
 * @returns 获取租户id
 */
export const tenantId = (): number => {
	return userInfos?.tenantId;
};

/***
 * 获取用户账户.
 */
export const userAccount = (): string => userInfos?.account;

/**
 * 获取用户手机
 * @returns 获取用户手机
 */
export const userPhone = (): string => userInfos?.phone;

/**
 * 获取用户id
 * @returns 获取用户id.
 */
export const userId = (): number => userInfos?.id;

/**
 * 获取组织id
 * @returns 获取组织id
 */
export const orgId = (): number => userInfos?.orgId;

/**
 * 获取组织名称
 * @returns 获取组织名称
 */
export const orgName = (): string => userInfos?.orgName;

/**
 * 获取职位id
 * @returns 获取职位id.
 */
export const posId = (): number => userInfos?.posId;

/**
 * 获取部位名称
 * @returns 获取职位名称
 */
export const posName = (): string => userInfos?.posName;

/**
 * 获取角色集合
 * @returns 获取角色集合
 */
export const roles = (): any => userInfos?.roles;

/**
 * 判断是否有指定角色编码
 * @param code
 * @returns
 */
export const hasRoleCode = (code: string): boolean => {
	return userInfos.roles.find((u) => u.code === code) ? true : false;
};

/**
 * 判断是否有某接口权限
 * @param privilege
 * @returns
 */
export const hasPrivilege = (privilege: string): boolean => {
	return userInfos.authApiList.includes(privilege);
};
