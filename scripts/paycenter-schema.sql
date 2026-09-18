-- ============================================================================================
-- 收款账号分配系统 —— 幂等 DDL 清单（PostgreSQL 16）
-- ============================================================================================
--
-- 用途
--   上线 / 换库 / 灾备恢复时，用一份**可重复执行**的 SQL 把 pay_* 相关库对象对齐到契约。
--
-- 为什么需要这个文件（本项目的 CodeFirst 边界）
--   1. SqlSugar CodeFirst **会**建表、建索引、加列 —— 全新库其实什么都不用做，启动即自动创建。
--   2. SqlSugar CodeFirst **不会**改已有列的宽度 / 类型（`ALTER TABLE ... ALTER COLUMN` 不在它的能力范围）。
--      这正是本项目踩过的坑：`orderno` 的列宽与入参 `[MaxLength]` 曾长期脱节，
--      且「当前值刚好卡在上限」所以测试全绿，等位数增长后才炸。
--   3. 所以本文件的价值有两块：
--      (a) 把**期望 schema 写成可执行的契约**，而不是散落在实体类的特性里；
--      (b) 补上 CodeFirst 不做的**列宽/类型纠偏**（§3）。
--
-- 幂等性
--   · CREATE TABLE IF NOT EXISTS / CREATE INDEX IF NOT EXISTS / ADD COLUMN IF NOT EXISTS
--   · 列宽纠偏用 DO 块**先查后改**，已达标则完全不动（不产生 rewrite，不加锁）
--   · COMMENT 本身幂等
--   → 本文件可任意次重复执行，结果一致；也适用于「库已存在、只想补齐差异」的增量场景。
--
-- 不在本文件维护的对象（由**启动种子**维护，见下表）
--   SqlSugar 的 [SeedData] / [IncreSeed] 在应用启动时幂等写入，**不要**在 SQL 里重复造，
--   否则会与种子的 Id 主键冲突。清单见 §4。
--
-- 事实依据
--   本文件的列定义/索引与 `paymentcenter` 库 `pg_dump --schema-only` 逐字核对过（2026-09-17）。
--   若改动了实体，请同步改这里，并跑 `scripts/pay_schema_guard.py` 校验活库是否漂移。
-- ============================================================================================


-- --------------------------------------------------------------------------------------------
-- §1 表结构
-- --------------------------------------------------------------------------------------------

-- 1.1 收款账号表（F1）
CREATE TABLE IF NOT EXISTS pay_account (
    id             bigint        NOT NULL,
    type           varchar(32)   NOT NULL,          -- 收款类型（字典 pay_account_type）
    accountinfo    varchar(512)  NOT NULL,          -- 账号信息（按决策不做脱敏）
    totalquota     numeric(18,2) NOT NULL,          -- 总额度
    usedquota      numeric(18,2) NOT NULL,          -- 已用额度
    lockedquota    numeric(18,2) NOT NULL,          -- 锁定额度（预占）
    status         integer       NOT NULL,          -- 账号状态（PayAccountStatusEnum）
    remark         varchar(256),
    createtime     timestamp,
    updatetime     timestamp,
    createuserid   bigint,
    createusername varchar(64),
    updateuserid   bigint,
    updateusername varchar(64),
    isdelete       boolean       NOT NULL,
    CONSTRAINT pay_account_pkey PRIMARY KEY (id)
);

-- 1.2 收款订单表（F2/F3）
CREATE TABLE IF NOT EXISTS pay_order (
    id             bigint        NOT NULL,
    orderno        varchar(64)   NOT NULL,          -- 系统订单号 = 时间戳(17) + 雪花尾段
    externalno     varchar(64),                     -- 外部业务单号（幂等键的一半，见 §2 的 u_pay_order_ce）
    requestamount  numeric(18,2) NOT NULL,          -- 请求金额
    accountid      bigint        NOT NULL,          -- 匹配到的收款账号
    receivedamount numeric(18,2) NOT NULL,          -- 累计到账金额
    status         integer       NOT NULL,          -- 订单状态（PayOrderStatusEnum）
    expiretime     timestamp     NOT NULL,          -- 过期时间
    completetime   timestamp,                       -- 完成时间
    overpayremark  varchar(256),                    -- 超额到账备注（F4.4）
    clientid       bigint,                          -- 调用方（F6.3 隔离）
    createtime     timestamp,
    updatetime     timestamp,
    createuserid   bigint,
    createusername varchar(64),
    updateuserid   bigint,
    updateusername varchar(64),
    isdelete       boolean       NOT NULL,
    CONSTRAINT pay_order_pkey PRIMARY KEY (id)
);

-- 1.3 订单事件流水表（F7.4 只增不改）
CREATE TABLE IF NOT EXISTS pay_order_event (
    id             bigint        NOT NULL,
    orderid        bigint        NOT NULL,
    orderno        varchar(64)   NOT NULL,
    eventtype      integer       NOT NULL,          -- 事件类型（PayEventTypeEnum）
    fromstatus     integer,                         -- 变更前状态
    tostatus       integer,                         -- 变更后状态
    amount         numeric(18,2) NOT NULL,          -- 本次金额
    receivedtotal  numeric(18,2) NOT NULL,          -- 累计到账
    remark         varchar(512),
    operatorid     bigint,
    operatorname   varchar(64),
    createtime     timestamp,
    updatetime     timestamp,
    createuserid   bigint,
    createusername varchar(64),
    updateuserid   bigint,
    updateusername varchar(64),
    isdelete       boolean       NOT NULL,
    CONSTRAINT pay_order_event_pkey PRIMARY KEY (id)
);

-- 1.4 到账通知记录表（F4.5 凭证去重）
CREATE TABLE IF NOT EXISTS pay_notify_record (
    id             bigint        NOT NULL,
    orderid        bigint        NOT NULL,
    orderno        varchar(64)   NOT NULL,
    amount         numeric(18,2) NOT NULL,
    notifytime     timestamp     NOT NULL,
    voucherno      varchar(64)   NOT NULL,          -- 凭证号（去重键）
    clientid       bigint        NOT NULL,          -- 通知方（去重键）
    rawbody        text,                            -- 原始报文（留证）
    applied        boolean       NOT NULL,          -- 是否已累加（先插后回填，天然抗并发）
    createtime     timestamp,
    updatetime     timestamp,
    createuserid   bigint,
    createusername varchar(64),
    updateuserid   bigint,
    updateusername varchar(64),
    isdelete       boolean       NOT NULL,
    CONSTRAINT pay_notify_record_pkey PRIMARY KEY (id)
);

-- 1.5 异常到账台账表（F5）
CREATE TABLE IF NOT EXISTS pay_abnormal_receipt (
    id             bigint        NOT NULL,
    amount         numeric(18,2) NOT NULL,
    notifytime     timestamp     NOT NULL,
    voucherno      varchar(64)   NOT NULL,
    clientid       bigint        NOT NULL,
    reportorderno  varchar(64),                     -- 上报的订单号（可能不存在）
    rawbody        text,
    reason         integer       NOT NULL,          -- 异常原因（PayAbnormalReasonEnum）
    relatedorderid bigint,                          -- 人工关联到的订单 Id
    relatedorderno varchar(64),                     -- 人工关联到的订单号
    handlestatus   integer       NOT NULL,          -- 处理状态（PayHandleStatusEnum）
    handlerid      bigint,
    handlername    varchar(64),
    handletime     timestamp,
    handleremark   varchar(256),
    createtime     timestamp,
    updatetime     timestamp,
    createuserid   bigint,
    createusername varchar(64),
    updateuserid   bigint,
    updateusername varchar(64),
    isdelete       boolean       NOT NULL,
    CONSTRAINT pay_abnormal_receipt_pkey PRIMARY KEY (id)
);

-- 1.6 业务审计日志表（F7.3/F7.5，只增不改）
CREATE TABLE IF NOT EXISTS pay_audit_log (
    id             bigint        NOT NULL,
    action         integer       NOT NULL,          -- 动作（PayAuditActionEnum，含 Export / ApiCall / ApiAuthFailure）
    targettype     varchar(32)   NOT NULL,          -- 目标类型
    targetid       bigint        NOT NULL,
    targetno       varchar(64),                     -- 目标单号
    beforejson     text,
    afterjson      text,
    remark         varchar(512),
    operatorid     bigint        NOT NULL,
    operatorname   varchar(64),
    operatorip     varchar(64),
    clientid       bigint,                          -- 调用方（SysOpenAccess.Id），后台动作为 NULL
    clientkey      varchar(128),                    -- 调用方 AccessKey 原文（审计行自包含）
    createtime     timestamp,
    updatetime     timestamp,
    createuserid   bigint,
    createusername varchar(64),
    updateuserid   bigint,
    updateusername varchar(64),
    isdelete       boolean       NOT NULL,
    CONSTRAINT pay_audit_log_pkey PRIMARY KEY (id)
);


-- --------------------------------------------------------------------------------------------
-- §2 索引
-- --------------------------------------------------------------------------------------------
-- 命名约定：i_{table}_{缩写} = 普通索引；u_{table}_{缩写} = 唯一索引。

-- pay_account
CREATE INDEX        IF NOT EXISTS i_pay_account_tsc        ON pay_account (type, status, createtime);
CREATE INDEX        IF NOT EXISTS i_pay_account_ct         ON pay_account (createtime);

-- pay_order
CREATE UNIQUE INDEX IF NOT EXISTS u_pay_order_no           ON pay_order (orderno);

-- ★★ 迁移必读：旧的单列唯一索引必须显式删除 ★★
--   幂等键已从 `ExternalNo` 单列改为 `(ClientId, ExternalNo)` 组合。
--   原因：ExternalNo 是**调用方自己的业务单号**，只在他自己的命名空间里有意义。
--   单列唯一时，接入方 B 用了与 A 相同的单号会**命中 A 的订单**并把它（含收款账号明文）返回给 B，
--   而 B 自己的收款请求根本没被分配 —— 钱可能进错账户 + 跨调用方数据泄漏，且双方都收不到报错。
--
--   ⚠️ SqlSugar CodeFirst **只会新建 u_pay_order_ce，不会删掉 u_pay_order_en**。
--      旧索引残留会继续拒绝「不同调用方使用相同业务单号」，
--      表现为写库时抛唯一约束冲突 —— 而且是在代码改对之后才出现，极易被误判成新引入的 bug。
--      所以这一句 DROP 是**必须执行**的迁移步骤，不是可选清理。
--      scripts/pay_schema_guard.py 会断言 u_pay_order_en 已消失。
DROP INDEX IF EXISTS u_pay_order_en;
CREATE UNIQUE INDEX IF NOT EXISTS u_pay_order_ce           ON pay_order (clientid, externalno);

CREATE INDEX        IF NOT EXISTS i_pay_order_se           ON pay_order (status, expiretime);   -- 过期扫描
CREATE INDEX        IF NOT EXISTS i_pay_order_aid          ON pay_order (accountid);
CREATE INDEX        IF NOT EXISTS i_pay_order_ct           ON pay_order (createtime);

-- pay_order_event
CREATE INDEX        IF NOT EXISTS i_pay_order_event_oid    ON pay_order_event (orderid, createtime);
CREATE INDEX        IF NOT EXISTS i_pay_order_event_ct     ON pay_order_event (createtime);

-- pay_notify_record
CREATE UNIQUE INDEX IF NOT EXISTS u_pay_notify_record_cv   ON pay_notify_record (clientid, voucherno);  -- ★ 凭证去重
CREATE INDEX        IF NOT EXISTS i_pay_notify_record_oid  ON pay_notify_record (orderid);
CREATE INDEX        IF NOT EXISTS i_pay_notify_record_ct   ON pay_notify_record (createtime);

-- pay_abnormal_receipt
CREATE UNIQUE INDEX IF NOT EXISTS u_pay_abnormal_receipt_cv ON pay_abnormal_receipt (clientid, voucherno);
CREATE INDEX        IF NOT EXISTS i_pay_abnormal_receipt_hs ON pay_abnormal_receipt (handlestatus, createtime);
CREATE INDEX        IF NOT EXISTS i_pay_abnormal_receipt_ct ON pay_abnormal_receipt (createtime);

-- pay_audit_log
CREATE INDEX        IF NOT EXISTS i_pay_audit_log_target   ON pay_audit_log (targettype, targetid, createtime);
CREATE INDEX        IF NOT EXISTS i_pay_audit_log_action   ON pay_audit_log (action, createtime);
CREATE INDEX        IF NOT EXISTS i_pay_audit_log_ct       ON pay_audit_log (createtime);


-- --------------------------------------------------------------------------------------------
-- §3 列宽 / 类型纠偏 —— SqlSugar CodeFirst **不会**做这件事
-- --------------------------------------------------------------------------------------------
-- 场景：老库上列宽是旧值（如 orderno 曾是 varchar(32)），改了实体后 CodeFirst 不会 ALTER，
--       于是「代码能编译、单测能过、上线写库报错」。
-- 做法：先查 information_schema，只有**当前宽度 < 期望宽度**才 ALTER（加宽是元数据操作，
--       不重写表；已达标则完全不执行）。
--
-- ★ 为什么 orderno 必须是 64：订单号 = 时间戳(17) + 雪花尾段，当前 32 位，
--   雪花尾段约 2027-11 进位到 16 位后变 33 位。列宽一次留足（PayConst.OrderNoLength = 64）。
--   **同一常量必须同时用在实体列宽与入参 [MaxLength] 上**，两边分开写就会脱节。

DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            -- (表名, 列名, 期望 varchar 长度)
            ('pay_order',            'orderno',        64),
            ('pay_order',            'externalno',     64),
            ('pay_order',            'overpayremark',  256),
            ('pay_order_event',      'orderno',        64),
            ('pay_notify_record',    'orderno',        64),
            ('pay_notify_record',    'voucherno',      64),
            ('pay_abnormal_receipt', 'voucherno',      64),
            ('pay_abnormal_receipt', 'reportorderno',  64),
            ('pay_abnormal_receipt', 'relatedorderno', 64),
            ('pay_audit_log',        'targetno',       64),
            ('pay_audit_log',        'targettype',     32),
            ('pay_account',          'accountinfo',    512),
            ('pay_account',          'type',           32)
        ) AS t(tbl, col, want)
    LOOP
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
             WHERE table_schema = 'public'
               AND table_name   = r.tbl
               AND column_name  = r.col
               AND character_maximum_length IS NOT NULL
               AND character_maximum_length < r.want
        ) THEN
            EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE varchar(%s)', r.tbl, r.col, r.want);
            RAISE NOTICE '列宽纠偏：%.% → varchar(%)', r.tbl, r.col, r.want;
        END IF;
    END LOOP;
END $$;

-- 金额列统一 numeric(18,2)（单币种，见设计决策 #5）。若历史上建成了别的精度，一并纠正。
--
-- ⚠️ **数据安全**：若老库的 scale > 2（如 numeric(10,4)），`ALTER ... TYPE numeric(18,2)` 会
--    把已有值**四舍五入**到 2 位小数 —— 这是对**数据**的修改，不只是元数据。
--    加宽精度（18 > 10）是安全的，缩小 scale 不是。执行前请：
--      1) 备份；2) 先查 `select count(*) from <表> where <金额列> <> round(<金额列>,2);`
--    期望为 0（即本来就没有 2 位以上小数），否则先人工确认这些尾差怎么处理。
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('pay_account',          'totalquota'),
            ('pay_account',          'usedquota'),
            ('pay_account',          'lockedquota'),
            ('pay_order',            'requestamount'),
            ('pay_order',            'receivedamount'),
            ('pay_order_event',      'amount'),
            ('pay_order_event',      'receivedtotal'),
            ('pay_notify_record',    'amount'),
            ('pay_abnormal_receipt', 'amount')
        ) AS t(tbl, col)
    LOOP
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
             WHERE table_schema = 'public'
               AND table_name   = r.tbl
               AND column_name  = r.col
               AND (numeric_precision IS DISTINCT FROM 18 OR numeric_scale IS DISTINCT FROM 2)
        ) THEN
            EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE numeric(18,2)', r.tbl, r.col);
            RAISE NOTICE '金额精度纠偏：%.% → numeric(18,2)', r.tbl, r.col;
        END IF;
    END LOOP;
END $$;


-- --------------------------------------------------------------------------------------------
-- §3.5 新增列纠偏（老库补齐）—— CodeFirst 会加列，但这里显式写出来作为部署契约
-- --------------------------------------------------------------------------------------------
-- 为什么已经有 CodeFirst 还要写：本文件是「期望 schema 的可执行契约」，
-- 部署时应当**先跑本文件再启动应用**，这样即使应用因为别的原因起不来，
-- 库结构也已经是完整的（而不是「等应用起来了才补列」）。
-- 全部用 IF NOT EXISTS，可反复执行。

-- 3.5.1 pay_audit_log 的调用方字段（2026-09-17 补）
--   背景：开放接口的调用没有登录用户，只用 operatorid（SysUser.Id）无法表达
--   「这条审计是某个接入方调接口产生的」。clientid/clientkey 两个字段专门承载这件事。
--   两者属于**不同命名空间**（sysuser.id vs sysopenaccess.id），所以不能复用 operatorid。
--   clientkey 存 AccessKey 原文而非外键：认证失败时该 key 可能压根不在 sysopenaccess 里，
--   且凭证行被删除后外键会悬空 —— 审计行必须自包含。
ALTER TABLE pay_audit_log ADD COLUMN IF NOT EXISTS clientid  bigint;
ALTER TABLE pay_audit_log ADD COLUMN IF NOT EXISTS clientkey varchar(128);

-- 3.5.2 开放接口凭证的启停状态（2026-09-17 补）
--   ⚠️ sysopenaccess 是**框架表**（Admin.NET.Core），本模块依赖它但不拥有它。
--   放在这里的原因：本模块的资金类接口完全依赖「凭证可被停用」这一能力
--   （密钥泄漏时若不删行就无法吊销；删行又会让 pay_order.clientid / pay_audit_log.clientid 悬空），
--   所以它属于本模块的部署前置条件，必须在部署清单里显式可见。
--   默认 1 = StatusEnum.Enable，与实体默认值一致；老库既有凭证全部视为启用（保持现状，不误伤）。
ALTER TABLE sysopenaccess ADD COLUMN IF NOT EXISTS status integer NOT NULL DEFAULT 1;

-- ★ 上面那条在**列已存在**时是空操作，而 SqlSugar CodeFirst 建这张表时**不带 DB 默认值**
--   （实体里 `public virtual StatusEnum Status { get; set; } = StatusEnum.Enable;` 是 **C# 字段
--   初始化器**，只在应用内 new 对象时生效，不会变成列默认值）。
--   于是存在一条静默路径：**全新库 → CodeFirst 先建出无默认值的 status → 本语句空操作** →
--   列永远没有默认值，而 `pay_schema_guard.py` 的断言「status 默认 1」就会失败。
--   本地容器之所以一直是好的，只是因为当初那列是由**上面这条语句**新增的（而不是 CodeFirst）。
--   2026-09-18 在一台全新库上实测到，故补一个「先查后改」的 DO 块。
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema  = 'public'
           AND table_name    = 'sysopenaccess'
           AND column_name   = 'status'
           AND column_default IS NULL
    ) THEN
        ALTER TABLE sysopenaccess ALTER COLUMN status SET DEFAULT 1;
        RAISE NOTICE '列默认值纠偏：sysopenaccess.status → DEFAULT 1';
    END IF;
END $$;


-- --------------------------------------------------------------------------------------------
-- §4 由启动种子维护的对象（**不要**在此文件里重复插入）
-- --------------------------------------------------------------------------------------------
-- 种子走 SqlSugar 的 [SeedData] / [IncreSeed]，应用启动时幂等写入，且 Id 是固定值。
-- 在 SQL 里再插一遍会主键冲突；只在这里登记清单，便于核对「库里的种子是否齐」。
--
-- | 对象                | 落点                                     | Id / 标识 |
-- |---------------------|------------------------------------------|-----------|
-- | 收款类型字典类型    | PaySeedData.cs → PayDictTypeSeedData     | 1300000005101，Code = pay_account_type |
-- | 收款类型字典值 ×4   | PaySeedData.cs → PayDictDataSeedData     | 1300000005101~104（wxpay / alipay / bank / cloudquickpass） |
-- | 订单过期时长配置    | PaySeedData.cs → PayConfigSeedData       | 1300000005201，Code = pay_order_expire_minutes |
-- | 后台菜单 ×25        | PayMenuSeedData.cs                       | 1300000000201~227（4 个页面 + 21 个按钮） |
-- | 开放接口按钮 ×2     | **Core** SeedData/SysMenuSeedData.cs     | 1310000000426（生成密钥）/ 1310000000427（生成签名） |
-- | 过期扫描作业+触发器 | PayOrderExpireJob.cs（[JobDetail]/[Minutely]） | pay_order_expire_job / pay_order_expire_trigger |
--
-- 核对种子是否落库：
--   SELECT count(*) FROM sysdictdata WHERE dicttypeid = 1300000005101;              -- 期望 4
--   SELECT count(*) FROM sysconfig   WHERE code = 'pay_order_expire_minutes';       -- 期望 1
--   SELECT count(*) FROM sysmenu     WHERE id BETWEEN 1300000000201 AND 1300000000227; -- 期望 25
--   SELECT count(*) FROM sysmenu     WHERE id IN (1310000000426, 1310000000427);    -- 期望 2
--   SELECT * FROM sysjobdetail WHERE jobid = 'pay_order_expire_job';                -- 期望 1 行
--   SELECT * FROM sysjobtrigger WHERE jobid = 'pay_order_expire_job';               -- 期望 1 行（minutely）
--
-- ⚠️ 2026-09-17 追加 4 个按钮（1300000000219 / 225 / 226 / 227）：typeOptions、detailByNo、
--    statusOptions、exportNotify 原先**没有任何按钮权限**。其中两个是真敞口：
--      · payOrder/detailByNo —— 按订单号取全量详情（含收款账号、金额、事件流水），
--        等于把「查订单」变成可枚举接口；
--      · payExport/exportNotify —— 导出**全部**到账流水，一次整表外带。
--    前端没有对应按钮（由 scripts/s6_admin_probe.py 与单测调用），所以只能靠补 Btn 行收口。
--    ⚠️ 非超管角色需在「角色管理」里勾选这 4 项，否则调用会被 403；超管自动全量授权，不受影响。
--
-- ⚠️ 「开放接口按钮」为什么登记在 Core 的种子里而不是本模块的 PayMenuSeedData：
--    这两个接口属于框架的开放身份服务，按钮的父菜单 1310000000421 也在 Core 种子里，
--    就近放置才不会两处维护。
--    Core 种子是 [IgnoreUpdateSeed]（只跳过「更新已有行」，**新增行照常插入**），
--    且本项目 EnableIncreSeed=false，所以所有种子类每次启动都会执行，新增按钮能落到已有库。
--    背景：这两个接口原先**没有任何按钮权限**，而框架鉴权是「黑名单」模型
--    （SysRoleService.GetUserApiList 的 apiList[1] = 全部按钮 − 用户已有按钮），
--    没映射按钮的接口对**任何已登录用户**放行 —— 属于权限模型外的未审计接口。
--
-- ★★ 权限串必须是**接口路由的尾部**，而且**路由不一定等于方法名**：
--    框架按 path.EndsWith(permission) 匹配（大小写不敏感），如
--        `payAccount/page` ↔ `/api/payAccount/page`。
--    但 CreateSecret 生成的路由是 `/api/sysOpenAccess/secret`，**不是** `/createSecret`
--    —— 照方法名写权限串会永远匹配不上，菜单里勾了也没用、接口照样对所有人开放。
--    权威来源是前端 `Web/src/api-services/**` 里的 localVarPath（由 swagger 生成）。
--    守卫已自动化：scripts/pay_schema_guard.py §8（覆盖 /api/pay* 全部后台接口 + 两个 sysOpenAccess 按钮）。
-- ⚠️ 超管（AccountType=SuperAdmin）由框架直接返回全部启用菜单，**新菜单只加种子即可**；
--    非超管角色需在「角色管理」里勾选 —— 这一步是**数据**，不是 DDL。


-- --------------------------------------------------------------------------------------------
-- §5 执行后自检
-- --------------------------------------------------------------------------------------------
-- 期望：6 行，且每张表的列数与注释一致。
--   SELECT table_name, count(*) AS cols
--     FROM information_schema.columns
--    WHERE table_schema = 'public' AND table_name LIKE 'pay\_%'
--    GROUP BY table_name ORDER BY table_name;
--
-- 期望：24 行（6 主键 + 4 唯一 + 14 普通）。
--   SELECT count(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename LIKE 'pay\_%';
--
-- 期望：0 行（旧单列唯一索引已删除；残留会导致跨调用方同单号被拒）。
--   SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND indexname = 'u_pay_order_en';
--
-- 期望：0 行（列宽无漂移）。
--   SELECT table_name, column_name, character_maximum_length
--     FROM information_schema.columns
--    WHERE table_name = 'pay_order' AND column_name = 'orderno' AND character_maximum_length <> 64;
--
-- 更完整的契约校验直接跑脚本（共 124 项断言）：
--   §1~§5 表/列/类型/索引/唯一性/列宽三处联动（schema 层）
--   §6    框架表前置列（sysopenaccess.status）
--   §7    源码级安全不变式（幂等键隔离、归属校验、fail-closed、脱敏、审计、常量时间比较）
--   §8    前端权限串 ↔ 后端路由一致性（防「权限串匹配不上 = 接口对所有人开放」）
--   §9    掩码标记与密钥字符集不冲突
--   §10   入参 DTO 校验不变式（[Required] 挂非空值类型是空操作；更新类入参不得继承实体）
--   用法（需 venv，才有 psycopg2）：
--     ~/.workbuddy-ai/binaries/python/envs/default/bin/python scripts/pay_schema_guard.py
