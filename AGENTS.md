# AGENTS.md — PaymentCenter

> Agent 硬约束。细节/为什么 → `.workbuddy-ai/memory/` + 本地 `doc/`。  
> **心法：坑多为静默型**（不报错、测试绿，结果错）。

## 项目

**做什么**：收款账号池按「商家单号 + 类型 + 金额」匹配并预占额度 → 生成订单；收银台提交凭证即 `Completed` + Webhook；异常/补录走后台确认。账号分普通（非过期单≤2）/大额（不限次数），匹配只锁额度不置占用挡路。开放面只有 `allocate` / `status`（HMAC）。产品名「PulsePay」。

**怎么铺**：Admin.NET.Pro 裁剪（.NET 8 + Furion.Pure + SqlSugar + PG）；业务只在 `Admin.NET.Application/PayCenter/`；前端 Vue3 + Element Plus（`Web/`）。`doc/`、`scripts/` **gitignore，本机自备**。Git：`main` → `origin/main`。

**事实来源（冲突时）**：本文件 → README → `doc/设计方案` → `scripts` 守卫/schema → MEMORY/NOTES → `doc/生产部署` / `本地开发环境`。

## 命令（路径照抄）

| 事 | 命令 |
|---|---|
| 起/停 | `scripts/dev-up.sh` / `dev-down.sh`（后端 :5005） |
| 构建 | `/usr/local/share/dotnet/dotnet build Admin.NET/Admin.NET.sln` |
| 单测 | 同上 `dotnet test …Admin.NET.Test.csproj`（~8min，宿主须 Development） |
| 守卫 | `~/.workbuddy-ai/binaries/python/envs/default/bin/python scripts/pay_schema_guard.py` |
| 回归 | `scripts/regress-all.sh --with-login`（串行；退出码 2≠通过） |
| 前端 | `cd Web && env -u NODE_OPTIONS npm run typecheck`（闸门 0 错）/ `npm run build` / `npm run dev`(:8888) |
| 发布 | `scripts/publish-release.sh` → `/Users/Working/Agent/Publish/PaymentCenter/` |

dotnet/python/docker 不在默认 PATH；npm **必须** `env -u NODE_OPTIONS`。端口：5005 / 8888 / PG 55432。改业务代码先 **build 再重启**（`--no-build` 跑旧二进制）。

## 硬约束（违反会静默错）

**额度**：比较「列在左」`Total >= Used+Locked+amount`；预占 **阻塞 `FOR UPDATE`，禁 `SKIP LOCKED`**；过期/到账先抢状态再读金额。匹配**不再**依赖 `InUse`（锁后 Enabled/Exhausted）；普通账号非过期订单数 `< 2`，大额不限。`MinAmount>0` 时请求金额须 ≥ 最小金额，并优先选高门槛码。过期重传须重锁额度，失败进异常台账。

**账号备注 / 收银台 Notice**：后台 wangEditor 写受限 HTML；存库与出库 `PayHtmlSanitize`；列宽三处联动 `PayConst.AccountRemarkLength`（2000）+ schema + DTO MaxLength。

**鉴权**：身份只来自 `PayCallerContext.RequireClientId`（禁 `?? 0`）；幂等 `(clientid,externalno)`；`allocate`/`status` 按 ClientId 隔离且挂 `[Authorize(Signature)]+[PayScope]`；scope 未标 → fail-closed。  
**`/api/pay/notify` 已删** → 常规到账走收银台提交；后台 `confirmReceived` 仅异常/补录；`PayNotifyService` 仅内部/单测，勿挂回控制器。  
HMAC：`METHOD&PATH&ak&ts&nonce&BODY_SHA256_HEX`（对实发字节；`EnableBuffering` 后复位）。密钥 getter 脱敏；「掩码=不改」；`FixedTimeEquals`。绑定用户每次 `GetBindUserAsync` 读库。审计：认证失败 → `IOpenAccessAuditSink`；资金 → `WriteOpenApiAsync` 同事务；`status` 不审。

**对外契约**（改前读 `doc/对外接口API文档.md` + 设计 §7）：开放面只有 allocate/status + Webhook；**HTTP 一律 200**，结果看 JSON `code`；金额字符串固定 2 位；枚举回名称（仅对外 DTO）；超精度拒；幂等冲突 409。`allocate` 可选 `returnUrl`/`returnLabel`（落库订单，收银台「返回」按钮；幂等以首次为准）。`Controllers/` 只放对外薄入口，逻辑在 Service（勿给 Allocate/Notify Service 挂 `IDynamicApiController`）。  
★ **`doc/对外接口API文档.md` 面向接入方用户**：只写对外契约（怎么调、字段、错误码、标准 `type`）；**不要**写实现细节、字典表名、内部路径、运维/种子说明。内部怎么配 → `开放接口凭证操作手册` / 设计文档。改对外文档时先尊重用户本地已改口径，勿回填冗长说明。

**时间**：`TZ=Asia/Shanghai` 否则拒启；前端日期 `value-format="YYYY-MM-DD HH:mm:ss"`。

**库/配置**：仅 PG；`pay_*` 不开多租户；改列宽/索引三处联动（`PayConst` + schema.sql + DTO MaxLength）。配置只在 `Application/Configuration/`：基线 `Database.json`（占位+Init 关）+ `Database.Development.json`（本地库）；Furion 读 `bin/` 副本须 rebuild。脚本连库只走 `scripts/db_target.py`。生产：`Urls=127.0.0.1:5005`、关 Swagger、日志 Warning；覆盖配置排除 `Database.json`；排障 `api/logs/` + `journalctl`（不必 run.log）→ `doc/生产部署.md`。`Upload.json` **勿**放回 `image/svg+xml` / APK / 纯文本；上传走 `FileContentProbe` 魔数。收款码 `UploadQr`：前端 `compressQr` + 后端 JPEG 限边（`PayConst.QrImageMaxSide`）；旧大图须后台重传。nginx `/upload/` 只读 + 缓存见生产部署 §2.8。

**DTO**：`[Required]` 勿挂值类型；入参平铺不继承实体；更新状态字段可空。

**前端**：菜单 `component` 唯一命中；`permission` 写路由尾部；权限黑名单→新接口必补按钮种子；typecheck 闸门含 `paycenter/` + `api-services/` + `openAccess/`。勿用 `IsHide` 藏菜单（超管也看不到）。

**沙箱**：探活用 `lsof` 不用 curl；`grep -E`；dev 日志禁 `tail`；勿在脚本运行中改该脚本。

**勿回退**（守卫有断言）：幂等隔离、status 归属、密钥脱敏、BODY_SHA256、scope fail-closed、双线审计、阻塞 FOR UPDATE。
