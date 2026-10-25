#!/usr/bin/env bash
# ============================================================
#  air-matrix · 启动全部服务（在【宿主机】执行）
#  会先加载 deploy/.env（compose 的 ${VAR} 插值来源），再 docker compose up -d
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT}"

if [ ! -f deploy/.env ]; then
  echo "[up] 缺少 deploy/.env，请先：cp deploy/.env.example deploy/.env 并填真实值" >&2
  exit 1
fi
if [ ! -d deploy/config ]; then
  echo "[up] 缺少 deploy/config，请先：cp dji_server/docs/config-template/* deploy/config/ 并改真实值" >&2
  exit 1
fi

# 加载环境变量（供 compose ${VAR} 插值）
set -a
# shellcheck disable=SC1091
. deploy/.env
set +a

echo "[up] docker compose up -d"
docker compose up -d
echo "[up] 当前容器状态："
docker compose ps
