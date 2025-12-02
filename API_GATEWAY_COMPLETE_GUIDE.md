# API Gateway 完整使用指南

**版本：1.0** | **最后更新：2025 年 12 月** | **项目：ApiGateway**

---

## 目录

1. [快速开始](#快速开始)
2. [系统架构](#系统架构)
3. [核心功能](#核心功能)
4. [配置指南](#配置指南)
5. [可观测性（SigNoz）](#可观测性signoz)
6. [认证与授权](#认证与授权)
7. [API 参考](#api-参考)
8. [常见问题](#常见问题)
9. [性能优化](#性能优化)
10. [故障排查](#故障排查)

---

## 快速开始

### 前置条件

- **.NET 10.0 SDK** 或更高版本
- **Docker Desktop** （用于 SigNoz、MySQL、Redis、RabbitMQ）
- **PowerShell 5.1+** (Windows) 或 **Bash** (Linux/macOS)
- **Git** 版本控制

### 安装步骤

#### 1. 克隆仓库

```bash
git clone https://github.com/moretimecode/GateWayfarer.git
cd GateWayfarer
git checkout feature/v0.1
```

#### 2. 启动依赖服务

```bash
# 启动完整的基础设施栈（MySQL、Redis、RabbitMQ）
docker-compose -f docker-compose.otel.yml up -d

# 或启动 SigNoz 可观测性栈
./start-signoz.bat  # Windows
./start-signoz.sh   # Linux/macOS
```

#### 3. 构建项目

```bash
dotnet build Gateway.sln -c Debug
```

#### 4. 运行 API Gateway

```bash
cd src/Api
dotnet run
```

#### 5. 验证运行

```bash
# 检查健康状态
curl http://localhost:5000/health

# 获取路由配置
curl http://localhost:5000/api/routes

# 查看 Swagger 文档
open http://localhost:5000/swagger
```

---

## 系统架构

### 高级架构

```
┌────────────────────────────────────────────────────────────────┐
│                    Client / Consumer                            │
└────────────────────────────────────────────────────────────────┘
                            ↓ HTTP/HTTPS
┌────────────────────────────────────────────────────────────────┐
│                    API GATEWAY (Reverse Proxy)                 │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐      │
│  │ Authentication │ │ Authorization │ │ Route Matching  │      │
│  │  & JWT Auth   │ │  & RBAC       │ │  & Configuration│      │
│  └──────────────┘  └──────────────┘  └─────────────────┘      │
│                                                                 │
│  ┌────────────────┐  ┌──────────────┐  ┌─────────────┐        │
│  │ Protocol Detect │ │ Request Flow  │ │ Health Check│        │
│  │ (HTTP/WebSocket)│ │ & Forwarding  │ │ & Discovery │        │
│  └────────────────┘  └──────────────┘  └─────────────┘        │
│                                                                 │
│  ┌────────────────────────────────────────────────────┐       │
│  │    OpenTelemetry (Traces + Metrics)               │       │
│  │    → SigNoz / Jaeger / Prometheus                │       │
│  └────────────────────────────────────────────────────┘       │
└────────────────────────────────────────────────────────────────┘
    ↓ Forward                                    ↑ Response
┌────────────────────────────────────────────────────────────────┐
│              Backend Services (Microservices)                  │
│  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐                       │
│  │ API1 │  │ API2 │  │ API3 │  │ APIx │                       │
│  └──────┘  └──────┘  └──────┘  └──────┘                       │
└────────────────────────────────────────────────────────────────┘
```

### 分层设计

```
┌─────────────────────────────────────┐
│  API Layer (Controllers)            │ → HTTP endpoints
├─────────────────────────────────────┤
│  Application Layer (Use Cases)      │ → Business logic
├─────────────────────────────────────┤
│  Domain Layer (Entities)            │ → Core models
├─────────────────────────────────────┤
│  Infrastructure Layer (Services)    │ → Implementation
│  - Authentication/Authorization     │
│  - Route Configuration              │
│  - Protocol Detection               │
│  - OpenTelemetry                    │
├─────────────────────────────────────┤
│  Shared Layer (Utilities)           │ → Common helpers
└─────────────────────────────────────┘
```

---

## 核心功能

### 1. 路由管理

**功能**：

- 动态路由配置
- 路径匹配（精确、前缀、正则）
- 路由优先级管理
- 后端服务发现

**数据库表**：`RouteConfiguration`

**示例配置**：

```json
{
  "id": "route-001",
  "path": "/api/users",
  "backendUrl": "http://user-service:8080",
  "methods": ["GET", "POST"],
  "isActive": true,
  "priority": 10,
  "allowAnonymous": false,
  "authorizationPolicy": "authenticated"
}
```

### 2. 认证（Authentication）

**支持方式**：

- JWT Token (Bearer)
- 自定义认证头
- 可配置的密钥/发行者/听众

**配置示例**：

```json
"Authentication": {
  "Enabled": true,
  "TokenHeader": "Authorization",
  "TokenScheme": "Bearer",
  "JwtSecret": "your-secret-key-min-32-characters-long!!!",
  "JwtIssuer": "ApiGateway",
  "JwtAudience": "ApiGatewayClient",
  "TokenExpirationMinutes": 60
}
```

**获取令牌**：

```bash
curl -X POST http://localhost:5000/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "password",
    "expirationMinutes": 60
  }'
```

**使用令牌**：

```bash
curl -H "Authorization: Bearer YOUR_TOKEN" \
  http://localhost:5000/api/protected-route
```

### 3. 授权（Authorization）

**支持类型**：

- 基于角色 (RBAC)
- 基于声明 (Claims)
- 基于范围 (Scopes)
- 自定义策略

**授权策略示例**：

```json
{
  "name": "admin-only",
  "requireAuthentication": true,
  "requiredRoles": ["admin"],
  "requiredClaims": ["department:IT"],
  "allowedScopes": ["api.admin"]
}
```

### 4. 协议检测

**自动识别**：

- HTTP/1.1
- HTTP/2
- WebSocket
- Server-Sent Events (SSE)

### 5. 健康检查

**端点**：`GET /health`

**响应**：

```json
{
  "status": "healthy",
  "timestamp": "2025-12-02T10:30:00Z",
  "services": {
    "database": "healthy",
    "cache": "healthy",
    "messageQueue": "healthy"
  }
}
```

---

## 配置指南

### appsettings.json 配置

#### 连接字符串

```json
"ConnectionStrings": {
  "DBConnection": "Server=localhost;Port=3306;Database=ApiGatewayDb;Uid=root;Pwd=myrootpass;CharSet=utf8mb4;",
  "RawRabbit": "amqp://admin:admin123@localhost:5672/"
}
```

#### 认证配置

```json
"ApiGateway": {
  "Authentication": {
    "Enabled": true,
    "TokenHeader": "Authorization",
    "TokenScheme": "Bearer",
    "JwtSecret": "your-secret-key-min-32-characters-long!!!",
    "JwtIssuer": "ApiGateway",
    "JwtAudience": "ApiGatewayClient",
    "TokenExpirationMinutes": 60,
    "DefaultAuthorizationPolicy": "public"
  }
}
```

#### 健康检查配置

```json
"HealthCheck": {
  "Enabled": true,
  "DefaultPath": "/health",
  "Interval": "00:00:05",
  "Timeout": "00:00:05",
  "ThreadPoolStrategy": "SharedPool",
  "MaxConcurrency": 20
}
```

#### OpenTelemetry 配置

```json
"OpenTelemetry": {
  "EnableTraces": true,
  "EnableMetrics": true,
  "SigNozEnabled": true,
  "SigNozOtlpEndpoint": "http://localhost:4317",
  "JaegerEnabled": true,
  "JaegerHost": "localhost",
  "JaegerPort": 6831,
  "PrometheusEnabled": true,
  "PrometheusPort": 9090,
  "PrometheusScrapeCacheDuration": 3000
}
```

### 数据库初始化

```bash
# 应用数据库迁移
cd src/Infrastructure
dotnet ef database update

# 查看迁移历史
dotnet ef migrations list
```

### 环境变量覆盖

```bash
# Linux/macOS
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DBConnection="Server=prod-db;..."

# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__DBConnection = "Server=prod-db;..."

# 启动应用
dotnet run
```

---

## 可观测性（SigNoz）

### 快速启动

```bash
# Windows
.\start-signoz.bat

# Linux/macOS
chmod +x start-signoz.sh
./start-signoz.sh
```

### 访问 SigNoz UI

```
http://localhost:3000
```

### 分布式追踪（Traces）

**查看步骤**：

1. 打开 SigNoz UI
2. 导航到 **Traces** 标签
3. 选择服务 **ApiGateway**
4. 查看请求链路

**链路包含信息**：

- 完整请求路径
- 每个操作的耗时
- 错误和异常
- 自定义标签和属性

**示例链路**：

```
GET /api/routes (totla: 245ms)
├── AuthenticationMiddleware (30ms)
├── AuthorizationMiddleware (20ms)
├── RouteMatchingMiddleware (45ms)
├── BackendForwarding (140ms)
│   ├── DNS Resolution (5ms)
│   ├── TCP Connection (10ms)
│   └── HTTP Request (125ms)
└── ResponseProcessing (10ms)
```

### 性能指标（Metrics）

**关键指标**：

- HTTP 请求数量 (req/sec)
- 请求延迟分布 (p50, p95, p99)
- 错误率 (error %)
- 吞吐量 (requests/min)
- CPU 使用率
- 内存使用率

### 日志查询（Logs）

```
# 查询所有认证失败的请求
status_code:401 service_name:ApiGateway

# 查询特定时间范围的错误
status_code:>=500 timestamp:[2025-12-02T00:00:00Z TO 2025-12-02T23:59:59Z]

# 查询特定路由的请求
http_url:"/api/routes"
```

### 告警配置

**创建告警步骤**：

1. SigNoz UI → **Alerts** → **Create Alert**
2. 选择指标，例如：`HTTP Request Duration (p95) > 500ms`
3. 设置评估周期：`1 minute`
4. 设置通知渠道（Slack、钉钉等）

**常见告警规则**：

```
规则1: 错误率 > 5%
规则2: 平均响应时间 > 500ms
规则3: 数据库连接失败 > 0
规则4: 内存使用率 > 80%
规则5: CPU 使用率 > 90%
```

### 自定义仪表板

**创建仪表板**：

1. **Dashboards** → **Create Dashboard**
2. 添加面板：
   - 请求吞吐量
   - 错误率趋势
   - 延迟分布
   - 服务健康状态

---

## 认证与授权

### 认证流程

```
┌─────────────┐
│ Client      │
└──────┬──────┘
       │ 1. POST /auth/token
       ↓
┌─────────────────────────────┐
│ API Gateway - Auth Handler  │
│ - 验证用户名/密码          │
│ - 生成 JWT Token           │
└──────┬──────────────────────┘
       │ 2. Return JWT Token
       ↓
┌─────────────┐
│ Client      │ ← Token: eyJhbGc...
└──────┬──────┘
       │ 3. Send request with Authorization header
       ↓
┌─────────────────────────────┐
│ API Gateway - Middleware    │
│ - 提取 Token               │
│ - 验证签名                │
│ - 验证过期时间            │
└──────┬──────────────────────┘
       │ 4a. Valid          4b. Invalid
       ↓                    ↓
    Continue           Return 401
```

### 授权流程

```
┌──────────────────────┐
│ Authenticated User   │
└──────────┬───────────┘
           │
           ↓
┌──────────────────────────────┐
│ Authorization Service        │
│ - 检查角色 (RBAC)           │
│ - 检查声明 (Claims)         │
│ - 检查范围 (Scopes)         │
└──────┬───────────────────────┘
       │
       ├─→ Policy: "public"
       │   ├─ RequireAuthentication = false
       │   └─ ✓ Allow
       │
       ├─→ Policy: "authenticated"
       │   ├─ RequireAuthentication = true
       │   └─ ✓ Allow (if authenticated)
       │
       ├─→ Policy: "admin-only"
       │   ├─ RequiredRoles = ["admin"]
       │   ├─ Check user roles
       │   └─ Allow if has "admin" role
       │
       └─→ Policy: "email-verified"
           ├─ RequiredClaims = ["email_verified"]
           ├─ Check user claims
           └─ Allow if claim exists
```

### API 端点

#### 获取令牌

```http
POST /auth/token
Content-Type: application/json

{
  "username": "admin",
  "password": "admin123",
  "expirationMinutes": 60
}
```

**响应 (200 OK)**：

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600,
  "tokenType": "Bearer"
}
```

#### 验证令牌

```http
POST /auth/verify
Content-Type: application/json

{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**响应 (200 OK)**：

```json
{
  "isValid": true,
  "userId": "user-123",
  "username": "admin",
  "roles": ["admin", "user"],
  "claims": {
    "email": "admin@example.com",
    "department": "IT"
  }
}
```

---

## API 参考

### 路由管理 API

#### 获取所有路由配置

```http
GET /api/routes
Authorization: Bearer YOUR_TOKEN
```

**响应**：

```json
{
  "routes": [
    {
      "id": "route-001",
      "path": "/api/users",
      "backendUrl": "http://user-service:8080",
      "methods": ["GET", "POST"],
      "isActive": true,
      "priority": 10,
      "allowAnonymous": false,
      "authorizationPolicy": "authenticated"
    }
  ]
}
```

#### 创建新路由

```http
POST /api/routes
Authorization: Bearer YOUR_TOKEN
Content-Type: application/json

{
  "path": "/api/products",
  "backendUrl": "http://product-service:8080",
  "methods": ["GET", "POST", "PUT", "DELETE"],
  "isActive": true,
  "priority": 20,
  "allowAnonymous": true,
  "authorizationPolicy": "public"
}
```

#### 更新路由

```http
PUT /api/routes/{id}
Authorization: Bearer YOUR_TOKEN
Content-Type: application/json

{
  "path": "/api/products/v2",
  "backendUrl": "http://product-service-v2:8080",
  "isActive": true
}
```

#### 删除路由

```http
DELETE /api/routes/{id}
Authorization: Bearer YOUR_TOKEN
```

### 健康检查 API

```http
GET /health
```

**响应**：

```json
{
  "status": "healthy",
  "timestamp": "2025-12-02T10:30:00Z",
  "services": {
    "database": {
      "status": "healthy",
      "responseTime": 15
    },
    "cache": {
      "status": "healthy",
      "responseTime": 5
    },
    "messageQueue": {
      "status": "healthy",
      "responseTime": 20
    }
  }
}
```

### Swagger API 文档

```
http://localhost:5000/swagger
```

---

## 常见问题

### Q1: 如何修改 JWT 密钥长度要求？

**A**: 编辑 `appsettings.json` 中的 `JwtSecret`，最少 32 字符。

```json
"JwtSecret": "your-very-long-secret-key-with-at-least-32-characters-for-security!"
```

### Q2: 如何跳过认证进行本地测试？

**A**: 在 `appsettings.json` 中禁用认证：

```json
"Authentication": {
  "Enabled": false
}
```

### Q3: 如何添加自定义声明到 JWT？

**A**: 修改 `JwtAuthenticationService.cs`：

```csharp
var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, userId),
    new Claim(ClaimTypes.Name, username),
    new Claim(ClaimTypes.Email, email),
    // 添加自定义声明
    new Claim("department", "IT"),
    new Claim("role", "admin")
};
```

### Q4: SigNoz 数据保留多久？

**A**: 默认 30 天。可在 ClickHouse 配置中修改 TTL：

编辑 `signoz-otel-collector-config.yaml` 中的 Exporter 设置。

### Q5: 如何导出路由配置？

**A**: 使用 API 导出：

```bash
curl http://localhost:5000/api/routes > routes-backup.json
```

### Q6: 如何处理 CORS 跨域请求？

**A**: 编辑 `Startup.cs` 的 CORS 配置：

```csharp
services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
```

---

## 性能优化

### 1. 缓存优化

```json
"Caching": {
  "Enabled": true,
  "Provider": "Redis",
  "DefaultTTL": 300,
  "MaxCacheSize": 1000
}
```

**缓存路由配置**：

```csharp
var routes = _cache.GetOrSet("routes",
    () => _repository.GetAllRoutes(),
    TimeSpan.FromMinutes(5));
```

### 2. 连接池优化

```json
"ConnectionPool": {
  "MinSize": 10,
  "MaxSize": 100,
  "IdleTimeout": 300,
  "MaxRetries": 3
}
```

### 3. 请求批处理

配置 OTel Collector 的批处理器：

```yaml
processors:
  batch:
    send_batch_size: 1000
    timeout: 10s
```

### 4. 异步处理

使用 async/await 处理请求：

```csharp
public async Task HandleAsync(HttpContext context)
{
    var route = await _repository.GetRouteAsync(path);
    await _forwarder.ForwardAsync(route, context);
}
```

### 5. 负载均衡

配置多个后端实例：

```json
{
  "path": "/api/users",
  "backendUrls": [
    "http://user-service-1:8080",
    "http://user-service-2:8080",
    "http://user-service-3:8080"
  ],
  "loadBalancingStrategy": "RoundRobin"
}
```

### 性能基准测试

```bash
# 使用 Apache Bench 测试
ab -n 10000 -c 100 http://localhost:5000/api/routes

# 使用 wrk 测试
wrk -t 12 -c 400 -d 30s http://localhost:5000/api/routes
```

**预期结果**：

- 吞吐量：> 1000 req/s
- P95 延迟：< 50ms
- P99 延迟：< 100ms

---

## 故障排查

### 问题 1: 应用启动失败

**症状**：`dotnet run` 后应用立即崩溃

**排查步骤**：

```bash
# 1. 检查 .NET 版本
dotnet --version  # 应该 >= 10.0

# 2. 检查依赖项
dotnet restore

# 3. 清理并重新构建
dotnet clean
dotnet build

# 4. 查看详细错误信息
dotnet run --verbosity diagnostic
```

### 问题 2: 数据库连接失败

**症状**：`MySQL connection refused`

**排查步骤**：

```bash
# 1. 检查 MySQL 是否运行
docker ps | grep mysql

# 2. 验证连接字符串
# appsettings.json 中的 DBConnection

# 3. 检查数据库是否存在
docker exec -it <mysql-container> mysql -uroot -pmyrootpass -e "SHOW DATABASES;"

# 4. 创建数据库
docker exec -it <mysql-container> mysql -uroot -pmyrootpass -e "CREATE DATABASE ApiGatewayDb;"
```

### 问题 3: 认证令牌过期

**症状**：`401 Unauthorized`

**排查步骤**：

```bash
# 1. 获取新令牌
curl -X POST http://localhost:5000/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}'

# 2. 验证令牌
curl -H "Authorization: Bearer YOUR_NEW_TOKEN" \
  http://localhost:5000/api/routes

# 3. 检查 TokenExpirationMinutes 设置
# appsettings.json
```

### 问题 4: 路由无法匹配

**症状**：`404 Not Found`

**排查步骤**：

```bash
# 1. 列出所有路由
curl http://localhost:5000/api/routes

# 2. 检查路由是否激活
# isActive: true

# 3. 验证请求 URL
# 确保完全匹配配置的 path

# 4. 检查路由优先级
# 按 priority 降序匹配
```

### 问题 5: SigNoz 无法接收追踪

**症状**：SigNoz UI 中无追踪数据

**排查步骤**：

```bash
# 1. 检查 OTel Collector 状态
docker ps | grep otel-collector

# 2. 查看 Collector 日志
docker logs signoz-otel-collector

# 3. 验证应用连接
curl http://localhost:14250

# 4. 检查配置
# appsettings.json 中的 OpenTelemetry

# 5. 重启 Collector
docker-compose -f docker-compose.signoz.yml restart signoz-otel-collector
```

### 问题 6: 高内存占用

**症状**：内存使用率持续增长

**排查步骤**：

```bash
# 1. 检查内存泄漏
dotnet trace collect -p <PID> --duration 30s

# 2. 分析堆转储
dotnet dump collect -p <PID>
dotnet dump analyze core_*.dmp

# 3. 减少缓存大小
"MaxCacheSize": 500  // 降低缓存限制

# 4. 启用垃圾回收
DOTNET_gcServer=true
```

---

## 开发指南

### 项目结构

```
ApiGateway/
├── src/
│   ├── Api/                          # Web API 项目
│   │   ├── Controllers/              # API 端点
│   │   ├── Middleware/               # 中间件
│   │   ├── Startup.cs               # 启动配置
│   │   └── Program.cs               # 主入口
│   ├── Application/                 # 应用层（用例）
│   ├── Domain/                      # 领域层（实体）
│   │   ├── Routes/                 # 路由配置实体
│   │   ├── Authentication/         # 认证实体
│   │   └── Services/               # 领域服务
│   ├── Infrastructure/              # 基础设施层
│   │   ├── Services/               # 具体实现
│   │   ├── OpenTelemetry/          # 可观测性
│   │   ├── Database/               # 数据访问
│   │   ├── Configuration/          # EF 配置
│   │   └── Endpoints/              # 端点配置
│   └── Shared/                     # 共享工具
├── tests/
│   ├── Tests.Integration/          # 集成测试
│   │   ├── P0_CoreFunctionality/  # 核心功能测试
│   │   └── P1_Advanced/           # 高级功能测试
│   └── Tests.Unit/                # 单元测试
├── docker-compose.signoz.yml       # SigNoz 编排
├── docker-compose.otel.yml         # 基础设施编排
└── Gateway.sln                     # 解决方案文件
```

### 运行测试

```bash
# 运行所有测试
dotnet test Gateway.sln

# 运行特定项目的测试
dotnet test tests/Tests.Integration/Tests.Integration.csproj

# 运行特定测试类
dotnet test --filter "FullyQualifiedName~AuthenticationTests"

# 查看代码覆盖率
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## 生产部署

### 发布构建

```bash
# 发布为自包含应用
dotnet publish -c Release -o ./publish \
  --self-contained -r linux-x64

# 创建 Docker 镜像
docker build -f src/Api/Dockerfile -t apigateway:1.0 .

# 推送到镜像仓库
docker tag apigateway:1.0 myregistry/apigateway:1.0
docker push myregistry/apigateway:1.0
```

### Kubernetes 部署

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: api-gateway
spec:
  replicas: 3
  selector:
    matchLabels:
      app: api-gateway
  template:
    metadata:
      labels:
        app: api-gateway
    spec:
      containers:
        - name: gateway
          image: myregistry/apigateway:1.0
          ports:
            - containerPort: 5000
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: ConnectionStrings__DBConnection
              valueFrom:
                secretKeyRef:
                  name: db-secret
                  key: connection-string
          livenessProbe:
            httpGet:
              path: /health
              port: 5000
            initialDelaySeconds: 30
            periodSeconds: 10
          readinessProbe:
            httpGet:
              path: /health
              port: 5000
            initialDelaySeconds: 10
            periodSeconds: 5
```

---

## 贡献指南

1. Fork 仓库
2. 创建功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'Add amazing feature'`)
4. 推送分支 (`git push origin feature/amazing-feature`)
5. 创建 Pull Request

---

## 许可证

MIT License - 详见 LICENSE 文件

---

## 支持和反馈

- 📧 Email: support@example.com
- 🐛 Issues: https://github.com/moretimecode/GateWayfarer/issues
- 💬 Discussions: https://github.com/moretimecode/GateWayfarer/discussions

---

**最后更新**：2025 年 12 月 2 日 | **版本**：1.0.0
