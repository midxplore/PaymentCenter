<div align="center"><h1>PaymentCenter</h1></div>
<div align="center"><h3>收款账号分配系统</h3></div>

按「收款类型 + 金额」从收款账号池中匹配一个额度充足的账号/码并生成订单；到账由外部系统异步上报（支持分次到账），
最终形成一条可审计的收款流水闭环。构建在 **Admin.NET.Pro 裁剪版**之上 —— 只保留框架与 **PayCenter** 一个业务模块。

## 技术基线

| 项 | 内容 |
|---|---|
| 后端 | .NET 8 + Furion.Pure `4.9.7.221` + SqlSugar `5.1.4.210` |
| 数据库 | **PostgreSQL（唯一形态）** —— 额度预占依赖 `FOR UPDATE` 行锁语义，其它库不可迁移 |
| 前端 | Vue `3.5` + Element Plus `2.12` + Vite `7.2`（`Web/`） |
| 时区 | **必须 `TZ=Asia/Shanghai`** —— 全部时间列是 `timestamp without time zone`，不符则**拒绝启动** |
| 端口 | 后端 `5005` / 前端 `8888` / 本地 PG 容器 `55432` |

## 功能范围（需求 F1~F7）

| 编号 | 功能 | 落点 |
|---|---|---|
| F1 | 收款账号/码管理、追加额度、启用停用、额度耗尽自动下架 | 后台「收款管理 → 收款账号」 |
| F2 | 查询匹配（剩余额度最接近者优先）+ 额度预占 + 幂等 | `POST /api/pay/allocate` |
| F3 | 全局过期时长 + 每分钟自动过期并释放未达成预占 | 后台作业 `pay_order_expire_job` |
| F4 | 异步到账通知、分次到账、凭证号去重 | `POST /api/pay/notify` |
| F5 | 异常到账台账 + 人工关联到订单 | 后台「收款管理 → 异常到账」 |
| F6 | 签名鉴权（HMAC-SHA256）+ scope 权限隔离 + 密钥脱敏/停用 | `/api/pay/*` |
| F7 | 订单全生命周期、业务审计（只增不改）、4 类导出 | 后台「收款管理 → 收款订单 / 业务审计」 |

- 对外接口只有 3 个：`/api/pay/allocate`、`/api/pay/notify`、`/api/pay/status`，全部要求**签名鉴权 + scope**
- 本期不做：周期性额度、退款、多因子路由、IP 白名单、超额到账自动化流程

## 快速开始

```bash
scripts/dev-up.sh                                  # 库 → 构建 → 后端 → schema 纠偏 → 守卫
cd Web && env -u NODE_OPTIONS npm run dev          # http://localhost:8888
scripts/dev-down.sh                                # 停止（--with-db 连库一起停）
scripts/regress-all.sh --with-login                # 全量 HTTP 回归（串行跑 4 个脚本）
```

> 路径坑（dotnet / docker / npm / python 都不在 PATH 上）、时区要求与故障速查见 [doc/本地开发环境.md](doc/本地开发环境.md)。

## 文档

| 文档 | 用途 |
|---|---|
| **[AGENTS.md](AGENTS.md)** | **在本仓库工作的第一入口**：硬约束、命令表、「静默失效」清单 |
| [技术设计方案](doc/收款账号分配系统-技术设计方案.md) | **唯一事实来源**：行为、接口契约、设计决策 |
| [功能需求文档 V3](doc/收款账号分配系统-功能需求文档-V3.md) | 需求原文 |
| [本地开发环境](doc/本地开发环境.md) | 怎么跑、故障速查 |
| [S7 验收报告](doc/S7-验收报告.md) | 验收证据与已修缺陷 |
| [scripts/paycenter-schema.sql](scripts/paycenter-schema.sql) | 期望 schema 的可执行契约（幂等 DDL） |
| [scripts/pay_schema_guard.py](scripts/pay_schema_guard.py) | schema 漂移 + 源码安全不变式守卫（退出码可挂 CI） |

> ⚠️ 连接串在 `Admin.NET/Admin.NET.Application/Configuration/Database.json`（**唯一来源**）。
> 该文件会**随发布分发**，部署时必须替换为生产连接串，切勿直接用于生产。

---

## 📎 附录：上游 Admin.NET.Pro 说明（原文保留，仅作参考）

> 本仓库是上游框架的**裁剪版**：保留 `Core / Application / Web.Core / Web.Entry` 四层与本项目模块，
> 框架自带的示例业务模块与部署脚手架（`docker-compose*.yml` / `build.sh` / `nginx/` 等）已删除。
> 下面这段是**上游原文**，其中的版本号（`.NET6` / `Vite5`）与部署说明（MySQL 等）描述的是**上游**，
> 与本仓库基线不同，不要照抄。

<div align="center"><h1>Admin.NET.Pro</h1></div>
<div align="center"><h3>站在巨人肩膀上的 .NET 通用权限开发框架</h3></div>

<div align="center">

[![star](https://gitee.com/zuohuaijun/Admin.NET/badge/star.svg?theme=dark)](https://gitee.com/zuohuaijun/Admin.NET/stargazers)
[![fork](https://gitee.com/zuohuaijun/Admin.NET/badge/fork.svg?theme=dark)](https://gitee.com/zuohuaijun/Admin.NET/members)
[![GitHub license](https://img.shields.io/badge/license-MIT-yellow)](https://gitee.com/zuohuaijun/Admin.NET/blob/next/LICENSE)

</div>

## 🎁框架介绍
Admin.NET 是基于 .NET6 (Furion/SqlSugar) 实现的通用权限开发框架，前端采用 Vue3+Element-plus+Vite5，整合众多优秀技术和框架，模块插件式开发。集成多租户、缓存、数据校验、鉴权、事件总线、动态API、通讯、远程请求、任务调度、打印等众多黑科技。代码结构简单清晰，注释详尽，易于上手与二次开发，即便是复杂业务逻辑也能迅速实现，真正实现“开箱即用”。

面向中小企业快速开发平台框架，框架采用主流技术开发设计，前后端分离架构模式。完美适配国产化软硬件环境，支持国产中间件、国产数据库、麒麟操作系统、Windows、Linux部署使用；集成国密加解密插件，使用SM2、SM3、SM4等国密算法进行签名、数据完整性保护；软件层面全面遵循等级保护测评要求，完全符合等保、密评要求。

```
超高人气的框架(Furion)配合高性能超简单的ORM(SqlSugar)加持，阅历痛点，相见恨晚！让 .NET 开发更简单，更通用，更流行！
```

## 🍁说明
1.  支持各种数据库，后台配置文件自行修改（自动生成数据库及种子数据）
2.  前端运行步骤：1、安装依赖pnpm install 2、运行pnpm run dev 3、打包pnpm run build
3.  QQ交流群1：[87333204](https://jq.qq.com/?_wv=1027&k=1t8iqf0G)  QQ交流群2：[252381476](https://jq.qq.com/?_wv=1027&k=IkzihDcL)  
4.  演示环境1：https://demo.adminnet.top  账号：superAdmin.NET  密码：Admin.NET++010101
5. [GitHub 镜像地址](https://github.com/zuohuaijun/Admin.NET.git)  [Gitee 镜像地址](https://gitee.com/zuohuaijun/Admin.NET.git)  [GitCode 镜像地址](https://gitcode.com/zuohuaijun/Admin.NET.git)
6.  在线文档 [https://adminnet.top/](https://adminnet.top/)

## 📙开发流程
```bash
1. 建议每个应用系统单独创建一个工程（Admin.NET.Application层只是示例），单独设置各项配置，引用Admin.NET.Core层（非必须不改工程名）

2. Web层引用新建的应用层工程即可（所有应用系统一个解决方案显示一个后台一套代码搞定，可以自由切换不同应用层）

# 可以随主仓库升级而升级避免冲突，原则上接口、服务、控制器合并模式不影响自建应用层发挥与使用。若必须修改或补充主框架，也欢迎PR！

```

## 🍎效果截图
<table>
    <tr>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/1.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/2.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/3.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/4.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/5.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/6.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/7.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/8.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/9.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/10.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/11.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/12.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/13.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/14.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/15.png"/></td>
        <td><img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/16.png"/></td>
    </tr>
</table>

## 🍖内置功能
 1. 主控面板：控制台页面，可进行工作台，分析页，统计等功能的展示。
 2. 用户管理：对企业用户和系统管理员用户的维护，可绑定用户职务，机构，角色，数据权限等。
 3. 机构管理：公司组织架构维护，支持多层级结构的树形结构。
 4. 职位管理：用户职务管理，职务可作为用户的一个标签。
 5. 菜单管理：配置系统菜单，操作权限，按钮权限标识等，包括目录、菜单、按钮。
 6. 角色管理：角色绑定菜单后，可限制相关角色的人员登录系统的功能范围。角色也可以绑定数据授权范围。
 7. 字典管理：对系统中经常使用的一些较为固定的数据进行维护。
 8. 访问日志：用户的登录和退出日志的查看和管理。
 9. 操作日志：系统正常操作日志记录和查询；系统异常信息日志记录和查询。
10. 服务监控：服务器的运行状态，CPU、内存、网络等信息数据的查看。
11. 在线用户：当前系统在线用户的查看，包括强制下线。基于 SignalR 实现。
12. 公告管理：系统通知公告信息发布维护，使用 SignalR 实现对用户实时通知。
13. 文件管理：文件的上传下载查看等操作，文件可使用本地存储，阿里云oss、腾讯cos等接入，支持拓展。
14. 任务调度：采用 Sundial，.NET 功能齐全的开源分布式作业调度系统。
15. 系统配置：系统运行的参数的维护，参数的配置与系统运行机制息息相关。
16. 邮件短信：发送邮件功能、发送短信功能。
17. 系统接口：使用 Swagger 生成相关 api 接口文档。支持 Knife4jUI 皮肤。
18. 代码生成：可以一键生成前后端代码，自定义配置前端展示控件，让开发更快捷高效。
19. 在线构建器：拖动表单元素生成相应的 VUE 代码(支持vue3)。
20. 对接微信：对接微信小程序开发，包括微信支付。
21. 导入导出：采用 Magicodes.IE 支持文件导入导出，支持根据H5模板生成PDF等报告文件。
22. 限流控制：采用 AspNetCoreRateLimit 组件实现对接口访问限制。
23. ES 日志：通过 NEST 组件实现日志存取到 Elasticsearch 日志系统。
24. 开放授权：支持OAuth 2.0开放标准授权登录，比如微信。
25. APIJSON：适配腾讯APIJSON协议，支持后端0代码，[使用文档](https://github.com/liaozb/APIJSON.NET)。
26. 数据库视图：基于SqlSugar生成查询SQL + 表实体维护视图，可维护性更强。

## 🎀捐赠支持
```
如果对您有帮助，请点击右上角⭐Star关注或扫码捐赠，感谢支持开源！
```
<img src="https://gitee.com/zuohuaijun/Admin.NET/raw/next/doc/img/pay.png"/>

## 💐特别鸣谢
- 👉 Furion：[https://gitee.com/dotnetchina/Furion](https://gitee.com/dotnetchina/Furion)
- 👉 vue-next-admin：[https://lyt-top.gitee.io/vue-next-admin-doc-preview/](https://lyt-top.gitee.io/vue-next-admin-doc-preview/)
- 👉 SqlSugar：[https://gitee.com/dotnetchina/SqlSugar](https://gitee.com/dotnetchina/SqlSugar)
- 👉 NewLife.Redis：[https://github.com/NewLifeX/NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)
- 👉 Magicodes.IE：[https://gitee.com/magicodes/Magicodes.IE](https://gitee.com/magicodes/Magicodes.IE)
- 👉 SKIT.FlurlHttpClient.Wechat：[https://gitee.com/fudiwei/DotNetCore.SKIT.FlurlHttpClient.Wechat](https://gitee.com/fudiwei/DotNetCore.SKIT.FlurlHttpClient.Wechat)
- 👉 IdGenerator：[https://github.com/yitter/idgenerator](https://github.com/yitter/idgenerator)
- 👉 UAParser：[https://github.com/ua-parser/uap-csharp/](https://github.com/ua-parser/uap-csharp/)
- 👉 OnceMi.AspNetCore.OSS：[https://github.com/oncemi/OnceMi.AspNetCore.OSS](https://github.com/oncemi/OnceMi.AspNetCore.OSS)
- 👉 NETCore.MailKit：[https://github.com/myloveCc/NETCore.MailKit](https://github.com/myloveCc/NETCore.MailKit)
- 👉 Lazy.Captcha.Core：[https://gitee.com/pojianbing/lazy-captcha](https://gitee.com/pojianbing/lazy-captcha)
- 👉 AspNetCoreRateLimit：[https://github.com/stefanprodan/AspNetCoreRateLimit](https://github.com/stefanprodan/AspNetCoreRateLimit)
- 👉 Elasticsearch.Net：[https://github.com/elastic/elasticsearch-net](https://github.com/elastic/elasticsearch-net)
- 👉 Masuit.Tools：[https://gitee.com/masuit/Masuit.Tools](https://gitee.com/masuit/Masuit.Tools)
- 👉 IGeekFan.AspNetCore.Knife4jUI：[https://github.com/luoyunchong/IGeekFan.AspNetCore.Knife4jUI](https://github.com/luoyunchong/IGeekFan.AspNetCore.Knife4jUI)
- 👉 AspNet.Security.OAuth.Providers：[https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers)
- 👉 System.Linq.Dynamic.Core：[https://github.com/zzzprojects/System.Linq.Dynamic.Core](https://github.com/zzzprojects/System.Linq.Dynamic.Core)
- 👉 APIJSON.NET：[https://github.com/liaozb/APIJSON.NET](https://github.com/liaozb/APIJSON.NET)
- 👉 vue-plugin-hiprint：[https://gitee.com/CcSimple/vue-plugin-hiprint](https://gitee.com/CcSimple/vue-plugin-hiprint)
