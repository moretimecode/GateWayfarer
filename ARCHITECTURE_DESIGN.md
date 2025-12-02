# API Gateway 架构设计文档

## 1. 整体设计

### 1.1 系统目标

API Gateway 是一个企业级反向代理和 API 管理系统，提供：

- ✅ **集中管理** - 统一管理多个后端服务的路由
- ✅ **安全认证** - JWT 令牌和细粒度授权控制
- ✅ **协议转换** - 支持 HTTP/1.1、HTTP/2、WebSocket
- ✅ **可观测性** - 完整的分布式追踪和性能监控
- ✅ **高可用** - 支持负载均衡和故障转移
- ✅ **灵活配置** - 动态路由无需重启应用

### 1.2 核心价值主张

```
用户请求
    ↓
┌─────────────────────────────────┐
│     API Gateway                 │
│  ┌─────────────────────────┐   │
│  │ • 统一认证授权          │   │  减少后端服务成本
│  │ • 动态路由管理          │   │  提高系统可靠性
│  │ • 性能监控告警          │   │  加快开发速度
│  │ • 协议适配转换          │   │
│  └─────────────────────────┘   │
└─────────────────────────────────┘
    ↓
┌─────────┬─────────┬─────────┐
│ 服务 1  │ 服务 2  │ 服务 3  │  无需关心网关
└─────────┴─────────┴─────────┘
```

---

## 2. 分层架构

### 2.1 四层模型

```
┌─────────────────────────────────────────┐
│  Presentation Layer (API)               │
│  - Controllers                          │
│  - HTTP Endpoints                       │
│  - Swagger Documentation                │
└─────────┬───────────────────────────────┘
          │ HTTP Requests/Responses
┌─────────▼───────────────────────────────┐
│  Application Layer (Use Cases)          │
│  - Route Handler                        │
│  - Authentication Handler               │
│  - Authorization Handler                │
│  - Protocol Detector                    │
│  - Request Forwarder                    │
└─────────┬───────────────────────────────┘
          │ Business Logic
┌─────────▼───────────────────────────────┐
│  Domain Layer (Entities & Services)     │
│  - RouteConfigItem                      │
│  - AuthPolicy                           │
│  - AuthenticationOptions                │
│  - JwtAuthenticationService             │
│  - AuthorizationService                 │
└─────────┬───────────────────────────────┘
          │ Core Models
┌─────────▼───────────────────────────────┐
│  Infrastructure Layer (Implementation)  │
│  - Database Access (EF Core)            │
│  - Cache (Redis)                        │
│  - Message Queue (RabbitMQ)             │
│  - OpenTelemetry Integration            │
│  - External Service Calls               │
└─────────┬───────────────────────────────┘
          │
┌─────────▼───────────────────────────────┐
│  Persistence & External Systems        │
│  - MySQL Database                       │
│  - Redis Cache                          │
│  - RabbitMQ Queue                       │
│  - SigNoz Observability                 │
│  - Backend Services                     │
└─────────────────────────────────────────┘
```

### 2.2 请求生命周期

```
1. 入口
   ↓
┌─────────────────────────────┐
│ HttpContext 创建             │ → ASP.NET Core 原生
└─────────────┬───────────────┘
              ↓
2. 中间件管道
   ↓
┌─────────────────────────────┐
│ CORS 中间件                 │
└─────────────┬───────────────┘
              ↓
┌─────────────────────────────┐
│ 认证中间件                  │
│ - 提取 Token               │
│ - 验证签名                │
│ - 创建 ClaimsPrincipal    │
└─────────────┬───────────────┘
              ↓
┌─────────────────────────────┐
│ OpenTelemetry 中间件       │
│ - 开始追踪                │
│ - 记录请求信息            │
└─────────────┬───────────────┘
              ↓
3. 路由匹配
   ↓
┌─────────────────────────────┐
│ Route Matching              │
│ - 精确匹配                │
│ - 前缀匹配                │
│ - 正则匹配                │
└─────────────┬───────────────┘
              ↓
4. 授权检查
   ↓
┌─────────────────────────────┐
│ Authorization Check         │
│ - 验证角色                │
│ - 验证声明                │
│ - 验证范围                │
└─────────────┬───────────────┘
              ├─ Failed → 403 Forbidden
              ↓
5. 协议检测
   ↓
┌─────────────────────────────┐
│ Protocol Detection          │
│ - HTTP/1.1                │
│ - HTTP/2                  │
│ - WebSocket               │
│ - SSE                     │
└─────────────┬───────────────┘
              ↓
6. 请求转发
   ↓
┌─────────────────────────────┐
│ Forward to Backend          │
│ - 建立连接                │
│ - 转发请求                │
│ - 流式响应                │
└─────────────┬───────────────┘
              ↓
7. 响应处理
   ↓
┌─────────────────────────────┐
│ Process Response            │
│ - 记录响应信息            │
│ - 关闭连接                │
└─────────────┬───────────────┘
              ↓
8. 追踪结束
   ↓
┌─────────────────────────────┐
│ End Trace                   │
│ - 发送到 SigNoz           │
│ - 记录完整信息            │
└─────────────┬───────────────┘
              ↓
          返回响应
```

---

## 3. 核心模块设计

### 3.1 认证模块 (Authentication)

```
JWT 生成流程：
┌─────────────┐
│ 用户凭证     │
│ username    │
│ password    │
└──────┬──────┘
       │
       ↓
┌────────────────────────────┐
│ JwtAuthenticationService   │
│                            │
│ GenerateToken:             │
│ 1. 验证凭证              │
│ 2. 创建 Claims          │
│ 3. 签名 Token           │
│ 4. 返回 JWT             │
└──────┬─────────────────────┘
       │
       ↓
┌────────────────────────────┐
│ JWT Token                  │
│ {                          │
│   "alg": "HS256",         │
│   "typ": "JWT"            │
│ }                          │
│ {                          │
│   "sub": "user-123",      │
│   "name": "admin",        │
│   "roles": ["admin"],     │
│   "exp": 1735802400       │
│ }                          │
│ [signature]               │
└────────────────────────────┘

验证流程：
┌─────────────┐
│ JWT Token   │
└──────┬──────┘
       │
       ↓
┌────────────────────────────┐
│ VerifyToken                │
│                            │
│ 1. 提取 Header/Payload    │
│ 2. 验证签名              │
│ 3. 检查过期时间          │
│ 4. 返回验证结果          │
└──────┬─────────────────────┘
       │
       ├─ 有效 → ClaimsPrincipal
       └─ 无效 → AuthenticationResult (Error)
```

**关键实现**：

- `JwtAuthenticationService.GenerateToken()`
- `JwtAuthenticationService.VerifyToken()`
- `AuthenticationMiddleware`

### 3.2 授权模块 (Authorization)

```
授权决策树：
┌──────────────────┐
│ ClaimsPrincipal  │
│ (Authenticated   │
│  User)           │
└────────┬─────────┘
         │
         ↓
┌──────────────────────────────────┐
│ Authorization Policy             │
│ {                                │
│   RequireAuthentication: true/f  │
│   RequiredRoles: [...]          │
│   RequiredClaims: [...]         │
│   AllowedScopes: [...]          │
│ }                                │
└────────┬───────────────────────────┘
         │
         ↓
┌──────────────────────────────────┐
│ AuthorizationService             │
│                                  │
│ CheckAuthorization:              │
│                                  │
│ if RequireAuthentication:        │
│   ├─ User authenticated? YES/NO │
│   │                             │
│   if RequiredRoles:             │
│   ├─ User has role? YES/NO      │
│   │                             │
│   if RequiredClaims:            │
│   ├─ User has claim? YES/NO     │
│   │                             │
│   if AllowedScopes:             │
│   └─ User has scope? YES/NO     │
│                                  │
└────────┬───────────────────────────┘
         │
         ├─ All passed → Authorized (✓)
         └─ Any failed → Unauthorized (✗)
```

**关键实现**：

- `AuthorizationService.CheckAuthorization()`
- `AuthPolicy` 实体
- `AuthorizationMiddleware`

### 3.3 路由匹配模块 (Route Matching)

```
匹配算法：
┌─────────────┐
│ 请求 URL    │
│ /api/users  │
└──────┬──────┘
       │
       ↓
┌────────────────────────────────┐
│ 从数据库加载所有路由           │
│ (缓存 5 分钟)                 │
│                                │
│ Route 1: /api/users           │
│ Route 2: /api/*/details       │
│ Route 3: /api/.*              │
│ ...                            │
└────────┬───────────────────────┘
         │
         ↓
┌────────────────────────────────┐
│ 按优先级排序                    │
│ (Priority DESC)                │
│                                │
│ Priority 100: /api/users      │
│ Priority 50:  /api/*/details  │
│ Priority 10:  /api/.*         │
└────────┬───────────────────────┘
         │
         ↓
┌────────────────────────────────┐
│ 逐一匹配                        │
│                                │
│ /api/users                     │
│   ├─ 精确匹配 /api/users ✓   │
│   └─ 返回 Route 1             │
└────────────────────────────────┘
```

**匹配类型**：

1. **精确匹配** (Exact): `/api/users` == `/api/users`
2. **前缀匹配** (Prefix): `/api/users/*` matches `/api/users/123`
3. **正则匹配** (Regex): `/api/[a-z]+` matches `/api/users`

**关键实现**：

- `RouteConfigItem` 实体
- `RouteMatchingHandler`
- 路由缓存机制

### 3.4 协议检测模块 (Protocol Detection)

```
协议识别流程：
┌──────────────┐
│ HttpContext  │
└───────┬──────┘
        │ Protocol, Headers, Body
        ↓
┌────────────────────────────────┐
│ HttpContextProtocolDetector    │
│                                │
│ 检查：                         │
│ 1. Connection: upgrade?       │
│ 2. Upgrade: websocket?        │
│ 3. Accept: text/event-stream? │
│ 4. Protocol Version?          │
└────────┬───────────────────────┘
         │
         ├─ WebSocket → WSHandler
         ├─ SSE → SSEHandler
         ├─ HTTP/2 → HTTP2Handler
         └─ HTTP/1.1 → HTTP1Handler
```

**支持协议**：

- HTTP/1.1 (标准 REST API)
- HTTP/2 (多路复用)
- WebSocket (双向通信)
- Server-Sent Events (服务端推送)

### 3.5 可观测性模块 (OpenTelemetry)

```
遥测数据流：
┌──────────────────────┐
│ 应用事件             │
│ - 请求开始          │
│ - 处理中            │
│ - 响应完成          │
└──────────┬───────────┘
           │
           ↓
┌──────────────────────────────┐
│ OpenTelemetry SDK           │
│                             │
│ Traces:                     │
│ - 创建 Activity            │
│ - 添加标签和事件           │
│ - 记录异常                │
│                             │
│ Metrics:                    │
│ - 计数器 (Counter)        │
│ - 仪表 (Gauge)            │
│ - 直方图 (Histogram)      │
└──────────┬──────────────────┘
           │
           ↓
┌──────────────────────────────┐
│ Exporter (Jaeger)           │
│                             │
│ 批处理 + 压缩               │
│ Batch size: 1000            │
│ Timeout: 10s                │
└──────────┬──────────────────┘
           │
           ↓
┌──────────────────────────────┐
│ OTel Collector              │
│                             │
│ - 接收 Jaeger 数据         │
│ - 处理和转换               │
│ - 导出到 ClickHouse       │
│ - 发送到 Kafka             │
└──────────┬──────────────────┘
           │
           ├──→ ClickHouse (存储)
           └──→ Kafka (流处理)
                    │
                    ↓
            ┌──────────────────┐
            │ SigNoz           │
            │ - 追踪查询       │
            │ - 指标分析       │
            │ - 告警规则       │
            │ - Web UI         │
            └──────────────────┘
```

**收集的数据**：

- Spans (追踪)
- Metrics (指标)
- Logs (日志)
- Traces context (上下文传播)

---

## 4. 数据模型

### 4.1 路由配置 (RouteConfiguration)

```csharp
public class RouteConfigItem
{
    public string Id { get; set; }                    // 唯一标识
    public string Path { get; set; }                  // URL 路径
    public string BackendUrl { get; set; }            // 后端服务地址
    public List<string> Methods { get; set; }         // HTTP 方法
    public int Priority { get; set; }                 // 优先级
    public bool IsActive { get; set; }                // 是否激活
    public bool AllowAnonymous { get; set; }          // 允许匿名
    public string AuthorizationPolicy { get; set; }   // 授权策略
    public DateTime CreatedAt { get; set; }           // 创建时间
    public DateTime? UpdatedAt { get; set; }          // 更新时间
}
```

**数据库索引**：

- PRIMARY KEY: `Id`
- INDEX: `Path`, `Priority`, `IsActive`

### 4.2 授权策略 (AuthPolicy)

```csharp
public class AuthPolicy
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool RequireAuthentication { get; set; }
    public List<string> RequiredRoles { get; set; }
    public List<string> RequiredClaims { get; set; }
    public List<string> AllowedScopes { get; set; }
}
```

**预定义策略**：

- `public` - 无需认证
- `authenticated` - 需要有效令牌
- `admin-only` - 需要 admin 角色
- `email-verified` - 需要验证电子邮件

---

## 5. 部署架构

### 5.1 开发环境

```
Developer Machine
├── API Gateway (dotnet run)
├── MySQL (Docker)
├── Redis (Docker)
├── RabbitMQ (Docker)
└── SigNoz Stack (Docker)
    ├── OTel Collector
    ├── ClickHouse
    ├── Backend
    └── UI
```

### 5.2 生产环境

```
┌─────────────────────────────────────┐
│         Load Balancer (Nginx)       │
│         (Traffic Distribution)      │
└────────┬──────────────┬──────────────┘
         │              │
    ┌────▼────┐    ┌────▼────┐
    │ Gateway │    │ Gateway │ (multiple replicas)
    │ Pod 1   │    │ Pod 2   │
    └────┬────┘    └────┬────┘
         │              │
         └──────┬───────┘
                │
        ┌───────▼────────────┐
        │  Kubernetes Service│
        │  (api-gateway)     │
        └───────┬────────────┘
                │
    ┌───────────┼───────────┬──────────────┐
    │           │           │              │
 ┌──▼─┐   ┌────▼──┐  ┌────▼──┐  ┌──────▼─┐
 │MySQL│   │Redis  │  │RabbitMQ   │SigNoz │
 │ HA  │   │ HA    │  │  HA      │Stack  │
 └─────┘   └───────┘  └────────┘ └───────┘
```

---

## 6. 扩展性设计

### 6.1 新增认证方式

```
步骤：
1. 创建新的 AuthenticationStrategy 接口实现
2. 在 AuthenticationMiddleware 中注册
3. 配置选择使用的认证方式

示例：
┌─────────────────────────────────┐
│ IAuthenticationStrategy          │
├─────────────────────────────────┤
│ JwtStrategy (现有)              │
│ OAuth2Strategy (新增)           │
│ ApiKeyStrategy (新增)           │
│ MutualTlsStrategy (新增)        │
└─────────────────────────────────┘
```

### 6.2 新增协议支持

```
步骤：
1. 创建新的 IProtocolHandler 实现
2. 在 ProtocolDetector 中注册
3. 处理协议特定的逻辑

示例：
┌─────────────────────────────────┐
│ IProtocolHandler                │
├─────────────────────────────────┤
│ Http1Handler (现有)             │
│ Http2Handler (现有)             │
│ WebSocketHandler (现有)         │
│ GrpcHandler (新增)              │
│ ServerSentEventsHandler (现有)  │
└─────────────────────────────────┘
```

### 6.3 新增输出器 (Exporters)

```
OTel Collector 配置：

exporters:
  clickhouse:    ← 现有
  kafka:         ← 现有
  jaeger:        ← 添加新输出器
  otlp:
    endpoint: http://backend:4317
  prometheus:    ← 可添加
    endpoint: http://prometheus:9090
```

---

## 7. 安全设计

### 7.1 认证安全

```
威胁                 防护措施
────────────────────────────────────
Token 泄露          ├─ HTTPS 传输
                    ├─ 短过期时间
                    ├─ 刷新令牌机制
                    └─ 令牌黑名单

Brute Force         ├─ 速率限制
                    ├─ 账户锁定
                    └─ 登录尝试日志

Token 伪造          ├─ HMAC 签名验证
                    ├─ RSA 公钥验证
                    └─ 时间戳检查
```

### 7.2 授权安全

```
原则：
1. 最小权限 (Least Privilege)
   - 默认拒绝，显式允许

2. 分离关注 (Separation of Concerns)
   - 认证 ≠ 授权
   - 粗粒度 ≠ 细粒度

3. 审计日志 (Audit Logging)
   - 记录所有授权决策
   - 追踪权限变更
```

### 7.3 传输安全

```
HTTPS 配置：
appsettings.json:
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:5001",
        "Certificate": {
          "Path": "cert.pfx",
          "Password": "password"
        }
      }
    }
  }
}

HSTS (HTTP Strict Transport Security):
app.UseHsts();  // 在 Startup.cs
```

---

## 8. 性能优化

### 8.1 缓存策略

```
缓存层：
┌────────────────────────────┐
│ L1: 内存缓存 (IMemoryCache) │ (应用内)
├────────────────────────────┤
│ L2: Redis 缓存             │ (分布式)
├────────────────────────────┤
│ L3: 数据库                 │ (持久化)
└────────────────────────────┘

缓存对象：
- 路由配置 (TTL: 5min)
- 用户权限 (TTL: 10min)
- 授权策略 (TTL: 1hour)
- JWT 黑名单 (TTL: Token 过期时间)
```

### 8.2 连接池

```
HTTP 客户端连接池：
┌──────────────────────────┐
│ HttpClientFactory        │
│                          │
│ 为每个后端服务创建      │
│ 连接池，减少连接开销    │
│                          │
│ 连接数: 10-100          │
│ 超时: 30-60秒           │
└──────────────────────────┘
```

### 8.3 批处理和异步

```
OTel Batch Processor:
┌─────────────────────────┐
│ Span/Metric Generated   │
└────────────┬────────────┘
             │
             ↓ (缓冲)
┌─────────────────────────┐
│ Batch Buffer            │
│ (Size: 1000 or Time: 10s)
└────────────┬────────────┘
             │ (满足条件)
             ↓
┌─────────────────────────┐
│ Export to ClickHouse    │
└─────────────────────────┘
```

---

## 9. 监控和告警

### 9.1 关键指标

```
指标                        正常范围
──────────────────────────────────
请求吞吐量 (RPS)          > 1000/sec
平均延迟 (P50)            < 20ms
95分位延迟 (P95)          < 50ms
99分位延迟 (P99)          < 100ms
错误率                    < 0.1%
可用性                    > 99.9%
```

### 9.2 告警规则

```
告警级别         条件                    操作
────────────────────────────────────────
Critical        可用性 < 95%             立即通知
                错误率 > 5%
                响应时间 > 500ms

Warning         可用性 < 99%             通知团队
                错误率 > 1%
                内存使用 > 80%

Info            路由配置更改             记录日志
                新的认证失败
```

---

## 10. 迭代和演进

### 当前版本特性

✅ v1.0.0 (2025-12)

- 基础路由转发
- JWT 认证授权
- HTTP/1.1 支持
- SigNoz 可观测性
- 完整的测试覆盖

### 计划特性

🎯 v1.1.0 (2026-Q1)

- OAuth 2.0 支持
- 速率限制 (Rate Limiting)
- API 版本控制
- 请求重写规则

🎯 v1.2.0 (2026-Q2)

- GraphQL 支持
- gRPC 协议
- 服务网格集成
- AI 驱动的异常检测

🎯 v2.0.0 (2026-Q3)

- 完整微服务编排
- 自适应流量管理
- 智能缓存预热
- 零停机部署

---

## 11. 技术栈选型

| 层级     | 技术         | 理由             |
| -------- | ------------ | ---------------- |
| 框架     | ASP.NET Core | 高性能，企业级   |
| 语言     | C#           | 强类型，生产就绪 |
| 数据库   | MySQL        | 关系型，可靠     |
| 缓存     | Redis        | 高速，分布式     |
| 消息队列 | RabbitMQ     | 可靠，成熟       |
| 可观测性 | SigNoz       | 开源，低成本     |
| 测试     | xUnit        | .NET 标准        |
| ORM      | EF Core      | 原生支持         |

---

## 结论

本架构设计遵循：

- ✅ **分离关注** - 各层职责清晰
- ✅ **单一职责** - 每个组件功能专一
- ✅ **开闭原则** - 易扩展，难修改
- ✅ **依赖倒置** - 依赖抽象，不依赖具体
- ✅ **高内聚低耦合** - 模块独立，通信清晰

确保系统的可维护性、可扩展性和可靠性。

---

**版本**: 1.0 | **最后更新**: 2025 年 12 月 2 日
