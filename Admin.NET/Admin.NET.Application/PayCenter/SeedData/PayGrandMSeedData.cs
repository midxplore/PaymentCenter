// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// GrandM 下游样例：机构 / 商户角色 / 用户 / 开放凭证（示范如何加绑定租户下游）
/// </summary>
/// <remarks>
/// Id 段 13000000006xx，与菜单 020x、字典 51xx 互不冲突。
/// 商户角色授「工作台 + 个人中心基础能力 + 收款订单只读」，不含确认到账与账号池。
/// </remarks>
[SeedData(510)]
[IncreSeed]
public class PayGrandMOrgSeedData : ISqlSugarEntitySeedData<SysOrg>
{
    public const long OrgId = 1300000000601;
    public const long UserId = 1300000000611;
    public const long RoleId = 1300000000621;
    public const long OpenAccessId = 1300000000631;

    public IEnumerable<SysOrg> HasData()
    {
        var tenantId = SqlSugarConst.DefaultTenantId;
        return
        [
            new SysOrg
            {
                Id = OrgId,
                Pid = tenantId,
                Name = "GrandM",
                Code = "grandm",
                Type = "301",
                Level = 2,
                Remark = "下游接入方样例机构（PulsePay联调）",
                CreateTime = DateTime.Parse("2026-09-22 00:00:00"),
                TenantId = tenantId
            },
        ];
    }
}

[SeedData(511)]
[IncreSeed]
public class PayGrandMRoleSeedData : ISqlSugarEntitySeedData<SysRole>
{
    public IEnumerable<SysRole> HasData()
    {
        return
        [
            new SysRole
            {
                Id = PayGrandMOrgSeedData.RoleId,
                Name = "商户",
                Code = "pay_merchant",
                DataScope = DataScopeEnum.Self,
                SysFlag = YesNoEnum.N,
                Remark = "下游商户：可登录查看本 Client 订单；不可确认到账/管账号池",
                CreateTime = DateTime.Parse("2026-09-22 00:00:00"),
                TenantId = SqlSugarConst.DefaultTenantId
            },
        ];
    }
}

[SeedData(512)]
[IncreSeed]
public class PayGrandMUserSeedData : ISqlSugarEntitySeedData<SysUser>
{
    public IEnumerable<SysUser> HasData()
    {
        var encryptPassword = CryptogramHelper.Encrypt(
            new SysConfigSeedData().HasData().First(u => u.Code == ConfigConst.SysPassword).Value);
        return
        [
            new SysUser
            {
                Id = PayGrandMOrgSeedData.UserId,
                Account = "grandm",
                Password = encryptPassword,
                NickName = "GrandM",
                RealName = "GrandM商户",
                AccountType = AccountTypeEnum.NormalUser,
                Remark = "下游接入方样例用户",
                OrgId = PayGrandMOrgSeedData.OrgId,
                TenantId = SqlSugarConst.DefaultTenantId,
                CreateTime = DateTime.Parse("2026-09-22 00:00:00")
            },
        ];
    }
}

[SeedData(513)]
[IncreSeed]
public class PayGrandMUserRoleSeedData : ISqlSugarEntitySeedData<SysUserRole>
{
    public IEnumerable<SysUserRole> HasData()
    {
        return
        [
            new SysUserRole
            {
                Id = 1300000000641,
                UserId = PayGrandMOrgSeedData.UserId,
                RoleId = PayGrandMOrgSeedData.RoleId
            },
        ];
    }
}

[SeedData(514)]
[IncreSeed]
public class PayGrandMRoleMenuSeedData : ISqlSugarEntitySeedData<SysRoleMenu>
{
    public IEnumerable<SysRoleMenu> HasData()
    {
        // 商户角色：工作台 + 个人中心基础按钮 + 收款订单只读（不含确认到账/账号池）。
        // 「角色管理」改菜单会整体覆盖；改完请核对是否含 目录/页面/按钮 三层。
        // ★ RoleMenu.Id 固定：活库被 GrantRoleMenu 清空后，IncreSeed 可按 Id 重建。
        (long Id, long MenuId)[] rows =
        [
            // —— 工作台（登录默认跳 /dashboard/home）——
            (1300000000651, 1300000000101), // 工作台 Dir
            (1300000000652, 1300000000111), // 工作台 home
            (1300000000653, 1300000000121), // 站内信
            // —— 个人中心（顶栏改密 / 基本信息，缺了会 403）——
            (1300000000654, 1310000000161), // 个人中心 Menu
            (1300000000655, 1310000000162), // changePwd
            (1300000000656, 1310000000163), // baseInfo
            (1300000000657, 1310000000164), // uploadSignature
            (1300000000658, 1310000000165), // uploadAvatar
            // —— 收款订单只读 ——
            (1300000000659, 1300000000201), // 收款管理 Dir
            (1300000000660, 1300000000221), // 收款订单 Menu
            (1300000000661, 1300000000222), // page
            (1300000000662, 1300000000223), // detail
            (1300000000663, 1300000000226), // statusOptions
        ];
        return rows.Select(r => new SysRoleMenu
        {
            Id = r.Id,
            RoleId = PayGrandMOrgSeedData.RoleId,
            MenuId = r.MenuId
        });
    }
}

[SeedData(515)]
[IncreSeed]
public class PayGrandMOpenAccessSeedData : ISqlSugarEntitySeedData<SysOpenAccess>
{
    public IEnumerable<SysOpenAccess> HasData()
    {
        return
        [
            new SysOpenAccess
            {
                Id = PayGrandMOrgSeedData.OpenAccessId,
                AccessKey = "GrandM",
                // 演示密钥：联调前请在后台「生成密钥」换掉；WebhookUrl 默认空，填下游回调后再派发
                AccessSecret = "GrandMDemoSecretChangeMe",
                Scopes = PayConst.ScopeAllocate,
                Status = StatusEnum.Enable,
                BindTenantId = SqlSugarConst.DefaultTenantId,
                BindUserId = PayGrandMOrgSeedData.UserId,
                WebhookUrl = null,
                CreateTime = DateTime.Parse("2026-09-22 00:00:00")
            },
        ];
    }
}
