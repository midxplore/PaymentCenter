#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""收款账号分配系统 —— schema 漂移守卫。

**它守的是什么**
SqlSugar CodeFirst 会建表、建索引，但**不会**改已有列的宽度/类型。于是存在一类
「代码能编译、单测能过、上线写库才报错」的缺陷：

    DTO 上写 [MaxLength(32)]，列宽却已是 varchar(64)（或反过来）——
    而当前数据**刚好卡在上限**，所以测试全绿，等位数增长后才炸。

本项目已实测踩过一次：`NotifyInput.OrderNo` 的 `[MaxLength(32)]` 与订单号当前正好 32 位撞线，
雪花尾段约 2027-11 进位后变 33 位，届时 `/api/pay/notify`（所有到账通知）会被参数校验整体挡下。

所以本脚本把**三处必须联动的数字**放到一起比对：

    ① C# 常量   PayConst.OrderNoLength
    ② 活库列宽  information_schema.columns
    ③ 入参上限  DTO 上的 [MaxLength(...)]

任何一处单独漂移都会被拦下。

**2026-09-17 扩展：从「schema 守卫」扩到「schema + 安全不变式守卫」**
审计对外接口鉴权时发现了若干**改不了库、只能改代码**的资金安全缺陷，例如：
幂等键漏带 ClientId（跨调用方串单）、订单查询缺归属校验、密钥明文回显、
签名非常量时间比较、认证失败无审计。这类问题「改对了」之后同样会被后来者
顺手简化掉，所以一并写成断言（§6 框架表前置列、§7 源码级安全不变式）。
读源码的断言故意写得**具体到行**（如「幂等键查询只有一处实现」），
这样失败信息能直接指出退化点，而不是只说「某处不对」。

**跑法**
    python scripts/pay_schema_guard.py

退出码 0 = 无漂移；非 0 = 有漂移（可直接挂 CI）。

**与其他文件的关系**
  · 期望 schema 的**可执行契约**是 `scripts/paycenter-schema.sql`（幂等，可修列宽）；
    本脚本只**读**不写，用于发现漂移后提示你去跑那个 SQL。
  · 复用 `_pay_common.py` 的 PG 直连（列名一律小写）。
"""

import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from _pay_common import (  # noqa: E402
    REPO, assert_true, section, report, pg_query,
)

# ─────────────────────────── 期望契约 ───────────────────────────
# 与 scripts/paycenter-schema.sql 逐字对应；改实体时两处一起改。

# 表 -> 期望列数
EXPECTED_COL_COUNT = {
    "pay_account": 15,
    "pay_order": 18,
    "pay_order_event": 18,
    "pay_notify_record": 16,
    "pay_abnormal_receipt": 22,
    "pay_audit_log": 20,
}

# 表 -> {列: 期望 varchar 宽度}
EXPECTED_VARCHAR = {
    "pay_account": {
        "type": 32, "accountinfo": 512, "remark": 256,
        "createusername": 64, "updateusername": 64,
    },
    "pay_order": {
        "orderno": 64, "externalno": 64, "overpayremark": 256,
        "createusername": 64, "updateusername": 64,
    },
    "pay_order_event": {
        "orderno": 64, "remark": 512, "operatorname": 64,
        "createusername": 64, "updateusername": 64,
    },
    "pay_notify_record": {
        "orderno": 64, "voucherno": 64,
        "createusername": 64, "updateusername": 64,
    },
    "pay_abnormal_receipt": {
        "voucherno": 64, "reportorderno": 64, "relatedorderno": 64,
        "handlername": 64, "handleremark": 256,
        "createusername": 64, "updateusername": 64,
    },
    "pay_audit_log": {
        "targettype": 32, "targetno": 64, "remark": 512,
        "operatorname": 64, "operatorip": 64,
        "clientkey": 128,
        "createusername": 64, "updateusername": 64,
    },
}

# 表 -> 期望金额列（一律 numeric(18,2)，单币种）
EXPECTED_NUMERIC = {
    "pay_account": ["totalquota", "usedquota", "lockedquota"],
    "pay_order": ["requestamount", "receivedamount"],
    "pay_order_event": ["amount", "receivedtotal"],
    "pay_notify_record": ["amount"],
    "pay_abnormal_receipt": ["amount"],
    "pay_audit_log": [],
}

# 表 -> 期望普通索引（不含主键）
EXPECTED_INDEX = {
    "pay_account": ["i_pay_account_tsc", "i_pay_account_ct"],
    "pay_order": ["i_pay_order_se", "i_pay_order_aid", "i_pay_order_ct"],
    "pay_order_event": ["i_pay_order_event_oid", "i_pay_order_event_ct"],
    "pay_notify_record": ["i_pay_notify_record_oid", "i_pay_notify_record_ct"],
    "pay_abnormal_receipt": ["i_pay_abnormal_receipt_hs", "i_pay_abnormal_receipt_ct"],
    "pay_audit_log": ["i_pay_audit_log_target", "i_pay_audit_log_action", "i_pay_audit_log_ct"],
}

# 唯一索引：**去重与幂等的正确性直接挂在这上面**，必须存在且真的 unique
EXPECTED_UNIQUE_INDEX = {
    "u_pay_order_no": ("pay_order", ["orderno"]),
    # ★ 幂等键是 (ClientId, ExternalNo) 组合，**不是** ExternalNo 单列。
    #   单列会让不同接入方互相串单（B 命中 A 的订单，拿到 A 的收款账号明文）。
    "u_pay_order_ce": ("pay_order", ["clientid", "externalno"]),
    "u_pay_notify_record_cv": ("pay_notify_record", ["clientid", "voucherno"]),
    "u_pay_abnormal_receipt_cv": ("pay_abnormal_receipt", ["clientid", "voucherno"]),
}

# 必须**不存在**的索引：SqlSugar CodeFirst 只建不删，历史索引会残留并继续生效。
# 残留 u_pay_order_en（externalno 单列唯一）会拒绝「不同调用方使用相同业务单号」，
# 而且是在代码改对之后才暴露 —— 极易误判成新引入的 bug。
FORBIDDEN_INDEX = {
    "u_pay_order_en": "externalno 单列唯一索引已废弃，必须由 paycenter-schema.sql 的 DROP INDEX 删除",
}

# 订单号家族列：宽度必须等于 PayConst.OrderNoLength
ORDER_NO_COLUMNS = [
    ("pay_order", "orderno"),
    ("pay_order", "externalno"),
    ("pay_order_event", "orderno"),
    ("pay_notify_record", "orderno"),
    ("pay_notify_record", "voucherno"),
    ("pay_abnormal_receipt", "voucherno"),
    ("pay_abnormal_receipt", "reportorderno"),
    ("pay_abnormal_receipt", "relatedorderno"),
    ("pay_audit_log", "targetno"),
]

# DTO 里凡是这些属性名，上限必须引用常量、不能写死数字
ORDER_NO_PROPERTIES = {"OrderNo", "TargetOrderNo", "RelatedOrderNo", "OrderNos"}

PAYCONST = os.path.join(REPO, "Admin.NET/Admin.NET.Application/PayCenter/Const/PayConst.cs")
DTO_DIR = os.path.join(REPO, "Admin.NET/Admin.NET.Application/PayCenter/Dto")


# ─────────────────────────── 读取活库 ───────────────────────────


def live_columns():
    """返回 {(表, 列): dict}。"""
    rows = pg_query(
        """
        select table_name, column_name, data_type,
               coalesce(character_maximum_length, 0) as vlen,
               coalesce(numeric_precision, 0)      as prec,
               coalesce(numeric_scale, 0)          as scale,
               is_nullable
          from information_schema.columns
         where table_schema = 'public' and table_name like 'pay%'
        """
    )
    return {(r["table_name"], r["column_name"]): r for r in rows}


def live_indexes():
    """返回 {索引名: (表名, is_unique, [列...])}，只含 pay_* 表。"""
    out = {}
    rows = pg_query(
        """
        select t.relname as table_name, i.relname as index_name, ix.indisunique as is_unique,
               array_to_string(array_agg(a.attname order by k.ord), ',') as cols
          from pg_index ix
          join pg_class i on i.oid = ix.indexrelid
          join pg_class t on t.oid = ix.indrelid
          join pg_namespace n on n.oid = t.relnamespace
          join lateral unnest(ix.indkey) with ordinality as k(attnum, ord) on true
          join pg_attribute a on a.attrelid = t.oid and a.attnum = k.attnum
         where n.nspname = 'public' and t.relname like 'pay%'
         group by 1, 2, 3
        """
    )
    for r in rows:
        out[r["index_name"]] = (r["table_name"], r["is_unique"], r["cols"].split(","))
    return out


def read_order_no_length():
    """从 PayConst.cs 抠出 OrderNoLength。"""
    with open(PAYCONST, encoding="utf-8") as f:
        m = re.search(r"OrderNoLength\s*=\s*(\d+)", f.read())
    return int(m.group(1)) if m else None


def read_dto_maxlengths():
    """扫 DTO 目录，返回 [(文件, 属性名, 上限表达式)]。

    形如：
        [MaxLength(PayConst.OrderNoLength, ErrorMessage = "...")]
        public string OrderNo { get; set; }
    """
    found = []
    pat_attr = re.compile(r"\[MaxLength\(([^,)]+?)\s*,")
    pat_prop = re.compile(r"public\s+[\w<>?\[\]]+\s+(\w+)\s*\{\s*get;")
    for name in sorted(os.listdir(DTO_DIR)):
        if not name.endswith(".cs"):
            continue
        pending = None
        with open(os.path.join(DTO_DIR, name), encoding="utf-8") as f:
            for line in f:
                m = pat_attr.search(line)
                if m:
                    pending = m.group(1).strip()
                    continue
                m = pat_prop.search(line)
                if m and pending is not None:
                    found.append((name, m.group(1), pending))
                    pending = None
    return found


# ─────────────────── 源码级安全不变式（读代码，不读库） ───────────────────
# 下面这些缺陷**改不了库、只能改代码**，所以守卫也必须读源码。
# 它们都是本项目实际踩过或审计出来的资金安全缺陷，写成断言防止回退。

PAYCENTER = "Admin.NET/Admin.NET.Application/PayCenter"
CORE_OPEN_ACCESS = "Admin.NET/Admin.NET.Core/Service/OpenAccess"


def read_source(rel_path):
    """按行读源码；文件不存在返回 None。"""
    full = os.path.join(REPO, rel_path)
    if not os.path.exists(full):
        return None
    with open(full, encoding="utf-8") as f:
        return f.readlines()


def strip_line_comment(line):
    """去掉 C# 行注释（`//` 之后的部分）。

    为什么必须剥：本项目的注释**大量在解释「不要怎么写」**，例如
        // ★ 常量时间比较，不要写成 serverSign != sign。
    若不剥，这类「反面教材」注释会被正则当成真实代码命中，
    于是守卫自己产生假 FAIL（实测踩过：常量时间比较那条断言就是这么误报的）。
    """
    idx = line.find("//")
    return line[:idx] if idx >= 0 else line


def scan(rel_path, pattern, ignore_comments=True):
    """返回 [(行号, 行内容)]，行号从 1 开始。

    默认剥掉行注释后再匹配：这些断言校验的是**实现**，
    注释里出现某个写法不代表真的用了它（反之亦然，注释里没写也不代表没用）。
    """
    lines = read_source(rel_path)
    if lines is None:
        return None
    pat = re.compile(pattern)
    out = []
    for i, ln in enumerate(lines):
        body = strip_line_comment(ln) if ignore_comments else ln
        if pat.search(body):
            out.append((i + 1, ln.rstrip("\n")))
    return out


def live_open_access_status():
    """返回 sysopenaccess.status 的列信息；不存在返回 None。

    ⚠️ 这是**框架表**（Admin.NET.Core），不属于 pay_* 家族，
    所以 live_columns() 的 `table_name like 'pay%'` 过滤抓不到它，需要单独查。
    """
    rows = pg_query(
        """
        select data_type, is_nullable, column_default
          from information_schema.columns
         where table_schema = 'public'
           and table_name   = 'sysopenaccess'
           and column_name  = 'status'
        """
    )
    return rows[0] if rows else None


# 非空值类型：装箱后永远不为 null，所以 RequiredAttribute 对它们**恒为通过**
VALUE_TYPE_NAMES = {
    "long", "ulong", "int", "uint", "short", "ushort", "byte", "sbyte",
    "decimal", "double", "float", "bool",
    "DateTime", "DateTimeOffset", "TimeSpan", "Guid", "StatusEnum",
}

_PROP_RE = re.compile(r"public\s+(?:override\s+|new\s+|virtual\s+)*([\w<>?\[\],\.]+)\s+(\w+)\s*\{")


def required_props(rel_path):
    """扫描 ``[Required]`` 修饰的属性，返回 ``[(类名, 属性名, 类型名, 特性所在行号)]``。

    ⚠️ 必须**跨行**看：``[Required]`` 与属性声明通常不在同一行（实测本项目就是这样），
    所以不能只用一个单行正则，否则会漏判 —— 而漏判的守卫比没有守卫更糟。

    逐行维护 ``cur_class``（最近一个 ``public class``）与 ``pending_required``
    （最近一个还没被消费的 ``[Required]`` 行号），遇到属性声明时配对。
    非特性、非空白的行会清掉 ``pending_required``，避免「隔了十万八千里也算命中」。
    """
    lines = read_source(rel_path) or []
    cur_class, pending, out = None, None, []
    for i, ln in enumerate(lines):
        body = strip_line_comment(ln)
        mc = re.match(r"\s*public\s+(?:sealed\s+|partial\s+|abstract\s+)*class\s+(\w+)", body)
        if mc:
            cur_class, pending = mc.group(1), None
            continue
        if re.search(r"\[Required\b", body):
            pending = i + 1
            continue
        mp = _PROP_RE.search(body)
        if mp:
            if pending is not None:
                out.append((cur_class, mp.group(2), mp.group(1), pending))
            pending = None
        elif body.strip() and not body.strip().startswith("["):
            # 既不是特性也不是属性声明的实义行：断开配对
            pending = None
    return out


# ─────────────────────────── 主流程 ───────────────────────────

def main():
    cols = live_columns()
    idx = live_indexes()
    order_no_len = read_order_no_length()

    # ── 1. 表与列数 ─────────────────────────────────────────────
    section("1. 表与列数")
    for table, want in EXPECTED_COL_COUNT.items():
        got = sum(1 for (t, _) in cols if t == table)
        assert_true(got == want, f"{table} 列数 = {want}", f"实际 {got}")

    # ── 2. 列类型与宽度 ─────────────────────────────────────────
    section("2. 列类型与宽度（CodeFirst 不会自动改的就是这里）")
    for table, want_map in EXPECTED_VARCHAR.items():
        for col, want in want_map.items():
            r = cols.get((table, col))
            if r is None:
                assert_true(False, f"{table}.{col} 存在", "列缺失")
                continue
            got = r["vlen"]
            assert_true(
                r["data_type"] == "character varying" and got == want,
                f"{table}.{col} = varchar({want})",
                f"实际 {r['data_type']}({got})"
                + ("  ← 跑 scripts/paycenter-schema.sql 纠偏" if got != want else ""),
            )

    for table, num_cols in EXPECTED_NUMERIC.items():
        for col in num_cols:
            r = cols.get((table, col))
            if r is None:
                assert_true(False, f"{table}.{col} 存在", "列缺失")
                continue
            assert_true(
                r["data_type"] == "numeric" and r["prec"] == 18 and r["scale"] == 2,
                f"{table}.{col} = numeric(18,2)",
                f"实际 numeric({r['prec']},{r['scale']})",
            )

    # ── 3. 索引 ────────────────────────────────────────────────
    section("3. 索引")
    for table, names in EXPECTED_INDEX.items():
        for name in names:
            hit = idx.get(name)
            assert_true(
                hit is not None and hit[0] == table and not hit[1],
                f"{name} 存在（{table} 普通索引）",
                "缺失" if hit is None else f"实际 unique={hit[1]}",
            )

    # 唯一索引：去重/幂等的正确性挂在这上面，必须 unique 且列完全一致
    for name, (table, want_cols) in EXPECTED_UNIQUE_INDEX.items():
        hit = idx.get(name)
        if hit is None:
            assert_true(False, f"{name} 存在（{table} 唯一索引）", "缺失")
            continue
        assert_true(
            hit[0] == table and hit[1] and hit[2] == want_cols,
            f"{name} = UNIQUE({', '.join(want_cols)})",
            f"实际 {hit[0]} unique={hit[1]} ({', '.join(hit[2])})",
        )

    # ★ 废弃索引必须真的被删掉：CodeFirst 只建不删，残留索引会继续生效
    for name, why in FORBIDDEN_INDEX.items():
        # 注意 detail 是**立即求值**的（assert_true 的普通参数），
        # 所以不能直接写 idx[name] —— 索引已删时那会 KeyError，反而把守卫自己搞崩。
        present = name in idx
        assert_true(
            not present,
            f"{name} 已删除（废弃索引不得残留）",
            f"仍存在 {idx[name]} ← {why}" if present else "",
        )

    # ── 4. 常量 ↔ 列宽联动（P1 缺陷的直接守卫）────────────────────
    section("4. 常量 ↔ 列宽联动")
    if order_no_len is None:
        assert_true(False, "PayConst.OrderNoLength 可解析", f"未能在 {PAYCONST} 找到")
    else:
        assert_true(True, f"PayConst.OrderNoLength = {order_no_len}", PAYCONST)
        for table, col in ORDER_NO_COLUMNS:
            r = cols.get((table, col))
            if r is None:
                assert_true(False, f"{table}.{col} 宽度 == OrderNoLength", "列缺失")
                continue
            assert_true(
                r["vlen"] == order_no_len,
                f"{table}.{col} 宽度 == OrderNoLength({order_no_len})",
                f"实际 varchar({r['vlen']})",
            )

    # ── 5. DTO 入参上限 ↔ 列宽联动 ───────────────────────────────
    section("5. DTO 入参上限 ↔ 列宽联动")
    dto = read_dto_maxlengths()
    assert_true(len(dto) > 0, "扫到 DTO 上的 [MaxLength]", f"{len(dto)} 处")

    # 5a. 订单号家族属性：必须引用常量
    for fname, prop, expr in dto:
        if prop in ORDER_NO_PROPERTIES:
            assert_true(
                expr == "PayConst.OrderNoLength",
                f"{fname} → {prop} 上限引用 PayConst.OrderNoLength",
                f"实际 [MaxLength({expr})]"
                + ("  ← 写死数字会与列宽脱节" if expr != "PayConst.OrderNoLength" else ""),
            )

    # 5b. 其余字面量：若存在同名 DB 列，上限必须等于列宽
    #     属性名小写即列名（OrderNo→orderno、AccountInfo→accountinfo）。
    by_name = {}
    for (table, col), r in cols.items():
        if r["vlen"]:
            by_name.setdefault(col, set()).add(r["vlen"])
    for fname, prop, expr in dto:
        if prop in ORDER_NO_PROPERTIES or not expr.isdigit():
            continue
        widths = by_name.get(prop.lower())
        if not widths:
            continue  # 没有同名列（如纯 DTO 字段），不判
        n = int(expr)
        assert_true(
            n in widths,
            f"{fname} → {prop} 上限({n}) == 列宽",
            f"列宽候选 {sorted(widths)}",
        )

    # ── 6. 框架表上的模块前置列 ──────────────────────────────────
    # sysopenaccess 属于 Admin.NET.Core，不在 pay_* 家族里，但本模块的资金安全依赖它：
    # 没有 status 列就无法「停用而不删除」凭证（删行会让 pay_order.clientid 悬空、审计断链）。
    section("6. 框架表前置列（本模块依赖但不拥有）")
    oa_status = live_open_access_status()
    if oa_status is None:
        assert_true(False, "sysopenaccess.status 存在（凭证启停）",
                    "列缺失 ← 跑 scripts/paycenter-schema.sql §3.5.2 补齐；"
                    "缺失时凭证无法停用，密钥泄漏只能删行（审计链断）")
    else:
        assert_true(
            oa_status["data_type"] == "integer" and oa_status["is_nullable"] == "NO",
            "sysopenaccess.status = integer NOT NULL",
            f"实际 {oa_status['data_type']} nullable={oa_status['is_nullable']}",
        )
        # 默认值必须是 1（StatusEnum.Enable），否则新增凭证会落成「停用」而无法调用
        assert_true(
            (oa_status["column_default"] or "").startswith("1"),
            "sysopenaccess.status 默认 1（启用）",
            f"实际 default={oa_status['column_default']}",
        )

    # ── 7. 源码级安全不变式 ──────────────────────────────────────
    # 下面每一条都对应一个**真实审计出来的资金安全缺陷**。
    # 它们改不了库、只能改代码，所以守卫必须读源码 —— 目的是防止后来者「顺手简化」时回退。
    #
    # 注意：detail 参数是**立即求值**的，所以统一走 check() 包一层，
    # 只在失败时才把「退化点说明」拼出来（否则 PASS 行上会挂一句吓人的解释，读起来像失败）。
    section("7. 源码级安全不变式")

    def check(cond, label, fail_detail):
        return assert_true(cond, label, "" if cond else fail_detail)

    allocate = f"{PAYCENTER}/Service/PayAllocateService.cs"

    # 7a. 幂等键必须带 ClientId（跨调用方串单的直接守卫）
    hits = scan(allocate, r"u\.ExternalNo\s*==\s*externalNo")
    if hits is None:
        check(False, "PayAllocateService.cs 可读", "文件不存在")
    else:
        check(
            len(hits) == 1,
            "幂等键查询只有一处实现（FindByIdempotencyKeyAsync）",
            f"实际 {len(hits)} 处：" + "；".join(f"L{n}" for n, _ in hits)
            + " ← 散落多处时，任何一处漏写 ClientId 都会重开串单缺口",
        )
        for lineno, line in hits:
            check(
                "ClientId" in line,
                f"幂等键查询带 ClientId（L{lineno}）",
                f"L{lineno}: {line.strip()} ← 只按 ExternalNo 查会让不同接入方互相命中对方订单",
            )

    # 7b. 订单状态查询必须带 ClientId（越权读订单的直接守卫）
    hits = scan(allocate, r"u\.OrderNo\s*==\s*orderNo")
    if hits:
        for lineno, line in hits:
            check(
                "ClientId" in line,
                f"PayAllocateService 订单查询带 ClientId（L{lineno}）",
                f"L{lineno}: {line.strip()} ← 缺归属校验时任何有效密钥都能查任意订单",
            )

    # 7c. GetStatus 必须显式声明 [PayScope]（不能只靠路径兜底）
    hits = scan(allocate, r"\[PayScope\(")
    check(
        hits is not None and len(hits) >= 2,
        "PayAllocateService 中 [PayScope] 声明 ≥ 2 处（Allocate + GetStatus）",
        f"实际 {0 if hits is None else len(hits)} 处 ← 少一处就会退化成路径兜底匹配",
    )

    # 7d. scope 兜底必须 fail-closed（未归类路径不得返回 null = 不限制）
    check(
        bool(scan(f"{PAYCENTER}/Auth/PayScope.cs", r"return\s+PayConst\.ScopeUnclassified")),
        "PayScopeResolver 对未归类 /api/pay* 路径 fail-closed",
        "未找到 ScopeUnclassified 兜底 ← 返回 null 等于新接口默认敞开",
    )

    # 7e. 密钥不得明文回显（列表接口脱敏的直接守卫）
    check(
        bool(scan(f"{CORE_OPEN_ACCESS}/Dto/OpenAccessOutput.cs", r"OpenAccessSecretMask\.Mask")),
        "OpenAccessOutput.AccessSecret 走掩码（getter 脱敏）",
        "未找到掩码调用 ← 分页接口会把全部接入方密钥明文吐给只读账号",
    )

    # 7f. 认证失败必须落审计（不能只靠会轮转的文本日志）
    check(
        bool(scan(f"{CORE_OPEN_ACCESS}/SysOpenAccessService.cs", r"IOpenAccessAuditSink")),
        "签名鉴权失败时调用 IOpenAccessAuditSink",
        "未找到审计钩子调用 ← 401 发生在 MVC 之前，SysLogOp 采不到，失败将无任何可检索留痕",
    )
    check(
        bool(scan(f"{PAYCENTER}/Auth/PayOpenAccessAuditSink.cs", r"IOpenAccessAuditSink")),
        "PayOpenAccessAuditSink 已实现审计钩子",
        "实现类缺失 ← 钩子无人实现时认证失败不会落库",
    )

    # 7g. 资金动作必须写调用审计
    for rel, label in [
        (f"{PAYCENTER}/Service/PayAllocateService.cs", "查询匹配"),
        (f"{PAYCENTER}/Service/PayNotifyService.cs", "到账通知"),
    ]:
        check(
            bool(scan(rel, r"WriteOpenApiAsync\(PayAuditActionEnum\.ApiCall")),
            f"{label} 写 ApiCall 调用审计",
            "未找到审计写入 ← 资金变动将无法回答「是哪个接入方发起的」",
        )

    # 7h. 签名比较必须是常量时间
    check(
        not scan("Admin.NET/Admin.NET.Core/SignatureAuth/SignatureAuthenticationHandler.cs",
                 r"serverSign\s*!=\s*sign"),
        "签名比较使用常量时间函数（非 != 短路比较）",
        "仍在使用 != ← 比较耗时随公共前缀变化，构成 timing oracle",
    )

    # 7i. 凭证必须可停用（不能只靠删除吊销）
    check(
        bool(scan("Admin.NET/Admin.NET.Core/Entity/SysOpenAccess.cs", r"StatusEnum\s+Status")),
        "SysOpenAccess 有 Status 字段（停用而不删除）",
        "缺少状态字段 ← 密钥泄漏只能删行，pay_order.clientid 会悬空、审计断链",
    )
    check(
        bool(scan(f"{CORE_OPEN_ACCESS}/SysOpenAccessService.cs", r"StatusEnum\.Enable")),
        "签名校验路径检查凭证状态（停用即失效）",
        "未找到状态闸门 ← Status 字段会形同虚设",
    )

    # 7n. 本地调试开关必须归位：租户图形验证码种子必须停在 true
    #
    # 为什么这是**安全**断言而不是开发体验问题：
    #   pay_* 的 HTTP 回归脚本要登录后台，而租户图形验证码默认开启会挡住登录。
    #   历史上每轮跑回归的流程是「把 Captcha 改成 false → 重启 → 跑脚本 → 改回 true」。
    #   这一步**必然会被忘记**，而后果不是「脚本跑不了」，是**登录不再需要验证码** ——
    #   一个安全控制在生产里被静默关掉。所以把「必须停在 true」变成断言。
    #
    # ★ 生效的是 Application 那份：
    #   Core/SeedData/SysTenantSeedData.cs 带 [IgnoreUpdateSeed]（只跳过「更新已有行」），
    #   而 Application/SeedData 那份是 [SeedData(500)] 且 Id 与框架一致 = **重写框架种子**。
    #   只改 Core 那份不会有任何效果 —— 这正是「以为改了其实没改」的典型。
    tenant_seed = "Admin.NET/Admin.NET.Application/SeedData/SysTenantSeedData.cs"
    lines = read_source(tenant_seed)
    if lines is None:
        check(False, "SysTenantSeedData.cs 可读", f"{tenant_seed} 不存在")
    else:
        # 必须剥注释：解释这条规则的文字里很可能出现 "Captcha=false" 这种反面教材，
        # 不剥就会被当成真实赋值（本项目已因此误报过一次）。
        body = "\n".join(strip_line_comment(l) for l in lines)
        flags = re.findall(r"Captcha\s*=\s*(true|false)\b", body)
        check(
            len(flags) >= 1,
            "租户种子含 Captcha 显式赋值",
            "没找到 Captcha=… ← 依赖框架默认值时，行为会随框架版本悄悄变化",
        )
        check(
            all(f == "true" for f in flags),
            "租户图形验证码种子为 true（本地调试翻转后必须归位）",
            f"实际 {flags} ← 跑完回归脚本忘了改回来，生产登录将不再需要图形验证码",
        )

    # ── 7o. 时间基准：列类型必须与「写入用本地时间」配套 ──────────────
    # 为什么必须守：pay_* 的时间列是 `timestamp without time zone`（不带时区，存的是
    # 「+08 墙上时间」字面值），而应用与框架 AOP 全部用 DateTime.Now（本地时间），
    # 入参经 Newtonsoft `DateTimeZoneHandling=Local` 归一化、输出不带偏移。
    # 这四者**必须同时成立**才自洽。只要有一列被单独改成 timestamptz，
    # 那一列的含义就与其它列不同，而**不会有任何报错** —— 只会在对账时发现差 8 小时。
    # 所以做成一条「必须成组变更」的断言：要迁 UTC 就得连守卫、写入路径、前端一起改。
    section("7o. 时间基准（列类型 ↔ 写入方式）")

    ts_cols = {k: v for k, v in cols.items() if "timestamp" in (v["data_type"] or "")}
    check(
        len(ts_cols) >= 10,
        "pay_* 时间列可枚举",
        f"只找到 {len(ts_cols)} 个时间列，期望 ≥10 ← 连错库了？",
    )
    bad_ts = sorted(
        f"{t}.{c} → {v['data_type']}"
        for (t, c), v in ts_cols.items()
        if v["data_type"] != "timestamp without time zone"
    )
    check(
        not bad_ts,
        "pay_* 时间列全为 timestamp without time zone（与本地时间写入配套）",
        "以下列类型不符：" + ", ".join(bad_ts)
        + " ← 单独改列会与 DateTime.Now 的写入语义脱节（静默差 8 小时）。"
        "若确实要迁到 UTC + timestamptz，必须同时改 PayTimeBasis 守卫、写入路径与前端展示，"
        "并在同一提交里更新本断言。",
    )

    # ── 7p. 时间基准守卫必须真的挂上 ────────────────────────────────
    # ★ 这一条防的是「守卫被删成空壳」：文件还在、类型还能被扫到，
    #   但 ConfigureServices 里不再调用校验 —— 代码看起来完全正常，守卫静默失效。
    #   （实测教训：第一版测试只断言了「类型出现在 App.EffectiveTypes 里」，
    #     而 TZ=UTC 时守卫并未拦住启动，那条断言却照样通过 —— 典型的假绿灯。）
    section("7p. 时间基准守卫已挂载")

    guard_file = f"{PAYCENTER}/Const/PayTimeBasis.cs"
    check(read_source(guard_file) is not None, "PayTimeBasis.cs 存在", f"{guard_file} 不存在")

    startup_file = f"{PAYCENTER}/PayCenterStartup.cs"
    startup_lines = read_source(startup_file)
    if startup_lines is None:
        check(
            False,
            "PayCenterStartup.cs 存在",
            f"{startup_file} 不存在 ← 守卫不会被任何宿主执行，等于没写",
        )
    else:
        startup_body = "\n".join(strip_line_comment(l) for l in startup_lines)
        check(
            ": AppStartup" in startup_body,
            "PayCenterStartup 派生自 AppStartup",
            "类声明里没有 ': AppStartup' ← Furion 只执行 AppStartup 子类，守卫不会被调用",
        )
        check(
            "PayTimeBasis.EnsureLocalTimeBasis" in startup_body,
            "PayCenterStartup 真的调用了时间基准校验",
            "没找到 PayTimeBasis.EnsureLocalTimeBasis() 调用 ← 守卫成了空壳，启动不再校验",
        )

    # ── 7q. 设计文档必须写清时间基准 ────────────────────────────────
    # 为什么：这是**接口契约**（三方按什么时区传 notifyTime），
    # 不写在文档里，接入方只能猜，而猜错的方向是静默 8 小时偏差。
    section("7q. 设计文档写明时间基准")

    doc_file = "doc/收款账号分配系统-技术设计方案.md"
    doc_lines = read_source(doc_file)
    if doc_lines is None:
        check(False, "设计文档可读", f"{doc_file} 不存在")
    else:
        doc_body = "\n".join(doc_lines)
        check("时间基准" in doc_body, "设计文档含「时间基准」章节", "没找到该章节")
        check(
            "timestamp without time zone" in doc_body,
            "设计文档说明了列类型与语义",
            "文档没写列类型 ← 接入方无法判断该按哪个时区传时间",
        )

    # ── 8. 前端权限串 ↔ 后端路由一致性 ──────────────────────────────
    # 为什么必须守：JwtHandler 的判定是 path.EndsWith(permission)（大小写不敏感），
    # 且用的是**黑名单**模型（apiList[1] = 全部按钮 − 用户已有按钮）：
    #   路径能匹配到某个 permission → 若用户没有它则 403；
    #   路径匹配不到**任何** permission → 黑名单里也没有它 → **放行**。
    # 所以「Permission 串写错 / 漏写」不报错，只会让接口对任何已登录用户开放。
    # 实测踩过：CreateSecret 的路由是 /api/sysOpenAccess/secret（不是 /createSecret），
    # 照方法名写权限串会永远匹配不上。
    section("8. 前端权限串 ↔ 后端路由一致性")

    routes = set()
    for dirpath, _dirs, filenames in os.walk(os.path.join(REPO, "Web/src/api-services")):
        for fn in filenames:
            if not fn.endswith(".ts"):
                continue
            with open(os.path.join(dirpath, fn), encoding="utf-8") as f:
                for m in re.finditer(r"localVarPath\s*=\s*[`'\"](/api/[^`'\"]+)[`'\"]", f.read()):
                    routes.add(m.group(1).split("?")[0].rstrip("/"))

    check(
        len(routes) > 100,
        f"从 api-services 收集到后端路由 {len(routes)} 条",
        "路由过少 ← api-services 可能未生成或路径结构变了，本节结论不可信",
    )

    # 收集菜单种子里声明的按钮权限（含框架自带，避免把框架接口误判为未覆盖）
    perms = set()
    for rel in [
        f"{PAYCENTER}/SeedData/PayMenuSeedData.cs",
        "Admin.NET/Admin.NET.Core/SeedData/SysMenuSeedData.cs",
    ]:
        for _lineno, line in (scan(rel, r"Permission\s*=") or []):
            for m in re.finditer(r'Permission\s*=\s*"([^"]+)"', line):
                perms.add(m.group(1))

    # PayCenter 的后台接口：排除 /api/pay/{allocate,notify,status}（走签名鉴权，不走按钮权限）
    admin_routes = sorted(
        r for r in routes
        if "/api/pay" in r and not re.match(r"^/api/pay/(allocate|notify|status)$", r)
    )
    uncovered = [r for r in admin_routes if not any(r.lower().endswith(p.lower()) for p in perms)]
    check(
        not uncovered,
        f"PayCenter 后台接口全部被按钮权限覆盖（{len(admin_routes)} 条）",
        "未被覆盖：" + ", ".join(uncovered) + " ← 这些接口对任何已登录用户开放",
    )

    # 凭证管理面（框架页面，但属资金接口的凭证管理面）必须纳入 RBAC
    for perm in ["sysOpenAccess/secret", "sysOpenAccess/generateSignature"]:
        check(
            any(r.lower().endswith(perm.lower()) for r in routes),
            f"权限串 {perm} 能命中真实路由",
            f"{perm} 匹配不到任何路由 ← 该接口不受 RBAC 约束"
            f"（注意 CreateSecret 的路由是 /secret，不是 /createSecret）",
        )

    # ── 9. 掩码标记与密钥字符集不冲突 ───────────────────────────────
    # OpenAccessSecretMask 以 `*` 作标记，并**拒绝任何含 `*` 的入参**。
    # 这只有在「生成的密钥不可能含 `*`」时才成立：生成器用 Base64（字符集不含 `*`）。
    # 一旦有人把生成器换成含 `*` 的字符集，用户点「生成密钥」拿到的真密钥会被当成掩码拒绝。
    section("9. 掩码标记与密钥字符集不冲突")
    check(
        bool(scan(f"{CORE_OPEN_ACCESS}/SysOpenAccessService.cs", r"ToBase64String")),
        "CreateSecret 用 Base64 生成密钥（字符集不含 *）",
        "未找到 ToBase64String ← 若改用含 * 的字符集，「含 * 即掩码」会把真密钥误判为掩码",
    )

    # ── 10. 入参 DTO 的校验必须真的生效 ─────────────────────────────
    # 本节两条都是**静默失效**型的坑：不报错、不抛异常，只是校验没生效。
    # 而凭证管理面的校验一旦失效，后果是「本该必填的绑定用户为空」的凭证
    # 被建出来，然后该 accessKey 的**每一次**调用都在 OnValidated 里 NRE → 500。
    section("10. 入参 DTO 校验不变式")

    dto_file = f"{CORE_OPEN_ACCESS}/Dto/OpenAccessInput.cs"

    # 10a. [Required] 不得修饰非空值类型
    #      RequiredAttribute.IsValid 只判 `value is null`，装箱后的 long/int/enum 永不为 null，
    #      所以 [Required] 挂在它们上面等于**没写**。要拦 0 只能用 [Range(1, ...)]。
    bad_required = [
        (cls, prop, typ, lineno)
        for cls, prop, typ, lineno in required_props(dto_file)
        if typ in VALUE_TYPE_NAMES
    ]
    check(
        not bad_required,
        "DTO 上不存在 [Required] 修饰非空值类型（恒为通过 = 没校验）",
        "以下校验不会生效，应改用 [Range(1, long.MaxValue, ErrorMessage = ...)]："
        + "; ".join(f"L{n} {c}.{p} ({t})" for c, p, t, n in bad_required),
    )

    # 10b. 更新类入参不得继承实体 / 其它 DTO
    #      坑的机理：DataAnnotations 特性 Inherited=true，override 属性会沿重写链
    #      把基类同名属性上的特性一并继承（TypeDescriptor 会合并），
    #      于是实体上的 [Required] 会漏进「本该可选」的字段。
    #      实测两次：先继承 AddOpenAccessInput 被挡；改继承 SysOpenAccess **依然被挡**，
    #      报默认英文文案 "The AccessSecret field is required."（本类没写过英文文案，
    #      看到英文默认文案即说明来自继承链上游）。
    check(
        not scan(dto_file, r"class\s+Update\w*Input\s*:"),
        "更新类入参不继承任何实体/DTO（校验特性不再沿继承链漏入）",
        "检测到基类 ← 继承链上的校验特性是隐式的，改一个基类属性就可能悄悄多出一条必填；"
        "请平铺声明全部字段",
    )

    # 10c. 更新入参的密钥必须可选
    #      「留空 / 保持掩码 = 不修改」是编辑弹窗的既定语义（前端 placeholder 与
    #      secretRules 都按此实现）。若这里出现 [Required]，该语义就永远走不到服务端，
    #      而运维**停用一把泄漏的密钥**时会被迫回填密钥明文。
    upd_required = {
        p for c, p, _t, _n in required_props(dto_file) if c == "UpdateOpenAccessInput"
    }
    check(
        "AccessSecret" not in upd_required,
        "更新入参的 AccessSecret 不是必填（留空 / 掩码 = 不修改）",
        "AccessSecret 被标为必填 ← 「留空即不修改」的语义失效，"
        "且停用泄漏密钥时被迫回填明文",
    )
    check(
        "AccessKey" in upd_required,
        "更新入参的 AccessKey 仍为必填（没有被顺手放开）",
        "AccessKey 不再必填 ← 定位不到要改哪一行",
    )

    # 10d. 状态字段必须可空 = 「不修改」
    #      若给 Status 一个 `= StatusEnum.Enable` 默认值，
    #      「只改 scopes、没带 status」的请求会**静默把已停用的凭证重新启用** ——
    #      停用是密钥泄漏时的止血手段，不能存在任何「不显式声明就被关掉」的路径。
    check(
        bool(scan(dto_file, r"StatusEnum\?\s+Status")),
        "更新入参的 Status 可空（null = 不修改，不会静默重新启用）",
        "Status 不是可空 ← 不传状态的更新会把已停用凭证悄悄改回启用",
    )

    return report("schema + 安全不变式守卫")


if __name__ == "__main__":
    sys.exit(main())
