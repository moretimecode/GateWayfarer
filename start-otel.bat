@echo off
REM ============================================
REM OpenTelemetry 一键启动脚本（Windows）
REM ============================================

echo.
echo ===============================================
echo  API Gateway + OpenTelemetry 快速启动
echo ===============================================
echo.

REM 检查 Docker 是否安装
where docker > nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] 未找到 Docker，请先安装 Docker Desktop
    echo 下载地址: https://www.docker.com/products/docker-desktop
    pause
    exit /b 1
)

REM 检查 docker-compose
where docker-compose > nul 2>&1
if %errorlevel% neq 0 (
    echo [WARNING] 未找到 docker-compose，将尝试使用 docker compose
)

echo [1/4] 启动 OpenTelemetry 基础设施 (Jaeger + Prometheus + Grafana)...
echo.

REM 启动 Docker 容器
docker-compose -f docker-compose.otel.yml up -d

if %errorlevel% neq 0 (
    echo [ERROR] 启动容器失败，请检查 Docker 是否运行
    pause
    exit /b 1
)

echo [✓] 容器启动成功
echo.

REM 等待容器就绪
echo [2/4] 等待服务就绪...
timeout /t 5 /nobreak

echo [✓] 服务已就绪
echo.

REM 打开浏览器
echo [3/4] 打开服务管理界面...
echo.

REM 打开各个服务的 UI
start http://localhost:16686  REM Jaeger UI
timeout /t 1 /nobreak
start http://localhost:9090   REM Prometheus UI
timeout /t 1 /nobreak
start http://localhost:3000   REM Grafana (用户名: admin, 密码: admin)

echo [✓] 已打开浏览器
echo.

echo [4/4] 启动 API Gateway...
echo.
echo 请在 Visual Studio 或命令行中运行:
echo   dotnet run --project src/Api/Api.csproj
echo.

echo ===============================================
echo  服务地址:
echo ===============================================
echo.
echo Jaeger UI:      http://localhost:16686
echo Prometheus UI:  http://localhost:9090
echo Grafana:        http://localhost:3000 (admin/admin)
echo.
echo API Gateway 应在以下地址运行:
echo   http://localhost:5000 (默认端口)
echo   http://localhost:80   (Kestrel 配置的端口)
echo.

echo ===============================================
echo  快速命令:
echo ===============================================
echo.
echo 查看容器状态:
echo   docker ps -a
echo.
echo 查看容器日志:
echo   docker logs jaeger
echo   docker logs prometheus
echo   docker logs grafana
echo.
echo 停止所有容器:
echo   docker-compose -f docker-compose.otel.yml down
echo.
echo 清理所有数据:
echo   docker-compose -f docker-compose.otel.yml down -v
echo.

pause
