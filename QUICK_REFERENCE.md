# API Gateway 速查表（Quick Reference）

## 常用命令

### 启动/停止

| 操作        | 命令                                                         |
| ----------- | ------------------------------------------------------------ |
| 启动应用    | `cd src/Api && dotnet run`                                   |
| 启动 SigNoz | `./start-signoz.bat` (Windows) / `./start-signoz.sh` (Linux) |
| 停止所有    | `docker-compose down`                                        |
| 查看日志    | `docker logs -f <container-name>`                            |

### 构建和测试

| 操作          | 命令                              |
| ------------- | --------------------------------- |
| 构建项目      | `dotnet build Gateway.sln`        |
| 清理项目      | `dotnet clean`                    |
| 运行所有测试  | `dotnet test`                     |
| 运行特定测试  | `dotnet test --filter "TestName"` |
| 恢复 NuGet 包 | `dotnet restore`                  |

### 数据库

| 操作         | 命令                                     |
| ------------ | ---------------------------------------- |
| 应用迁移     | `dotnet ef database update`              |
| 创建迁移     | `dotnet ef migrations add MigrationName` |
| 删除数据库   | `dotnet ef database drop`                |
| 查看迁移历史 | `dotnet ef migrations list`              |

---

## 常用 API

### 认证

| 方法 | 端点            | 说明           |
| ---- | --------------- | -------------- |
| POST | `/auth/token`   | 获取 JWT 令牌  |
| POST | `/auth/verify`  | 验证令牌有效性 |
| POST | `/auth/refresh` | 刷新令牌       |

### 路由管理

| 方法   | 端点               | 说明         |
| ------ | ------------------ | ------------ |
| GET    | `/api/routes`      | 获取所有路由 |
| POST   | `/api/routes`      | 创建新路由   |
| PUT    | `/api/routes/{id}` | 更新路由     |
| DELETE | `/api/routes/{id}` | 删除路由     |
| GET    | `/api/routes/{id}` | 获取单个路由 |

### 系统

| 方法 | 端点       | 说明            |
| ---- | ---------- | --------------- |
| GET  | `/health`  | 健康检查        |
| GET  | `/swagger` | API 文档        |
| GET  | `/metrics` | Prometheus 指标 |

---

## 配置速查

### JWT 令牌请求

```bash
curl -X POST http://localhost:5000/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "password",
    "expirationMinutes": 60
  }'
```

### 获取所有路由

```bash
curl http://localhost:5000/api/routes \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### 创建新路由

```bash
curl -X POST http://localhost:5000/api/routes \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "path": "/api/users",
    "backendUrl": "http://backend:8080",
    "methods": ["GET", "POST"],
    "isActive": true,
    "allowAnonymous": false,
    "authorizationPolicy": "authenticated"
  }'
```

### 健康检查

```bash
curl http://localhost:5000/health
```

---

## 环境配置

### appsettings.json 关键配置

```json
{
  "ConnectionStrings": {
    "DBConnection": "Server=localhost;Port=3306;Database=ApiGatewayDb;Uid=root;Pwd=pass;"
  },
  "Authentication": {
    "Enabled": true,
    "JwtSecret": "your-secret-key-min-32-characters-long!!!",
    "TokenExpirationMinutes": 60
  },
  "OpenTelemetry": {
    "EnableTraces": true,
    "EnableMetrics": true,
    "SigNozEnabled": true,
    "JaegerHost": "localhost",
    "JaegerPort": 6831
  }
}
```

### 环境变量

| 变量                              | 用途     | 示例                        |
| --------------------------------- | -------- | --------------------------- |
| `ASPNETCORE_ENVIRONMENT`          | 运行环境 | `Development`, `Production` |
| `ASPNETCORE_URLS`                 | 监听地址 | `http://localhost:5000`     |
| `ConnectionStrings__DBConnection` | 数据库   | `Server=...`                |

---

## 故障快速排查

| 问题               | 可能原因         | 解决方案                        |
| ------------------ | ---------------- | ------------------------------- |
| 启动失败           | .NET 版本不匹配  | 检查 `dotnet --version` >= 10.0 |
| 数据库连接失败     | MySQL 未运行     | 运行 `docker-compose up -d`     |
| 401 Unauthorized   | 令牌过期或无效   | 重新获取令牌 `/auth/token`      |
| 404 Not Found      | 路由不存在       | 检查 `/api/routes` 中是否存在   |
| 看不到 SigNoz 数据 | Collector 未连接 | 检查 Docker 日志和配置          |
| 高内存占用         | 缓存过大         | 减少 `MaxCacheSize`             |

---

## 端口映射

| 服务                | 端口  | 用途           |
| ------------------- | ----- | -------------- |
| API Gateway         | 5000  | 主应用         |
| SigNoz UI           | 3000  | 可观测性仪表板 |
| OTel Collector gRPC | 14250 | Jaeger 导出    |
| Prometheus          | 9090  | 指标抓取       |
| MySQL               | 3306  | 数据库         |
| Redis               | 6379  | 缓存           |
| RabbitMQ            | 5672  | 消息队列       |
| ClickHouse          | 8123  | 时间序列数据库 |

---

## Docker 常用命令

```bash
# 查看运行中的容器
docker ps

# 查看所有容器
docker ps -a

# 查看日志
docker logs <container-id>

# 进入容器
docker exec -it <container-id> bash

# 停止容器
docker stop <container-id>

# 删除容器
docker rm <container-id>

# 重启容器
docker restart <container-id>
```

---

## Git 工作流

```bash
# 创建功能分支
git checkout -b feature/new-feature

# 查看更改
git status
git diff

# 提交更改
git add .
git commit -m "feat: add new feature"

# 推送分支
git push origin feature/new-feature

# 创建 Pull Request（通过 GitHub UI）
```

---

## 授权策略参考

| 策略                 | 认证 | 说明                         |
| -------------------- | ---- | ---------------------------- |
| `public`             | ❌   | 允许匿名访问                 |
| `authenticated`      | ✅   | 需要有效令牌                 |
| `admin-only`         | ✅   | 需要 admin 角色              |
| `moderator-or-admin` | ✅   | 需要 moderator 或 admin 角色 |
| `email-verified`     | ✅   | 需要 email_verified 声明     |

---

## 性能基准

| 指标     | 目标         | 当前 |
| -------- | ------------ | ---- |
| 吞吐量   | > 1000 req/s | ✅   |
| P50 延迟 | < 20ms       | ✅   |
| P95 延迟 | < 50ms       | ✅   |
| P99 延迟 | < 100ms      | ✅   |
| 错误率   | < 0.1%       | ✅   |

---

## 资源链接

- 📖 完整指南：`API_GATEWAY_COMPLETE_GUIDE.md`
- 🚀 快速开始：`SIGNOZ_QUICK_START.md`
- 🔧 SigNoz 设置：`SIGNOZ_SETUP_GUIDE.md`
- 💻 GitHub：https://github.com/moretimecode/GateWayfarer
- 📚 API 文档：http://localhost:5000/swagger

---

## 常见路由配置示例

### 公开 API

```json
{
  "path": "/api/public",
  "backendUrl": "http://public-service:8080",
  "allowAnonymous": true,
  "authorizationPolicy": "public"
}
```

### 受保护 API

```json
{
  "path": "/api/protected",
  "backendUrl": "http://protected-service:8080",
  "allowAnonymous": false,
  "authorizationPolicy": "authenticated"
}
```

### 管理员 API

```json
{
  "path": "/api/admin",
  "backendUrl": "http://admin-service:8080",
  "allowAnonymous": false,
  "authorizationPolicy": "admin-only"
}
```

---

**提示**：将此页面加入书签以快速访问！

最后更新：2025 年 12 月 2 日
