#!/bin/bash
# ============================================
# OpenTelemetry 一键启动脚本（Linux/Mac）
# ============================================

set -e  # 任何命令失败时退出

echo ""
echo "======================================="
echo "API Gateway + OpenTelemetry 快速启动"
echo "======================================="
echo ""

# 检查 Docker
if ! command -v docker &> /dev/null; then
    echo "[ERROR] 未找到 Docker，请先安装 Docker"
    echo "Mac:   https://docs.docker.com/desktop/install/mac-install/"
    echo "Linux: https://docs.docker.com/engine/install/"
    exit 1
fi

# 检查 docker-compose
if ! command -v docker-compose &> /dev/null; then
    echo "[WARNING] 使用 'docker compose' 代替 'docker-compose'"
    DC_CMD="docker compose"
else
    DC_CMD="docker-compose"
fi

echo "[1/4] 启动 OpenTelemetry 基础设施..."
echo ""

$DC_CMD -f docker-compose.otel.yml up -d

if [ $? -ne 0 ]; then
    echo "[ERROR] 启动容器失败"
    exit 1
fi

echo "[✓] 容器启动成功"
echo ""

echo "[2/4] 等待服务就绪..."
sleep 5

echo "[✓] 服务已就绪"
echo ""

echo "[3/4] 服务信息:"
echo ""

echo "======================================="
echo "服务地址："
echo "======================================="
echo ""
echo "Jaeger UI:     http://localhost:16686"
echo "Prometheus UI: http://localhost:9090"
echo "Grafana:       http://localhost:3000 (admin/admin)"
echo ""

echo "======================================="
echo "下一步："
echo "======================================="
echo ""
echo "1. 启动 API Gateway:"
echo "   dotnet run --project src/Api/Api.csproj"
echo ""
echo "2. 测试端点:"
echo "   curl http://localhost:5000/health"
echo ""
echo "3. 查看指标 (Prometheus):"
echo "   curl http://localhost:9090/api/v1/query?query=http_request_count"
echo ""
echo "4. 查看链路 (Jaeger):"
echo "   打开浏览器访问 http://localhost:16686"
echo ""

echo "======================================="
echo "常用命令："
echo "======================================="
echo ""
echo "查看容器状态:"
echo "  docker ps -a"
echo ""
echo "查看容器日志:"
echo "  docker logs jaeger"
echo "  docker logs prometheus"
echo ""
echo "停止服务:"
echo "  $DC_CMD -f docker-compose.otel.yml down"
echo ""
echo "清理数据:"
echo "  $DC_CMD -f docker-compose.otel.yml down -v"
echo ""
