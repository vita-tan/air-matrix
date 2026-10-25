#!/usr/bin/env bash
# ============================================================
#  air-matrix · Let's Encrypt 证书申请 / 续期（在【宿主机】执行）
#  前置：已完成域名 A 记录指向本机、安全组放行 80/443、
#        且 deploy/.env 已填 DOMAIN（或下方回退默认值）。
#
#  流程：
#   1) 若证书尚不存在，先在 dji_certs 卷生成【自签占位证书】，
#      让 nginx 能先起 443（HTTP-01 挑战走 80 不受影响）。
#   2) 启动/确保 nginx 运行。
#   3) 用 certbot --webroot 申请真实证书（写入 dji_certs 卷同一路径）。
#   4) reload nginx 生效。
#
#  续期（建议宿主机 cron 每天一次）：
#    docker compose run --rm air-matrix-certbot renew --webroot -w /var/www/certbot \
#      && docker compose exec -T air-matrix-nginx nginx -s reload
# ============================================================
set -euo pipefail

# 允许从 deploy/.env 读取 DOMAIN / CERTBOT_EMAIL
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
if [ -f "${ROOT}/deploy/.env" ]; then
  # shellcheck disable=SC1091
  set -a; . "${ROOT}/deploy/.env"; set +a
fi

DOMAIN="${DOMAIN:-air-matrix.crmappai.xyz}"
EMAIL="${CERTBOT_EMAIL:-admin@${DOMAIN}}"
COMPOSE="docker compose -f ${ROOT}/docker-compose.yml"
CERTS_VOL="air_matrix_certs"
LIVE_DIR="/etc/letsencrypt/live/${DOMAIN}"

echo "[ssl] 域名=${DOMAIN} 邮箱=${EMAIL}"

# 1) 自签占位证书（幂等：已存在则跳过）
echo "[ssl] 确保自签占位证书存在（让 nginx 能先起 443）"
docker run --rm -v "${CERTS_VOL}:/etc/letsencrypt" alpine:3.20 sh -c "
  set -e
  mkdir -p '${LIVE_DIR}'
  if [ ! -f '${LIVE_DIR}/fullchain.pem' ] || [ ! -f '${LIVE_DIR}/privkey.pem' ]; then
    apk add --no-cache openssl >/dev/null
    openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
      -keyout '${LIVE_DIR}/privkey.pem' \
      -out '${LIVE_DIR}/fullchain.pem' \
      -subj '/CN=${DOMAIN}' >/dev/null 2>&1
    echo '[ssl] 自签占位证书已生成'
  else
    echo '[ssl] 证书已存在，跳过自签占位'
  fi
"

# 2) 确保 nginx 运行（ACME HTTP-01 需要 80 端口由 nginx 提供挑战目录）
echo "[ssl] 启动 nginx"
${COMPOSE} up -d air-matrix-nginx
sleep 3

# 3) 申请真实证书（--keep-until-expiring 幂等：有效期内不重复申请）
echo "[ssl] certbot 申请/续期证书"
${COMPOSE} run --rm air-matrix-certbot \
  certonly --webroot -w /var/www/certbot \
  -d "${DOMAIN}" \
  --email "${EMAIL}" --agree-tos --non-interactive --keep-until-expiring

# 4) reload nginx
echo "[ssl] reload nginx 使新证书生效"
${COMPOSE} exec -T air-matrix-nginx nginx -s reload || ${COMPOSE} restart air-matrix-nginx

echo "[ssl] 完成：https://${DOMAIN}"
