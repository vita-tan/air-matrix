#!/usr/bin/env bash
# ============================================================
#  air-matrix · 一键部署（在【宿主机】执行）
#  步骤：前端构建 → 镜像构建 → 起服务 → 健康等待 → 申请证书
#  前置：deploy/.env 与 deploy/config/* 已按 README 填好真实值。
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT}"

if [ ! -f deploy/.env ]; then
  echo "[deploy] 缺少 deploy/.env，请先 cp deploy/.env.example deploy/.env 并填真实值" >&2
  exit 1
fi
if [ ! -d deploy/config ]; then
  echo "[deploy] 缺少 deploy/config，请先 cp dji_server/docs/config-template/* deploy/config/ 并改真实值" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
. deploy/.env
set +a

echo "== 1/5 构建前端 dist =="
bash deploy/scripts/build-frontend.sh

echo "== 2/5 构建后端与中间件镜像（backend 为多阶段 publish） =="
docker compose build

echo "== 3/5 启动服务（postgres/minio 先起，backend/mqtt/media 随后，nginx 最后） =="
docker compose up -d

echo "== 4/5 等待关键容器就绪 =="
sleep 8
docker compose ps

echo "== 5/5 申请 / 续期 HTTPS 证书并 reload nginx =="
bash deploy/certbot/init-ssl.sh

echo "== 部署完成：https://${DOMAIN:-air-matrix.crmappai.xyz} =="
