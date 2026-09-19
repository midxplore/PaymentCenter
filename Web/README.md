# 支付中心 · 前端（`Web/`）

收款账号分配系统的管理端。Vue 3 + TypeScript + Element Plus + Vite 7。

后端与业务契约见仓库根的 [`../AGENTS.md`](../AGENTS.md) 与 [`../doc/收款账号分配系统-技术设计方案.md`](../doc/收款账号分配系统-技术设计方案.md)；本文只讲前端怎么跑、怎么改。

## 开发命令

```bash
env -u NODE_OPTIONS npm install          # 宿主注入的 NODE_OPTIONS 会让 vite 崩，必须剥掉
env -u NODE_OPTIONS npm run dev          # http://localhost:8888（后端需在 :5005）
env -u NODE_OPTIONS npm run typecheck    # ★ 类型检查闸门：本模块必须 0 错
env -u NODE_OPTIONS npm run typecheck:all # 看全量明细（含已登记的框架/生成物债务）
env -u NODE_OPTIONS npm run build        # 构建产物 dist/
env -u NODE_OPTIONS npm run buildApi     # 由后端 swagger.json 重新生成 src/api-services/system/
```

开发服务器把 `^/api`、`^/[Uu]pload`、`^/[Ss]se` 代理到 `VITE_API_URL`（默认 `http://localhost:5005`），见 `vite.config.ts`。

## 目录

| 路径 | 说明 |
|---|---|
| `src/views/paycenter/` | **本项目的业务页面**：收款账号 / 收款订单 / 异常到账 / 业务审计 |
| `src/views/system/`、`login/`、`home/`、`about/` | 框架自带页面（品牌已改为本产品） |
| `src/api-services/system/` | **Swagger 生成物，不要手改**；后端接口变了就跑 `npm run buildApi` |
| `src/stores/themeConfig.ts` | 品牌默认值（运行时实际值以 `/api/sysTenant/sysInfo` 下发的租户配置为准） |
| `script/check-types.mjs` | 类型检查闸门定义：`src/views/paycenter/` + `src/api-services/` + `src/views/system/openAccess/` 必须 0 错 |

## 改完必须人工核对的三项（类型检查查不出来）

1. 菜单 `component` 在 `import.meta.glob` 里**必须恰好命中 1 个**（0 → 空白页；**>1 → 白屏**）；
2. `v-auth` 里的权限串必须能在后端菜单种子的 `permission` 里找到（没挂权限的接口 = 任何已登录用户都能调）；
3. 调用的 API 方法名必须真实存在于 `src/api-services`；
4. `el-date-picker` 一律带 `value-format="YYYY-MM-DD HH:mm:ss"` —— 传真正的 `Date` 会被序列化成带 `Z` 的 UTC，时间区间整体偏 8 小时。

## 品牌（改之前先读）

系统名 / 副标题 / 版权 / 水印 / logo / 版本号由**后端租户配置**下发：

- 种子：`Admin.NET/Admin.NET.Application/SeedData/SysTenantSeedData.cs`（生效的那份；`Core/SeedData` 那份带 `[IgnoreUpdateSeed]`，改它没效果）
- 后台可改：**系统配置**（`/platform/infoSetting`）

登录时 `src/utils/sysInfo.ts` 的 `loadSysInfo()` 会用后端返回值**覆盖** `themeConfig` 里的默认值 ——
所以**只改前端不生效**，`themeConfig.ts` 只是接口失败时的兜底。两处要一起改。

Logo 有两份，内容需保持一致：

| 位置 | 用途 |
|---|---|
| `Admin.NET.Web.Entry/wwwroot/upload/logo.svg` | 后端提供给 `<img>` 的默认 logo（dev 由 Vite 的 `^/[Uu]pload` 代理转发） |
| `src/assets/logo.svg` | 接口失败时前端兜底 |

> 该 svg 在 `.gitignore` 里做了**例外白名单**（`wwwroot/upload/` 整体忽略用户上传物，只放行这一个默认 logo），
> 否则新克隆的仓库 `/upload/logo.svg` 会 404。
