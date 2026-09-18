// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款账号分配系统菜单种子数据（F7.2 / F7.3 / F7.5 后台入口）
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么放在 PayCenter 目录而不是 Application/SeedData/SysMenuSeedData.cs</b>：
/// 本模块要能整体拆出去独立部署，菜单属于模块的交付物之一，跟模块代码放一起才不会拆漏。
/// </para>
/// <para>
/// <b>执行顺序 <see cref="SeedDataAttribute"/> = 503</b>：必须大于 500，
/// 因为框架要求「应用层种子最后执行」（见 <c>SqlSugarSetup.InitSeedData</c> 注释）。
/// </para>
/// <para>
/// <b>Permission 串的取值规则</b>：必须是接口路由的<b>尾部</b>——框架用
/// <c>path.EndsWith(permission)</c> 匹配（见 <c>JwtHandler.CheckAuthorizeAsync</c>），
/// 所以 <c>payAccount/page</c> 对应 <c>/api/payAccount/page</c>。
/// </para>
/// <para>
/// <b>谁需要这些行</b>：
/// <list type="bullet">
/// <item>菜单行（Type=Dir/Menu）决定侧边栏是否出现该页面；</item>
/// <item>按钮行（Type=Btn）决定 <c>v-auth</c> 是否显示对应按钮，同时充当接口权限白名单。</item>
/// </list>
/// 超管（<c>AccountType=SuperAdmin</c>）在 <c>JwtHandler</c> 里直接放行、且
/// <c>SysMenuService.GetLoginMenuTree</c> 对超管返回全部启用菜单，所以超管开箱即用；
/// <b>非超管角色需要到「角色管理」里勾选这些菜单</b>，本文件不代替角色授权。
/// </para>
/// </remarks>
[SeedData(503)]
[IncreSeed]
public class PayMenuSeedData : ISqlSugarEntitySeedData<SysMenu>
{
    /// <summary>
    /// 种子数据
    /// </summary>
    /// <returns></returns>
    public IEnumerable<SysMenu> HasData()
    {
        // Id 段说明：框架建议「业务应用菜单」放在 1300000000101 ~ 1310000000101 之间，
        // 这里用 1300000000201~1300000000243，与既有 1300000000111（工作台）、
        // 1300000005101+（收款字典/配置）互不冲突。
        var createTime = DateTime.Parse("2026-09-17 00:00:00");

        return
        [
            new SysMenu{ Id=1300000000201, Pid=0, Title="收款管理", Path="/paycenter", Name="paycenter", Component="Layout", Icon="ele-Money", Type=MenuTypeEnum.Dir, CreateTime=createTime, OrderNo=500 },

            //// 收款账号（F1）
            new SysMenu{ Id=1300000000211, Pid=1300000000201, Title="收款账号", Path="/paycenter/account", Name="payAccount", Component="/paycenter/account/index", Icon="ele-CreditCard", Type=MenuTypeEnum.Menu, CreateTime=createTime, OrderNo=100 },
            new SysMenu{ Id=1300000000212, Pid=1300000000211, Title="查询", Permission="payAccount/page", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=100 },
            new SysMenu{ Id=1300000000213, Pid=1300000000211, Title="增加", Permission="payAccount/add", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=110 },
            new SysMenu{ Id=1300000000214, Pid=1300000000211, Title="编辑", Permission="payAccount/update", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=120 },
            new SysMenu{ Id=1300000000215, Pid=1300000000211, Title="删除", Permission="payAccount/delete", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=130 },
            new SysMenu{ Id=1300000000216, Pid=1300000000211, Title="详情", Permission="payAccount/detail", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=140 },
            new SysMenu{ Id=1300000000217, Pid=1300000000211, Title="追加额度", Permission="payAccount/addQuota", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=150 },
            new SysMenu{ Id=1300000000218, Pid=1300000000211, Title="设置状态", Permission="payAccount/setStatus", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=160 },
            // ★ 下拉字典（账号类型）。**必须单独挂权限**：JwtHandler 是按 path.EndsWith(permission) 匹配的，
            //   "payAccount/page" 不会命中 "/api/payAccount/typeOptions"，所以不挂就等于对任何已登录用户放行。
            //   字典本身不敏感，但「未纳入 RBAC 的接口」会让权限清单失去意义、审计也无法回答「谁调过」。
            new SysMenu{ Id=1300000000219, Pid=1300000000211, Title="类型选项", Permission="payAccount/typeOptions", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=170 },

            //// 收款订单（F7.1 全生命周期 / F7.2 订单与账号双视角）
            new SysMenu{ Id=1300000000221, Pid=1300000000201, Title="收款订单", Path="/paycenter/order", Name="payOrder", Component="/paycenter/order/index", Icon="ele-Tickets", Type=MenuTypeEnum.Menu, CreateTime=createTime, OrderNo=110 },
            new SysMenu{ Id=1300000000222, Pid=1300000000221, Title="查询", Permission="payOrder/page", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=100 },
            new SysMenu{ Id=1300000000223, Pid=1300000000221, Title="详情", Permission="payOrder/detail", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=110 },
            new SysMenu{ Id=1300000000224, Pid=1300000000221, Title="导出", Permission="payExport/exportOrder", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=120 },
            // ★ 以下三条原先**没有任何按钮权限** → 对任何已登录用户开放。危害排序：
            //   1) detailByNo：按订单号直接取全量详情（含收款账号、金额、事件流水），等于把订单查询变成可枚举接口；
            //   2) exportNotify：导出**全部**到账流水，是一次整表外带；
            //   3) statusOptions：下拉字典，不敏感，但同样应纳入 RBAC。
            //   前端没有对应按钮（由脚本/测试调用），所以只能靠菜单种子里补 Btn 行来收口。
            //   ⚠️ 非超管角色需要在「角色管理」里勾选这三项，否则调用会被 403（超管自动全量授权）。
            new SysMenu{ Id=1300000000225, Pid=1300000000221, Title="按单号详情", Permission="payOrder/detailByNo", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=130 },
            new SysMenu{ Id=1300000000226, Pid=1300000000221, Title="状态选项", Permission="payOrder/statusOptions", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=140 },
            new SysMenu{ Id=1300000000227, Pid=1300000000221, Title="导出到账流水", Permission="payExport/exportNotify", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=150 },

            //// 异常到账台账（F5）
            new SysMenu{ Id=1300000000231, Pid=1300000000201, Title="异常到账", Path="/paycenter/abnormal", Name="payAbnormal", Component="/paycenter/abnormal/index", Icon="ele-WarningFilled", Type=MenuTypeEnum.Menu, CreateTime=createTime, OrderNo=120 },
            new SysMenu{ Id=1300000000232, Pid=1300000000231, Title="查询", Permission="payAbnormal/page", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=100 },
            new SysMenu{ Id=1300000000233, Pid=1300000000231, Title="人工关联", Permission="payAbnormal/link", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=110 },
            new SysMenu{ Id=1300000000234, Pid=1300000000231, Title="确认无需处理", Permission="payAbnormal/ignore", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=120 },
            new SysMenu{ Id=1300000000235, Pid=1300000000231, Title="导出", Permission="payExport/exportAbnormal", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=130 },

            //// 业务审计（F7.3）
            new SysMenu{ Id=1300000000241, Pid=1300000000201, Title="业务审计", Path="/paycenter/audit", Name="payAudit", Component="/paycenter/audit/index", Icon="ele-Document", Type=MenuTypeEnum.Menu, CreateTime=createTime, OrderNo=130 },
            new SysMenu{ Id=1300000000242, Pid=1300000000241, Title="查询", Permission="payAudit/page", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=100 },
            new SysMenu{ Id=1300000000243, Pid=1300000000241, Title="导出", Permission="payExport/exportAuditLog", Type=MenuTypeEnum.Btn, CreateTime=createTime, OrderNo=110 },
        ];
    }
}
