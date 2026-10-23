@echo off
chcp 65001 >nul
REM ============================================================
REM  DJI 无人机智能管控平台 - 一键启动（Windows）
REM ------------------------------------------------------------
REM   MQTT Broker : 127.0.0.1:1883      (aedes 本地联调)
REM   流媒体      : RTMP 1935 / HTTP-FLV+HLS 8000
REM   后端 API    : http://localhost:5005   (Swagger: /kapi)
REM   前端界面    : http://localhost:8888
REM   登录        : superadmin / 123456  (演示环境已免验证码)
REM ============================================================
REM  接真机前请先看同目录的 README-MQTT-流媒体-接真机.md
REM ============================================================

set DOTNET=E:\workbuddy\tools\dotnet\dotnet.exe
set ROOT=E:\program\wurenji\drone-platform
set SERVICES=%ROOT%\services
set BACKEND=%ROOT%\dji_server\Dji.Web.Entry
set FRONTEND=%ROOT%\dji_vue

echo.
echo [1/4] MQTT Broker  (1883) ...
start "dji-mqtt" cmd /k "cd /d %SERVICES% && node mqtt-broker.js"

echo [2/4] 流媒体服务    (RTMP 1935 / HTTP 8000) ...
start "dji-media" cmd /k "cd /d %SERVICES% && node media-server.js"

REM 等 broker 起来，后端启动时会立刻连接
timeout /t 2 >nul

echo [3/4] 后端 dji_server (5005) ...
start "dji-backend" cmd /k "cd /d %BACKEND% && "%DOTNET%" run"

echo [4/4] 前端 dji_vue    (8888) ...
start "dji-frontend" cmd /k "cd /d %FRONTEND% && npm run dev"

echo.
echo ============================================================
echo  全部组件已启动（4 个终端窗口）
echo ------------------------------------------------------------
echo   MQTT     : 127.0.0.1:1883
echo   流媒体   : rtmp 1935 / http-flv+hls 8000
echo   后端     : http://localhost:5005
echo   前端     : http://localhost:8888
echo   登录     : superadmin / 123456
echo ------------------------------------------------------------
echo  关闭对应终端窗口即可停止该组件
echo ============================================================
pause
