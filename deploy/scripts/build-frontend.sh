#!/usr/bin/env bash
# ============================================================
#  air-matrix · 前端构建（在【宿主机】执行，只需 Docker，无需本机装 Node）
#  作用：
#   1) 用 node:20 容器执行 npm ci && npm run build，产出 dji_vue/dist
#   2) 把 dist 复制进命名卷 air_matrix_dist（nginx 只读挂载 /usr/share/nginx/html）
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT}/dji_vue"

# 确保生产环境变量存在（.env.production 不入库，需从 example 复制并填 SM2 公钥）
if [ ! -f .env.production ]; then
  echo "[build-frontend] 未找到 dji_vue/.env.production，从 example 复制（请确认 VITE_SM2_PUBLIC_KEY 与后端一致）"
  cp .env.production.example .env.production
fi

echo "[build-frontend] 使用 node:20 容器构建（npm ci && npm run build）"
# 本项目依赖 Cesium/ECharts/monaco 等大包，vite build 默认堆内存会 OOM，需放宽到 4GB
docker run --rm \
  -e NODE_OPTIONS=--max-old-space-size=4096 \
  -v "${ROOT}/dji_vue:/app" \
  -w /app \
  node:20 sh -c "npm ci && npm run build"

DIST="${ROOT}/dji_vue/dist"
if [ ! -d "${DIST}" ]; then
  echo "[build-frontend] 构建失败：未生成 ${DIST}" >&2
  exit 1
fi

echo "[build-frontend] 复制 dist 到命名卷 air_matrix_dist"
docker run --rm \
  -v "${DIST}:/src:ro" \
  -v air_matrix_dist:/target \
  alpine:3.20 sh -c 'rm -rf /target/* /target/.[!.]* 2>/dev/null || true; cp -a /src/. /target/'

echo "[build-frontend] 完成：dist 已写入 air_matrix_dist 卷"
