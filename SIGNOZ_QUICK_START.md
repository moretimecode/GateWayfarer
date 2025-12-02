# SigNoz 快速开始

## 5 分钟快速启动

### 前置条件

- Docker Desktop 已安装并运行
- 网络连接良好

### 步骤 1：启动 SigNoz 堆栈

**Windows (PowerShell)：**

```powershell
.\start-signoz.bat
```

**Linux/macOS：**

```bash
chmod +x start-signoz.sh
./start-signoz.sh
```

或使用 Docker Compose 直接启动：

```bash
docker-compose -f docker-compose.signoz.yml up -d
```

### 步骤 2：等待服务初始化

服务启动后会输出以下信息：

```
✓ SigNoz 堆栈启动成功！

服务地址:
  - SigNoz UI:           http://localhost:3000
  - OTel Collector gRPC: localhost:4317
  - Jaeger gRPC:         localhost:14250
```

### 步骤 3：启动 API Gateway

```bash
cd src/Api
dotnet run
```

或使用 VS Code 调试运行。

### 步骤 4：发送测试请求

```bash
# 获取路由配置（会生成追踪）
curl -X GET http://localhost:5000/api/routes

# 或在浏览器打开
# http://localhost:5000/api/routes
# http://localhost:5000/health
```

### 步骤 5：查看追踪数据

1. 打开 http://localhost:3000
2. 点击左侧 **"Traces"** 菜单
3. 选择服务 **"ApiGateway"**
4. 查看你的请求的完整追踪链路

## 关键特性

### 分布式追踪

```
请求链路可视化：
┌─────────────────────────────────────┐
│ GET /api/routes (500ms)             │
├─────────────────────────────────────┤
│ ├─ HttpContextProtocolDetector      │
│ │  └─ 50ms                          │
│ ├─ RouteConfigurationHandler        │
│ │  └─ 350ms                         │
│ └─ ResponseForwarder                │
│    └─ 100ms                         │
└─────────────────────────────────────┘
```

### 实时性能指标

- HTTP 请求延迟分布
- 错误率和异常追踪
- 资源使用情况（CPU、内存）
- 自定义应用指标

### 日志和上下文

- 自动关联 Request ID
- Correlation ID 追踪
- 完整的请求/响应上下文

## 常用命令

```bash
# 查看运行中的容器
docker ps | grep signoz

# 查看 SigNoz 日志
docker logs signoz-otel-collector

# 停止堆栈
docker-compose -f docker-compose.signoz.yml down

# 完全清理（包括数据）
docker-compose -f docker-compose.signoz.yml down -v

# 重启特定服务
docker-compose -f docker-compose.signoz.yml restart signoz-backend
```

## 常见问题

### Q: UI 无法访问？

```bash
# 检查容器是否运行
docker ps | grep signoz-frontend

# 查看日志
docker logs signoz-frontend

# 等待更久（首次初始化可能需要 1-2 分钟）
```

### Q: 看不到追踪数据？

```bash
# 1. 检查应用是否连接到 Collector
curl http://localhost:14250

# 2. 查看 Collector 日志
docker logs signoz-otel-collector

# 3. 重新发送请求确保有新数据
curl http://localhost:5000/api/routes
```

### Q: 如何修改采集端口？

编辑 `docker-compose.signoz.yml` 中的端口映射：

```yaml
signoz-otel-collector:
  ports:
    - "14250:14250" # 修改这里的左边数字
```

## 下一步

1. **查看完整文档**：`SIGNOZ_SETUP_GUIDE.md`
2. **配置告警**：在 SigNoz UI 中设置告警规则
3. **自定义仪表板**：创建业务相关的性能指标面板
4. **集成通知**：配置 Slack、钉钉等告警通道

---

📚 **更多资源**

- SigNoz 官网：https://signoz.io
- OpenTelemetry 文档：https://opentelemetry.io
- GitHub：https://github.com/SigNoz/signoz
