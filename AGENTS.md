# AGENTS.md — PaymentCenter（收款账号分配系统）

> 面向在本仓库工作的 AI agent。只写「不看就会写错」的内容；为什么/实测数据见
> `.workbuddy-ai/memory/PROJECT-NOTES.md`，本文件不复述。
> 核心心法：**本项目的坑几乎都是「静默」型** —— 不报错、测试全绿，但结果是错的。

## 0. 项目与结构

- **是什么**：Admin.NET.Pro 裁剪版（.NET 8 + Furion.Pure 4.9.7.221 + SqlSugar），只保留一个业务模块 **PayCenter**（需求 F1~F7）。前端 Vue3 + Element Plus + Vite 7。
- **后端**：`Admin.NET/Admin.NET.sln`（Core / Application / Web.Core / Web.Entry / Test）。
  - 业务代码只放 `Admin.NET.Application/PayCenter/`（Controllers / Entity / Dto / Service / Auth / Job / SeedData / Const / Enum）。
  - ★ **`PayCenter/Controllers/` 只放对外接口**（`/api/pay/*`，签名鉴权 + `[PayScope]`）；后台管理接口留在各自的 `*Service`（JWT + RBAC）。
    控制器**只做 HTTP 入口**（鉴权特性 + 绑定 + 委托），业务逻辑留在 Service —— 单测与 `PayAbnormalService` 是**直接调用服务方法**的，
    把逻辑搬进控制器会让那些路径失去覆盖。因此 `PayAllocateService` / `PayNotifyService` **不实现** `IDynamicApiController`（守卫 §7c 会断言这一点）。
  - 裁剪现状：Application 层只剩框架 `Service/App/Auth` + PayCenter；**不要新增项目**，不要假设 Admin.NET 的示例业务模块还在。Core 是完整框架（Auth/Cache/Job/Menu/OpenAccess/Tenant/User…），可复用。
- **前端**：`Web/src/views/` 同时有框架页面（`system/*`、`login`、`home`、`about`）与本项目页面（`paycenter/*`）。框架页面基本不改；`src/views/system/openAccess/` 是本项目改造过的（密钥脱敏 / 停用 / scopes）。
- **脚本 / 文档**：`scripts/`、`doc/` 只在本机（**gitignore，不入库**）。改契约、跑守卫/回归、查部署手册仍用本地副本；克隆后需自备。
- **记忆**：`.workbuddy-ai/memory/MEMORY.md`（热规则）+ `PROJECT-NOTES.md`（为什么）；新增写 NOTES，勿堆日流水账。
- **Git**：`main` → `origin/main`（`git@github.com:midxplore/PaymentCenter.git`）。
- **协作方式**：AI 在本仓库改代码；用户本地/服务器验证。能跑 + 可观察优先于改得多。

## 1. 唯一事实来源（冲突时以此为准）

1. **本文件 `AGENTS.md`** —— Agent 硬约束（入库）。
2. **本地** `doc/收款账号分配系统-技术设计方案.md` —— 行为与接口契约（改实现前先读、改完更新；不入库）。
3. **本地** `scripts/paycenter-schema.sql` + `scripts/pay_schema_guard.py` —— schema / 源码不变式（不入库，改实体必跑守卫）。
4. `.workbuddy-ai/memory/MEMORY.md` + `PROJECT-NOTES.md` —— 热规则与长篇为什么（不入库）。
5. **本地** `doc/生产部署.md` / `doc/本地开发环境.md` —— 上线与本机怎么跑。
6. 更早一版参考：`/Users/Working/Project/Agent/Payment-Center`（相关功能先读，勿从零写）。

## 2. 命令（路径都不在 PATH，照抄）

| 事项 | 命令 |
|---|---|
| 起环境（库→构建→后端→schema 纠偏→守卫） | `scripts/dev-up.sh`（后端 :5005，日志 `Admin.NET/Admin.NET.Web.Entry/logs/dev-backend.log`） |
| 停环境 | `scripts/dev-down.sh`（`--with-db` 连库停；`--purge-db` 删卷） |
| 前端 dev | `cd Web && env -u NODE_OPTIONS npm run dev`（:8888） |
| 构建 | `/usr/local/share/dotnet/dotnet build Admin.NET/Admin.NET.sln`（基线 0 warning / 0 error） |
| 单元测试（走真实 PG，**约 8 分钟**） | `/usr/local/share/dotnet/dotnet test Admin.NET/Admin.NET.Test/Admin.NET.Test.csproj`（宿主自行设 `ASPNETCORE_ENVIRONMENT=Development`） |
| schema 守卫（改实体/DTO 必跑） | `~/.workbuddy-ai/binaries/python/envs/default/bin/python scripts/pay_schema_guard.py` |
| 全量 HTTP 回归 | `scripts/regress-all.sh --with-login`（自动关验证码 → **串行**跑回归脚本 → 保证还原 → 守卫自验；退出码 2 = 前置不满足，**不是通过**） |
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
- **额度预占必须阻塞式 `FOR UPDATE`，禁用 `SKIP LOCKED`**。`LIMIT 1` + `SKIP LOCKED` 会把「等不到锁」错报成 `API_ACCOUNT_UNAVAILABLE 无可用收款账号` → 三方不重试 → **丢单**（实测 10 并发只成功 2 笔）。等到锁后外层 WHERE 重新求值额度。
- 额度语义：`剩余 = Total − Used − Locked`（**不落库**）；锁定按**请求金额**释放，已用按**实际到账**累加。
- **过期与到账的竞态：先条件抢占订单状态，再读到账金额**（顺序反了会按偏小旧值结转 → 账实不符）。

### 鉴权 / 审计
- 调用方身份只有一个来源 `PayCallerContext.RequireClientId`，**禁止 `?? 0` 兜底**（退化成 0 会让所有接入方共享去重命名空间 → A 的凭证号静默去重掉 B 的到账通知）。
- 幂等键唯一索引是 **`(clientid, externalno)` 复合**，查询必须带 `ClientId`（幂等查询只有一处实现）。
- `notify` 刻意**不**按 ClientId 过滤（渠道不建单）；`allocate` / `status` 必须隔离。`status` 的「查无此单」与「不是你的单」返回同一个 `API_ORDER_NOT_FOUND`。
- 对外路由 `/api/pay/{allocate,notify,status}` 必须 `[Authorize(Signature)] + [PayScope(...)]`；新增 `/api/pay*` 接口忘写 `[PayScope]` 会被 `ScopeUnclassified` fail-closed 拒绝（**不要**改回返回 null）。
- **密钥出参必须在 getter 层脱敏**（掩码 `****`）；**「掩码 = 不改」**；改 accessKey 要**同时清新旧两个缓存**；停用凭证返回空密钥（与「无效 key」不可区分）。签名比较用 `FixedTimeEquals`。
- **审计两条线**：认证失败（401 在 MVC 之前，`SysLogOp` 采不到）走 `IOpenAccessAuditSink` → `pay_audit_log(action=ApiAuthFailure)`，**必须吞掉所有异常、只记带 accessKey 的失败、写入前按列宽截断**；资金动作走 `PayAuditService.WriteOpenApiAsync`（**同事务**），只读的 `status` 不审计。
- `pay_audit_log` / `pay_order_event` **只增不改**，无 update/delete 接口。

### 对外接口契约（改动前必读，`doc/对外接口API文档.md` 与设计文档 §7 必须同步）
- ★★ **所有响应（含失败）HTTP 传输状态都是 `200`**，真实结果在 JSON `code` 里（认证失败 `code=401`、冲突 `409`、参数错 `400` 全是 HTTP 200，已用 curl 独立验证）。改接口时**不要**试图去调 HTTP 状态码；文档里写「HTTP 401」是错的。
- ★★ **对外金额一律是十进制字符串，且固定两位小数**（`AmountStringConverter`：`1.2` → `"1.20"`、`100` → `"100.00"`；入参兼容数字）。格式串由 `PayConst.AmountScale` 推导，**不要写死字面量**。JSON 数字会被接入方用双精度浮点解析（仅 15~17 位有效数字）而**静默失真**；固定位数则是为了对账时能逐笔文本比对。
- ★★ **对外枚举一律回名称**（`EnumNameConverter`，`status`/`orderStatus` → `Pending/Partial/Completed/Expired`）。数字仅供后台管理界面用。**转换器只挂对外 DTO，不挂后台 DTO**（后者与前端下拉契约绑定）。
- ★★ **超精度金额必须拒绝，不能交给数据库舍入**（`PayConst.HasExcessScale`，`AmountScale=2` 与列 scale 联动）。否则提交 `1.005` 存成 `1.01` 而**响应回显 `1.005`** → 两边金额静默对不上（已实测）。
- ★★ **幂等冲突必须显式报 409**（`EnsureIdempotentConsistentAsync` 比对 `type`+`amount`）。只查 `externalNo` 存在性会让「改了金额重试」静默拿到旧金额订单 —— 与「钱进错账户」同类。
- **错误码前缀是 `API_*`**（`API_ACCOUNT_UNAVAILABLE` / `API_ORDER_NOT_FOUND` / `API_ORDER_REQUEST_CONFLICT` / `API_AMOUNT_INVALID` / `API_NOTIFY_INVALID`），定义在 `Core/Enum/ErrorCodeEnum.cs`。**带 `{0}` 的错误码必须传格式化参数**，否则细节丢失。

### 时间基准
- **`TZ=Asia/Shanghai` 是硬前提**（全部时间列 `timestamp without time zone` + `DateTime.Now`）。不符时 `PayCenterStartup` 启动期**直接拒绝启动**；容器 / systemd 都要设。漏设会让「历史行 +08 / 新行 UTC」混进同一列且不报错。
- 判定不能按时区 Id（本机是 `PRC`），也不能用 `SupportsDaylightSavingTime`。
- 前端 `el-date-picker` 一律 `value-format="YYYY-MM-DD HH:mm:ss"`；**不要**为消类型错改成 `new Date(...)`（会整体偏 8 小时）。构造收在 `views/paycenter/utils/exportParams.ts`。

### 数据库 / schema
- `pay_*` 继承 `EntityBase`，**不开多租户**（账号池公司级共享）。
- **CodeFirst 会建表建索引，但不改已有列宽/类型、不删旧索引。** 改列宽/索引要三处联动：① `PayConst.cs` 常量（唯一来源）② `scripts/paycenter-schema.sql` ③ DTO `[MaxLength(...)]` **引用常量**。改索引必须 `DROP INDEX IF EXISTS 旧名;` 再建新名。⚠️ 精度 scale 变小会四舍五入已有数据，先备份。
- ★★ **配置只有一处目录：`Admin.NET.Application/Configuration/`**（不要再加旁路覆盖目录 ——
  历史上的 `ConfigurationLocal/LocalOverride.json` 已按「结合框架能力、删多余内容」的要求**整体删除**）。
  目录内**允许**用框架自带的「按环境分文件」（`App.Development.json` / `HttpRemote.Development.json` 一直在用）：
  - `Database.json` = **基线**：PostgreSQL + **占位地址** + `EnableInitDb/InitTable/InitSeed` **全 false**（安全默认）
  - `Database.Development.json` = **本地开发**：覆盖 `ConnectionString` 指向开发库 + 三个初始化开关置 true
  - **切库 = 改 `Database.Development.json` 的 `ConnectionString`**（开发）或基线 `Database.json`（部署），然后重新 build。
  - 两个文件都**必须自带 `DbType`/`ConnectionConfigs` 结构**：Furion 与 `db_target.py` 都是
    「逐文件取 `ConnectionConfigs[0]` 再合并」，不是按字段跨文件合并。
  - 为什么基线不写真实地址：发布包随 `Database.json` 一起分发，基线放占位地址 = **发布包里不含开发库连接串**；
    且生产不会因为忘关开关而每次启动全表 CodeFirst。
  - ⚠️ 环境不是 `Development` 时拿到的是「占位地址 + 初始化关闭」：**连不上会明确报错**，
    而不是静默落到另一个库。
- ★ **Furion 读的是 `bin/<cfg>/` 里的副本**，改完源文件必须重新 build。`Configuration` 目录必须存在
  （缺 → 主机构建期抛 `DirectoryNotFoundException` → 「进程秒退、`logs/` 空」）。
- ★ `appsettings.Development.json` **故意不写** `ConfigurationScanDirectories`（扫描列表只由
  `appsettings.json` 一处定义）；不要「顺手对齐」两份配置 —— .NET 对**数组按下标合并**，
  两份写法不一致时会「靠副作用成立」，极难归因。
- ★★ **脚本要连库，一律走 `scripts/db_target.py`，禁止自己写死。** 它是「后端实际连哪个库」的
  **唯一解析实现**（`--json` / `--shell` / `--require-pg`；优先级：`PAY_PG_*` 环境变量 >
  `Database.<ENV>.json` > `Database.json`；`<ENV>` 取 `PAY_ENV` → `ASPNETCORE_ENVIRONMENT` → `Development`）。
  `dev-up.sh` 与 `_pay_common.py` 都从它取库 —— 历史上这些脚本各自硬编码
  `127.0.0.1:55432/paymentcenter`，**后端一旦切库，schema 纠偏 / 守卫 / 回归造数会安静地打在另一个库上，
  而输出照样「全绿」**（本项目最忌讳的静默失效）。临时指向别处用 `PAY_PG_HOST/PORT/USER/DB/PASSWORD`。
- ⚠️ 纠正一条旧说法：**`Configuration/Database*.json` 是可以读的**（`wc -c`、python 均正常）。
  会触发沙箱 `SIGTERM (exit 137)` 的是**递归扫描跨过该目录**（如从仓库根 `grep -rln ... .`）。
  要确认连的是哪个库，首选 `python scripts/db_target.py`；`dev-up.sh` 另有第二道断言
  （启动日志的 `初始化数据库 …` 必须是 `PostgreSQL`，且输出前**给口令打码**）。
- ★ **首次对空库启动很慢**：CodeFirst 逐张建表（远端库单张 3~25s，45 张要 4 分钟以上）。
  `dev-up.sh` 的就绪上限 `READY_MAX` 默认 **1800s**，等待期间每 15s 打印 `已建表 N/45`。
  **不要把这段等待误判成「启动失败」** —— 这也是旧脚本 90s 超时误报的原因。
- ★★ **单元测试宿主必须跑在 Development 环境**（`Admin.NET.Test/TestProgram.cs` 在 `Serve.RunNative()`
  **之前**设 `ASPNETCORE_ENVIRONMENT`）。不设 → .NET 回落 Production → `App.Development.json` /
  `HttpRemote.Development.json` 不叠加（例如 `JobSchedule.Enabled` 只在 Development 为 true）
  → 测试与后端跑在两套配置上，症状是「本地能跑、测试却失败」。
- ★★ **自检断言必须落在「库里有表 / 表名对得上」这类可观察事实上**，不能只断言「服务非空」：
  宿主连到一个**空库**时 `App.GetService<ISqlSugarClient>()` 依然**非空**，只断言「非空」会
  **安静通过**（假绿，曾被掩盖一整轮）。代价：`dotnet test` 每次都对远端库跑一遍 CodeFirst
  差异比对（45 张表，约 7~8 分钟）。
- **本项目只有 PostgreSQL 一种形态**（`FOR UPDATE` 行锁语义其它库没有，并发结论不可迁移）。
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
- **对 `Admin.NET.Web.Entry/logs/dev-backend.log` 禁用 `tail`**（报 `Operation not permitted`），
  而同一个文件 `grep -a` / `grep -ac` / `wc -l` / python 读取都正常。
  → 需要看日志尾部时用 python 读（`dev-up.sh` 的 `show_log_tail()` 就是这么做的），**不要用 `tail`**：
  否则最需要信息的失败路径会什么都不显示。另：该日志的「初始化数据库 …」那行是**明文带 `PASSWORD=`** 的，
  任何回显前先打码。
- **`Edit` 可能报成功但未落盘**（尤其同文件连续编辑 / 带前导空格的短行）→ 改完**回读核对**；反复不落盘改用 Python 脚本替换。
- 仓库级搜索用 Grep 工具（zsh 不支持 `--include`；沙箱 shell grep 常静默返回空）。
- 批量删文件一次 >50 个会被拦截 → 用 `rsync -a --delete`。
- ★ **不要在脚本运行期间编辑该脚本**：bash 是**增量读取**脚本文件的，运行中改长度会让它的读偏移错位，
  报出**假的** `unexpected EOF while looking for matching '`（而 `bash -n` 却通过，因为文件本身没坏）。
  要改就等它跑完，或先 `cp` 一份再跑副本。

## 5. 部署陷阱

- **框架自带的部署脚手架已删除**：`docker-compose.yml`、`docker-compose-builder.yml`、`build.sh`、`app/`、`mysql/`、`nginx/`、`.env.production`。它们是 Admin.NET 原版部署（MySQL 5.7、`.NET 9` 路径、tdengine、minio、Redis 密码 `123456`、MySQL root/root），与本项目（net8.0 + PostgreSQL）不符；`build.sh` 还会用 `docker/.env.production` **覆盖 `Web/.env.production`**。需要时从 git 历史取回。
- 本地数据库仍可用 `docker/docker-compose.pg.yml`（PostgreSQL 16，`127.0.0.1:55432`，`trust`）；但**连库的配置只有一处目录**：`Admin.NET.Application/Configuration/` —— 基线 `Database.json`（占位地址 + 初始化关闭）与 `Database.Development.json`（本地开发，指向当前远程开发库），见 §3。
- 生产：**必须 `TZ=Asia/Shanghai`**；PG 改 `scram-sha-256` + 独立密钥管理（本地是 `trust`，仅开发用）；TLS 需自行终结（原 nginx 示例配置与占位证书已随脚手架删除），上线前必须用真实证书。

## 6. 支付系统安全红线 / 残余风险

**不要回退**（`pay_schema_guard.py` 有断言）：跨调用方幂等隔离、`status` 归属校验、密钥 getter 脱敏 + 停用即失效、签名 `FixedTimeEquals`、scope fail-closed、认证失败 / 资金动作双线审计、`(clientid, voucherno)` 通知去重、阻塞式 `FOR UPDATE` 不超发。

**仍须注意**：

1. **HTTPS**：应用层不强制；生产靠 nginx 443。勿把 5005 暴露公网。
2. **告警**：仅有 `OnChallenge` Warning + `/api/pay/*` 限流；无失败次数阈值告警（本期不做）。
3. **账号不脱敏**（决策）：保护依赖签名 + RBAC；**日志/审计只落 AccountId**，勿写 `accountInfo` / 图片路径。
4. **非超管菜单**：超管免授权，普通角色拦截需人工验收。
5. 本地 PG `trust` + 仅绑 127.0.0.1（勿照搬生产）。
6. 权限黑名单：漏挂按钮权限 = 对任何已登录用户开放；新增接口须补种子。
7. 金额精度 scale 变小会改写已有数据 → 执行 `paycenter-schema.sql` 前先备份。
- **隐藏菜单必须「整棵子树都标 `IsHide=true`」**：侧边栏 `aside.vue` 的 `filterRoutesFun` 是**递归**过滤（隐藏目录即隐藏子树），但顶部**菜单搜索**用的是 `formatFlatteningRoutes` 拍平后的**一维**列表、逐项判 `isHide` —— 只隐藏目录会让子项**仍被搜到并能点进去**。且 `SysMenuSeedData` 带 `[IgnoreUpdateSeed]`，**改种子不会更新已有行**（实测：加 `IsHide=true` 后重启，活库 `ishide` 仍为 `false`，日志显示「更新 000 条」），现有库必须显式 `UPDATE sysmenu SET ishide=true WHERE id IN (...)`。守卫 §6b 断言「种子标隐藏的菜单活库也隐藏」+「隐藏项的子项也隐藏」。
- **凭证失效有三个开关**：停用凭证、**停用绑定用户**（`OnValidated` 校验 `BindUser.Status`）、重新生成密钥。三者对外**统一报「accessKey 无效」**（与不存在不可区分）。★ 校验绑定用户必须走 `GetBindUserAsync`（**每次读库**）——`GetByKey` 缓存长期不过期（实测 20s 仍是旧值），用缓存快照判启用会让「停用用户」**静默不生效**。
- **鉴权链路上任何「可能为 null 的关联对象」都必须判空**：原先 `openAccess.BindUser.Account` 在用户不存在时抛 NRE，而鉴权中间件里的 NRE 会变成 **HTTP 500 + 堆栈**（响应体含服务器文件路径、非 JSON）。
