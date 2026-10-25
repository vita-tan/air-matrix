# air-matrix 后端多阶段构建
# 构建上下文：dji_server/   dockerfile：Dji.Web.Entry/Dockerfile
# 注意：运行时镜像必须与项目目标框架一致（net10.0）。

# ---------- 构建阶段 ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 发布配置：默认 Debug。
# ⚠️ 重要：Dji.Core 引用了 SixLabors.ImageSharp 4.1.2，其 build targets 在【Release】下会强制
#    校验六林檎商业许可证（无 sixlabors.lic 即直接 error 构建失败；Debug 下 ContinueOnError 仅告警）。
#     当前仓库未随附许可证，故默认用 Debug 以保证可构建。若已获得许可证（放置 sixlabors.lic
#     或设置 -p:SixLaborsLicenseKey=...），请改为 Release：
#       docker compose build --build-arg BUILD_CONFIGURATION=Release
ARG BUILD_CONFIGURATION=Debug

# 复制整个源码树（含 Plugins 项目引用），再还原 + 发布
# 说明：Dji.Core 通过 ProjectReference 引用 ../Plugins/*，因此必须连同 Plugins 一起复制，
#       不能用「仅复制 *.csproj」的优化写法（会漏掉 Plugins 的工程引用导致 restore 失败）。
COPY . .
RUN dotnet restore Dji.Web.Entry/Dji.Web.Entry.csproj
RUN dotnet publish Dji.Web.Entry/Dji.Web.Entry.csproj -c ${BUILD_CONFIGURATION} -o /app/publish /p:UseAppHost=false

# ---------- 运行阶段 ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# 语言/区域设置（中文）
ENV LANG zh-Hans
# 时区：统一为 Asia/Shanghai，避免与 SQLite 时期行为漂移、日志时间错乱
ENV TZ=Asia/Shanghai

# 使用阿里云镜像源更新并安装 procps（用于查看进程，排障方便）
RUN sed -i 's/deb.debian.org/mirrors.aliyun.com/' /etc/apt/sources.list \
 && apt-get update && apt-get install -y --no-install-recommends procps tzdata wget \
 && ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
 && echo "Asia/Shanghai" > /etc/timezone \
 && rm -rf /var/lib/apt/lists/*

# 复制发布产物
COPY --from=build /app/publish .

# 后端仅在 Docker 内网监听 5005，不映射公网（由 nginx 反代）
EXPOSE 5005

ENTRYPOINT ["dotnet", "Dji.Web.Entry.dll"]
