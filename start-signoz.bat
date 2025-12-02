@echo off
REM SigNoz 启动脚本 (Windows)
REM 使用 Docker Compose 启动完整的 SigNoz 堆栈

echo.
echo ========================================
echo   SigNoz 遥测栈启动脚本
echo ========================================
echo.

REM 检查 Docker
docker --version >nul 2>&1
if errorlevel 1 (
    echo 错误: 未安装 Docker。请先安装 Docker Desktop
    exit /b 1
)

REM 检查 Docker Compose
docker-compose --version >nul 2>&1
if errorlevel 1 (
    echo 错误: 未安装 Docker Compose。请更新 Docker Desktop
    exit /b 1
)

echo [1/3] 启动 SigNoz 完整堆栈...
docker-compose -f docker-compose.signoz.yml up -d

if errorlevel 1 (
    echo 错误: 启动失败
    exit /b 1
)

echo [2/3] 等待服务初始化（约30秒）...
timeout /t 10 /nobreak

echo.
echo [3/3] 验证服务健康状态...

:wait_for_services
cls
echo 检查服务状态...

REM 检查 ClickHouse
curl -s http://localhost:8123/ping >nul 2>&1
if errorlevel 1 (
    echo ⏳ ClickHouse 初始化中...
    timeout /t 5 /nobreak
    goto wait_for_services
)

REM 检查 OTel Collector
curl -s http://localhost:13133/ >nul 2>&1
if errorlevel 1 (
    echo ⏳ OTel Collector 初始化中...
    timeout /t 5 /nobreak
    goto wait_for_services
)

REM 检查 SigNoz Backend
curl -s http://localhost:3301/api/v1/health >nul 2>&1
if errorlevel 1 (
    echo ⏳ SigNoz Backend 初始化中...
    timeout /t 5 /nobreak
    goto wait_for_services
)

REM 检查 SigNoz Frontend
curl -s http://localhost:3000 >nul 2>&1
if errorlevel 1 (
    echo ⏳ SigNoz Frontend 初始化中...
    timeout /t 5 /nobreak
    goto wait_for_services
)

echo.
echo ========================================
echo   ✓ SigNoz 堆栈启动成功！
echo ========================================
echo.
echo 服务地址:
echo   - SigNoz UI:           http://localhost:3000
echo   - ClickHouse:          http://localhost:8123
echo   - Kafka:               localhost:9092
echo   - OTel Collector gRPC: localhost:4317
echo   - OTel Collector HTTP: localhost:4318
echo   - Jaeger gRPC:         localhost:14250
echo   - Jaeger HTTP:         localhost:14268
echo.
echo 停止堆栈: docker-compose -f docker-compose.signoz.yml down
echo.
