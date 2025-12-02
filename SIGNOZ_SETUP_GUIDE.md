# SigNoz 集成指南

## 概述

SigNoz 是一个开源的 OpenTelemetry 本地化替代品，为您的 API 网关提供完整的可观测性解决方案，包括：

- **分布式追踪**：追踪请求在多个服务间的调用链
- **性能指标**：监控应用的实时性能数据
- **日志管理**：集中收集和查询应用日志
- **告警规则**：基于指标和追踪的智能告警

## 架构

```
┌─────────────────────────────────────────────────────────────┐
│ API Gateway (Your Application)                              │
│ - OpenTelemetry SDK (Traces + Metrics)                     │
│ - Sends to: http://localhost:4317 (OTLP gRPC)             │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ OTel Collector (signoz-otel-collector)                      │
│ - Receivers: OTLP, Jaeger                                  │
│ - Processors: Batch, Memory Limiter, Attributes            │
│ - Exporters: ClickHouse, Kafka                             │
└─────────────────────────────────────────────────────────────┘
     ↓                        ↓                       ↓
  ClickHouse              Kafka                  (其他导出器)
  (时间序列DB)           (消息队列)
     ↓                        ↓
┌──────────────────────────────────────────────────────────────┐
│ SigNoz Backend (signoz-backend)                              │
│ - 数据处理和查询                                            │
└──────────────────────────────────────────────────────────────┘
     ↓
┌──────────────────────────────────────────────────────────────┐
│ SigNoz UI (signoz-frontend)                                  │
│ - Web Dashboard: http://localhost:3000                      │
│ - 追踪查询、指标分析、日志搜索                             │
└──────────────────────────────────────────────────────────────┘
```

## 快速开始

### 1. 启动 SigNoz

#### Windows (PowerShell 管理员)：

```powershell
.\start-signoz.bat
```

#### Linux / macOS：

```bash
chmod +x start-signoz.sh
./start-signoz.sh
```

#### 使用 Docker Compose 直接启动：

```bash
docker-compose -f docker-compose.signoz.yml up -d
```

### 2. 验证服务

等待所有服务初始化完成后，访问以下地址：

| 服务           | URL                   | 用途                           |
| -------------- | --------------------- | ------------------------------ |
| SigNoz UI      | http://localhost:3000 | 主控制台，查看追踪、指标、日志 |
| OTel Collector | localhost:4317        | gRPC 端点（应用发送遥测数据）  |
| OTel Collector | localhost:4318        | HTTP 端点（备选）              |
| ClickHouse     | http://localhost:8123 | 数据库管理界面（高级）         |
| Kafka          | localhost:9092        | 消息队列（集成用）             |

### 3. 应用配置

你的 API Gateway 已配置为使用 SigNoz：

**appsettings.json：**

```json
"OpenTelemetry": {
  "EnableTraces": true,
  "EnableMetrics": true,
  "SigNozEnabled": true,
  "SigNozOtlpEndpoint": "http://localhost:4317",
  "JaegerEnabled": false,
  "PrometheusEnabled": true,
  "PrometheusPort": 9090
}
```

**自动配置的内容：**

- ✅ OTLP gRPC 导出器（指向 SigNoz）
- ✅ OpenTelemetry 链路追踪
- ✅ 运行时性能指标采集
- ✅ HTTP 请求/响应富化
- ✅ 自定义标签（请求 ID、关联 ID）

## 使用说明

### 查看分布式追踪

1. 打开 http://localhost:3000
2. 点击左侧菜单 **"Traces"**
3. 选择服务：**"api-gateway"**
4. 查看最近的请求追踪

**追踪详情包含：**

- 完整的请求链路
- 每个操作的耗时
- 错误和异常信息
- 自定义标签和属性

### 监控性能指标

1. 在 SigNoz UI 中点击 **"Dashboards"** 或 **"Metrics"**
2. 查看收集的指标：
   - HTTP 请求数量和延迟
   - 运行时性能（内存、CPU）
   - 流程级指标
   - 自定义应用指标

### 搜索日志（如果启用）

1. 点击 **"Logs"** 标签
2. 搜索特定时间范围内的日志
3. 按服务、级别、内容过滤

### 设置告警

1. 点击 **"Alerts"**
2. 创建新的告警规则，例如：
   - 错误率超过 5%
   - 平均响应时间 > 500ms
   - 特定端点失败

## 核心配置文件

### docker-compose.signoz.yml

- 定义所有容器和依赖关系
- 配置环境变量
- 管理端口映射和卷挂载

### signoz-otel-collector-config.yaml

- OTel Collector 配置
- 接收器（receivers）：OTLP、Jaeger、Prometheus
- 处理器（processors）：批处理、内存限制、属性丰富化
- 导出器（exporters）：ClickHouse、Kafka

### OpenTelemetryConfiguration.cs

- .NET 应用端的 OpenTelemetry 配置
- 启用链路追踪和指标采集
- OTLP 导出器配置

## 性能建议

### 内存管理

- OTel Collector 设置为最大 512 MB
- 批处理大小：1000 条记录
- 批处理超时：10 秒

### 数据保留

- ClickHouse 默认保留 30 天数据
- 可通过 TTL 策略配置保留期限

### 高可用部署

生产环境中的优化：

1. 使用 Kubernetes 部署 SigNoz
2. 配置多副本 ClickHouse
3. 使用 LoadBalancer 负载均衡
4. 配置告警和自动扩展

## 故障排除

### 问题：OTel Collector 无法连接

**症状：** 应用启动时出现连接错误

**解决：**

1. 确认 Docker 正在运行：`docker ps`
2. 检查端口占用：`netstat -an | find ":4317"`
3. 查看收集器日志：`docker logs signoz-otel-collector`

### 问题：数据未出现在 SigNoz UI

**症状：** 发送请求后看不到追踪数据

**解决：**

1. 验证应用连接：`curl http://localhost:4317` （应返回错误，但说明端口开放）
2. 检查应用日志是否有导出错误
3. 验证 ClickHouse 运行：`curl http://localhost:8123/ping`
4. 查看 Collector 日志：`docker logs signoz-otel-collector`

### 问题：高 CPU 或内存使用

**症状：** Docker 容器消耗过多资源

**解决：**

1. 增加内存限制（docker-compose.signoz.yml）
2. 调整批处理大小（signoz-otel-collector-config.yaml）
3. 检查是否有数据导出回环（infinite loops）

## 与现有系统集成

### Prometheus 指标导入

如果已有 Prometheus 配置：

```yaml
# signoz-otel-collector-config.yaml 中的 prometheus receiver
prometheus:
  config:
    scrape_configs:
      - job_name: "api-gateway"
        static_configs:
          - targets: ["localhost:9090"]
```

### Jaeger 迁移

从 Jaeger 迁移到 SigNoz：

1. 保持 Jaeger 导出器配置
2. OTel Collector 同时接收两者
3. 逐步将应用指向 SigNoz

## 后续步骤

1. **自定义仪表板**

   - 在 SigNoz UI 创建自定义仪表板
   - 添加关键业务指标

2. **增强数据富化**

   - 添加业务相关的自定义标签
   - 在 OpenTelemetryConfiguration.cs 中扩展

3. **集成告警通知**

   - 配置 Slack、钉钉等通知渠道
   - 设置关键指标告警

4. **性能优化**
   - 定期查看数据存储成本
   - 优化采样率（如果需要）

---

**更多信息：**

- SigNoz 官方文档：https://signoz.io/docs/
- OpenTelemetry 规范：https://opentelemetry.io/
