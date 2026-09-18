# docker/

> ⚠️ 框架自带的**部署脚手架已删除**：`docker-compose.yml`、`docker-compose-builder.yml`、
> `build.sh`、`app/`、`mysql/`、`nginx/`、`.env.production`。
> 它们是 Admin.NET 原版部署（MySQL 5.7、`.NET 9` 路径、tdengine、minio），
> 与本项目（net8.0 + PostgreSQL）不符，且 `build.sh` 会用 `docker/.env.production`
> **覆盖 `Web/.env.production`**。需要时从 git 历史取回。

## 本目录保留的内容

`docker-compose.pg.yml` —— 本地开发用 PostgreSQL 16 容器（`paymentcenter-pg`，仅绑 `127.0.0.1:55432`）。

```bash
docker compose -f docker/docker-compose.pg.yml up -d     # 启动
docker compose -f docker/docker-compose.pg.yml down      # 停止
docker compose -f docker/docker-compose.pg.yml down -v   # 清库
```

> ⚠️ 本项目当前实际连的是**远程开发库**（`8.219.79.35:38539/payment`）。
> 连库的**唯一来源**是 `Admin.NET/Admin.NET.Application/Configuration/Database.json`。
> 本容器只在需要离线 / 隔离验证时才用，用之前记得同步改那份配置。
