# AGENTS.md — PaymentCenter（收款账号分配系统）

> 面向在本仓库工作的 AI agent。只写「不看就会写错」的内容；为什么/实测数据见
> `.workbuddy-ai/memory/PROJECT-NOTES.md`，本文件不复述。
> 核心心法：**本项目的坑几乎都是「静默」型** —— 不报错、测试全绿，但结果是错的。

## 0. 项目与结构

- **是什么**：Admin.NET.Pro 裁剪版（.NET 8 + Furion.Pure 4.9.7.221 + SqlSugar），只保留一个业务模块 **PayCenter**（需求 F1~F7）。前端 Vue3 + Element Plus + Vite 7。
- **后端**：`Admin.NET/Admin.NET.sln`（Core / Application / Web.Core / Web.Entry / Test）。
  - 业务代码只放 `Admin.NET.Application/PayCenter/`（Entity / Dto / Service / Auth / Job / SeedData / Const / Enum）。
  - 裁剪现状：Application 层只剩框架 `Service/App/Auth` + PayCenter；**不要新增项目**，不要假设 Admin.NET 的示例业务模块还在。Core 是完整框架（Auth/Cache/Job/Menu/OpenAccess/Tenant/User…），可复用。
- **前端**：`Web/src/views/` 同时有框架页面（`system/*`、`login`、`home`、`about`）与本项目页面（`paycenter/*`）。框架页面基本不改；`src/views/system/openAccess/` 是本项目改造过的（密钥脱敏 / 停用 / scopes）。
- **脚本**：`scripts/` = 本地环境 + HTTP 回归 + schema 契约/守卫；`doc/` = 需求/设计/验收/本地环境；`.workbuddy-ai/memory/` = 项目长期记忆。
- ⚠️ **本仓库当前不是 git 仓库**（无 `.git`）。大改/删除前先 `cp` 到 `/tmp`；**不要擅自删文件**（不可回滚）。后续统一推送到 `git@github.com:midxplore/PaymentCenter.git`。

## 1. 唯一事实来源（冲突时以此为准）

1. **`doc/收款账号分配系统-技术设计方案.md`** —— 行为、接口契约、设计决策的唯一事实来源。**改实现前先读，改完回来更新它。**
2. `scripts/paycenter-schema.sql` —— 期望 schema 的可执行契约（幂等 DDL，补 CodeFirst 不做的列宽/精度纠偏）。
3. `scripts/pay_schema_guard.py` —— 漂移守卫（schema + 源码级安全不变式，退出码可挂 CI）。**改实体/入参 DTO 必跑。**
4. `.workbuddy-ai/memory/MEMORY.md`（热规则，**接近注入上限**，新增内容写 `PROJECT-NOTES.md`）+ `PROJECT-NOTES.md`（为什么）。
5. `doc/本地开发环境.md`（怎么跑 / 故障速查）、`doc/S7-验收报告.md`（验收证据与已修缺陷）。
6. 更早一版同系统在 `/Users/Working/Project/Agent/Payment-Center`（`Payment.Application` 已实现签名鉴权/过期作业/异常台账/导出/后台列表）—— 做相关功能前先读，不要从零写。

## 2. 命令（路径都不在 PATH，照抄）

| 事项 | 命令 |
|---|---|
| 起环境（库→构建→后端→schema 纠偏→守卫） | `scripts/dev-up.sh`（后端 :5005，日志 `Admin.NET/Admin.NET.Web.Entry/logs/dev-backend.log`） |
| 停环境 | `scripts/dev-down.sh`（`--with-db` 连库停；`--purge-db` 删卷） |
| 前端 dev | `cd Web && env -u NODE_OPTIONS npm run dev`（:8888） |
| 构建 | `/usr/local/share/dotnet/dotnet build Admin.NET/Admin.NET.sln`（基线 0 warning / 0 error） |
| 单元测试（走真实 PG，需先 dev-up） | `/usr/local/share/dotnet/dotnet test Admin.NET/Admin.NET.Test/Admin.NET.Test.csproj` |
| schema 守卫（改实体/DTO 必跑） | `~/.workbuddy-ai/binaries/python/envs/default/bin/python scripts/pay_schema_guard.py` |
| 全量 HTTP 回归 | `scripts/regress-all.sh --with-login`（自动关验证码 → **串行**跑 4 脚本 → 保证还原 → 守卫自验；退出码 2 = 前置不满足，**不是通过**） |
| 前端类型检查（**必跑**，闸门必须 0 错） | `cd Web && env -u NODE_OPTIONS npm run typecheck`（全量：`npm run typecheck:all`） |
| 前端构建 | `cd Web && env -u NODE_OPTIONS npm run build` |
| 查库 | `docker exec paymentcenter-pg psql -U payment -d paymentcenter -tAc "<SQL>"` |
| 读启动日志 | 日志含连接串 → 加 `\| grep -av "Host="` |

- **路径坑**：dotnet `/usr/local/share/dotnet/dotnet`；docker 需 `export PATH="/usr/local/bin:$PATH"`；python 只用 `~/.workbuddy-ai/binaries/python/envs/default/bin/python`（**只有它装了 psycopg2**）；npm 一律 `env -u NODE_OPTIONS`（宿主注入的 shim 会让 vite 崩）。
- **端口**：后端 5005（**不要 5000**，macOS AirPlay 占用）、前端 8888、PG 55432（不是 5432）。
- **改了业务代码必须 `dotnet build Admin.NET.sln` 再重启**：`dotnet run --no-build` 跑旧二进制；`dotnet test` 只刷新 `Application/bin`，不刷新 `Web.Entry/bin`。
- 只改一两个 `.vue` 时可先跑秒级语法校验：`cd Web && node script/check-sfc.cjs <file>`（只覆盖模板编译层，类型问题仍要 `npm run typecheck`）。

## 3. 硬约束（违反会静默出错）

### 额度 / 并发
- **数值比较一律「列在左」**：`u.Total >= u.Used + u.Locked + amount`（历史 SQLite 亲和性坑；PG 下不再触发，但保留写法）。
- **额度预占必须阻塞式 `FOR UPDATE`，禁用 `SKIP LOCKED`**。`LIMIT 1` + `SKIP LOCKED` 会把「等不到锁」错报成 `P1001 无可用收款账号` → 三方不重试 → **丢单**（实测 10 并发只成功 2 笔）。等到锁后外层 WHERE 重新求值额度。
- 额度语义：`剩余 = Total − Used − Locked`（**不落库**）；锁定按**请求金额**释放，已用按**实际到账**累加。
- **过期与到账的竞态：先条件抢占订单状态，再读到账金额**（顺序反了会按偏小旧值结转 → 账实不符）。

### 鉴权 / 审计
- 调用方身份只有一个来源 `PayCallerContext.RequireClientId`，**禁止 `?? 0` 兜底**（退化成 0 会让所有接入方共享去重命名空间 → A 的凭证号静默去重掉 B 的到账通知）。
- 幂等键唯一索引是 **`(clientid, externalno)` 复合**，查询必须带 `ClientId`（幂等查询只有一处实现）。
- `notify` 刻意**不**按 ClientId 过滤（渠道不建单）；`allocate` / `status` 必须隔离。`status` 的「查无此单」与「不是你的单」返回同一个 `P1004`。
- 对外路由 `/api/pay/{allocate,notify,status}` 必须 `[Authorize(Signature)] + [PayScope(...)]`；新增 `/api/pay*` 接口忘写 `[PayScope]` 会被 `ScopeUnclassified` fail-closed 拒绝（**不要**改回返回 null）。
- **密钥出参必须在 getter 层脱敏**（掩码 `****`）；**「掩码 = 不改」**；改 accessKey 要**同时清新旧两个缓存**；停用凭证返回空密钥（与「无效 key」不可区分）。签名比较用 `FixedTimeEquals`。
- **审计两条线**：认证失败（401 在 MVC 之前，`SysLogOp` 采不到）走 `IOpenAccessAuditSink` → `pay_audit_log(action=ApiAuthFailure)`，**必须吞掉所有异常、只记带 accessKey 的失败、写入前按列宽截断**；资金动作走 `PayAuditService.WriteOpenApiAsync`（**同事务**），只读的 `status` 不审计。
- `pay_audit_log` / `pay_order_event` **只增不改**，无 update/delete 接口。

### 时间基准
- **`TZ=Asia/Shanghai` 是硬前提**（全部时间列 `timestamp without time zone` + `DateTime.Now`）。不符时 `PayCenterStartup` 启动期**直接拒绝启动**；容器 / systemd 都要设。漏设会让「历史行 +08 / 新行 UTC」混进同一列且不报错。
- 判定不能按时区 Id（本机是 `PRC`），也不能用 `SupportsDaylightSavingTime`。
- 前端 `el-date-picker` 一律 `value-format="YYYY-MM-DD HH:mm:ss"`；**不要**为消类型错改成 `new Date(...)`（会整体偏 8 小时）。构造收在 `views/paycenter/utils/exportParams.ts`。

### 数据库 / schema
- `pay_*` 继承 `EntityBase`，**不开多租户**（账号池公司级共享）。
- **CodeFirst 会建表建索引，但不改已有列宽/类型、不删旧索引。** 改列宽/索引要三处联动：① `PayConst.cs` 常量（唯一来源）② `scripts/paycenter-schema.sql` ③ DTO `[MaxLength(...)]` **引用常量**。改索引必须 `DROP INDEX IF EXISTS 旧名;` 再建新名。⚠️ 精度 scale 变小会四舍五入已有数据，先备份。
- 数据库配置只走 `ConfigurationLocal/LocalOverride.json` 覆盖 `ConnectionConfigs[0]` 的 `DbType`/`ConnectionString`；**`Configuration/Database*.json` 含连接串，不要读、不要改**（读取会被沙箱拦截）。文件名必须唯一（不能叫 `Database.json`）。Furion 读的是 **`bin/<cfg>/` 副本**，改完要重新 build。
- **`ConfigurationLocal` 目录必须存在**（缺 → 主机构建期抛 `DirectoryNotFoundException` → 「进程秒退、`logs/` 空」）。`appsettings.Development.json` **故意不写** `ConfigurationScanDirectories`，不要「顺手对齐」两份配置（会静默回落 SQLite）。
- SQLite 只是应急回落；**正式开发必须 PG**（`FOR UPDATE` 行锁语义 SQLite 没有，压测结论不可迁移）。
- 直连 SQL：**PG 列名全小写**（`orderno` 不是 `OrderNo`）。
- 种子对象（菜单/字典/配置/作业）由启动种子幂等写入，**不要**在 SQL 里重复插入。菜单种子随模块走：`PayCenter/SeedData/PayMenuSeedData.cs`，**不放**框架 `SysMenuSeedData.cs`。

### 入参 DTO（三条静默失效）
- **`[Required]` 挂在非空值类型（long/int/enum）上 = 空操作** → 要拦 `0` 用 `[Range(1, long.MaxValue, ...)]`。
- **有可选字段的入参 DTO 平铺声明，不继承实体**（`DataAnnotations` 的 `Inherited=true` 会让实体 `[Required]` 漏进来；识别特征 = 报**默认英文**文案）。
- **更新入参的状态字段必须可空**（给默认值会把已停用凭证静默改回启用）。

### 前端
- **后端驱动路由**：菜单种子 `component` 在 `import.meta.glob` 里**必须恰好命中 1 个**（0 → 空白页；>1 → 返回 false → 白屏）。
- **权限是黑名单模型**：`apiList[1] = 全部按钮 − 用户已有按钮`，`JwtHandler` 按 `path.EndsWith(permission)` 匹配 → **没有对应按钮权限的接口 = 任何已登录用户都能调**。新增后台接口必须补按钮种子。
- **`permission` 写接口路由的「尾部」**（如 `payAccount/page`），**不要**写全路径；路由由方法名推导（不一定等于方法名），权威来源是 `Web/src/api-services/**` 的 `localVarPath`。
- 超管免菜单授权；非超管要在「角色管理」勾。一级 `type=1` + `component=Layout`；二级 `type=2` + `component=/paycenter/xxx/index`；三级 `type=3` 只填 `permission`。
- `npm run typecheck` 闸门 = `src/views/paycenter/` + `src/api-services/` + `src/views/system/openAccess/`，**必须 0 错**。**自有页面若落在 `src/views/system/` 下，必须显式加进闸门**，否则会被债务条目整段吞掉。
- 类型检查**查不出**运行期契约：菜单 `component` 唯一命中、`v-auth` 串在 permission 里、后端 `[MaxLength]`/`[Required]`。改完自检这三项。

## 4. 本机工具链陷阱（沙箱特有）

- **端口探活不能用 `curl`**（对无监听端口也返回退出码 0）→ 用 `lsof -nP -iTCP:$PORT -sTCP:LISTEN`，拿不到就 fail-closed。
- **`grep` 是 toybox，非 GNU，不支持 BRE 交替 `\|`**（静默 0 匹配）→ 多模式一律 `grep -E "a|b"`，或改用 Grep 工具（ripgrep）。
- **`Edit` 可能报成功但未落盘**（尤其同文件连续编辑 / 带前导空格的短行）→ 改完**回读核对**；反复不落盘改用 Python 脚本替换。
- 仓库级搜索用 Grep 工具（zsh 不支持 `--include`；沙箱 shell grep 常静默返回空）。
- 批量删文件一次 >50 个会被拦截 → 用 `rsync -a --delete`。

## 5. 部署陷阱

- ⚠️ **`docker/docker-compose.yml` 是 Admin.NET 原版部署，与本项目不符**（MySQL 5.7、`.NET 9` 路径、tdengine、minio、Redis 密码 `123456`、MySQL root/root）。**不要用它起本项目**。本地数据库用 `docker/docker-compose.pg.yml`（PostgreSQL 16，127.0.0.1:55432，`trust`）。
- 生产：**必须 `TZ=Asia/Shanghai`**；PG 改 `scram-sha-256` + 独立密钥管理（本地是 `trust`，仅开发用）。
- `docker/nginx/conf/nginx.conf` 已配 443 ssl 并反代 `adminNet:5005`，但证书是占位 `abc.admin.com.pem`，上线前必须换真实证书。

## 6. 支付系统安全红线 / 已知残余风险（评审结论，2026-09-18）

**已守住、不要回退的**（`pay_schema_guard.py` 有对应断言）：跨调用方幂等隔离、`status` 归属校验、密钥 getter 脱敏 + 停用即失效、签名 `FixedTimeEquals`、scope fail-closed、认证失败 / 资金动作双线审计、`(clientid, voucherno)` 通知去重、阻塞式 `FOR UPDATE` 不超发。

**仍存在的风险 / 待办**：

1. **F6.3 全链路 HTTPS 未在应用层强制**：本地与 `dev-up.sh` 都是 http；生产靠 nginx 443 终结 TLS。需确认 HTTP→HTTPS 跳转 / HSTS 与真实证书。
2. **F6.5 告警不足**：只有 `OnChallenge` 的 Warning 文本日志 + `/api/pay/*` 限流；**无失败次数阈值告警**（设计明确不在本期）。上线前建议补告警规则。
3. **F6.4 账号信息有意不脱敏**（决策 #1：调用方必须拿完整账号才能收款）→ 保护完全依赖签名鉴权 + 后台 RBAC。**日志 / 事件流水只落 AccountId**；任何把 `accountInfo` 写进日志/审计的改动都是安全回归。
4. **三方（银行 / 渠道）联调未做**；`accessKey/secretKey` 的下发与轮换流程未定。
5. **非超管角色的菜单拦截未被回归覆盖**（超管免授权，探针用超管身份）；需人工建普通角色验收。
6. **无 git 历史 → 不可回滚**：初始化 git 并推送到 `git@github.com:midxplore/PaymentCenter.git` 是当前最高优先级的工程风险。
7. 本地 PG `trust` 认证 + 仅绑 127.0.0.1（开发专用，勿照搬生产）。
8. 权限黑名单模型下，**漏挂按钮权限 = 对任何已登录用户开放**；守卫 §8 有断言，但新增接口仍需人补种子。
9. 金额精度纠偏（scale 变小）会改写已有数据 → 执行 `paycenter-schema.sql` 前先备份。
