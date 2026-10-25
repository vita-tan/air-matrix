#!/usr/bin/env bash
# ============================================================
#  air-matrix · 停止服务（在【宿主机】执行）
#  默认仅停止并移除容器，保留数据卷（dji_pg/dji_minio/... 不删除，数据安全）。
#  需要连同卷一起删除时显式：bash deploy/scripts/down.sh -v
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT}"

# down 仍需解析 compose（含 ${VAR}），故加载 .env
if [ -f deploy/.env ]; then
  set -a
  # shellcheck disable=SC1091
  . deploy/.env
  set +a
fi

echo "[down] docker compose down $*"
docker compose down "$@"
