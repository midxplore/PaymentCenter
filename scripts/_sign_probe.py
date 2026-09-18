#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""开放接口签名探测工具（本地手工联调用，非交付物）。

用法：
    python scripts/_sign_probe.py GET  "/api/pay/status?orderNo=X"
    python scripts/_sign_probe.py POST /api/pay/allocate '{"type":"wxpay","amount":10}'
    python scripts/_sign_probe.py POST /api/pay/notify   '{"orderNo":"X","amount":10,"voucherNo":"V1"}'

需要开放身份（accessKey/secretKey）：
    PC_AK / PC_SK 环境变量，或 `--ak` / `--sk` 参数。

★ 签名逻辑**不要**在这里重写一遍 —— 统一走 `_pay_common.sign_call`。
  历史教训：本文件曾自己拼签名串，于是
    ① nonce 用了毫秒时间戳 → 并发下撞 nonce，被框架重放检测判成 `401 重复的请求`；
    ② 路由改名后（`/api/payAllocate/*` → `/api/pay/*`）本文件的示例路径没跟着改。
  签名串怎么拼、nonce 怎么取，只允许有一个来源。
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from _pay_common import sign_call  # noqa: E402

DEFAULT_AK = os.environ.get("PC_AK", "")
DEFAULT_SK = os.environ.get("PC_SK", "")


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--ak=") and not a.startswith("--sk=")]
    ak = next((a.split("=", 1)[1] for a in argv[1:] if a.startswith("--ak=")), DEFAULT_AK)
    sk = next((a.split("=", 1)[1] for a in argv[1:] if a.startswith("--sk=")), DEFAULT_SK)

    method = (args[0] if args else "GET").upper()
    url = args[1] if len(args) > 1 else "/api/pay/status"
    body = json.loads(args[2]) if len(args) > 2 else None

    if not ak or not sk:
        print("缺少开放身份：请设 PC_AK / PC_SK 环境变量，或用 --ak=xxx --sk=yyy")
        return 2

    status, payload, _ = sign_call(method, url, body, ak=ak, sk=sk)
    print(f"HTTP {status}")
    print(json.dumps(payload, ensure_ascii=False, indent=2) if isinstance(payload, dict) else payload)
    return 0 if status == 200 and isinstance(payload, dict) and payload.get("code") == 200 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
