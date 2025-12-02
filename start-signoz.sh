#!/bin/bash
# SigNoz 启动脚本 (Linux/macOS)
# 使用 Docker Compose 启动完整的 SigNoz 堆栈

set -e

echo ""
echo "========================================"
echo "  SigNoz 遥测栈启动脚本"
echo "========================================"
echo ""

# 检查 Docker
if ! command -v docker &> /dev/null; then
    echo "错误: 未安装 Docker。请先安装 Docker"
    exit 1
fi

# 检查 Docker Compose
if ! command -v docker-compose &> /dev/null; then
    echo "错误: 未安装 Docker Compose。请先安装"
    exit 1
fi

echo "[1/3] 启动 SigNoz 完整堆栈..."
docker-compose -f docker-compose.signoz.yml up -d

if [ $? -ne 0 ]; then
    echo "错误: 启动失败"
    exit 1
fi

echo "[2/3] 等待服务初始化（约30秒）..."
sleep 10

echo ""
echo "[3/3] 验证服务健康状态..."
echo ""

# 等待服务就绪
wait_for_service() {
    local url=$1
    local name=$2
    local max_attempts=30
    local attempt=1
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s "$url" > /dev/null 2>&1; then
            return 0
        fi
        echo "⏳ $name 初始化中... (${attempt}/${max_attempts})"
        sleep 2
        attempt=$((attempt + 1))
    done
    
    return 1
}

wait_for_service "http://localhost:8123/ping" "ClickHouse"
wait_for_service "http://localhost:13133/" "OTel Collector"
wait_for_service "http://localhost:3301/api/v1/health" "SigNoz Backend"
wait_for_service "http://localhost:3000" "SigNoz Frontend"

echo ""
echo "========================================"
echo "  ✓ SigNoz 堆栈启动成功！"
echo "========================================"
echo ""
echo "服务地址:"
echo "  - SigNoz UI:           http://localhost:3000"
echo "  - ClickHouse:          http://localhost:8123"
echo "  - Kafka:               localhost:9092"
echo "  - OTel Collector gRPC: localhost:4317"
echo "  - OTel Collector HTTP: localhost:4318"
echo "  - Jaeger gRPC:         localhost:14250"
echo "  - Jaeger HTTP:         localhost:14268"
echo ""
echo "停止堆栈: docker-compose -f docker-compose.signoz.yml down"
echo ""
