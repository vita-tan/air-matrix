#!/usr/bin/env bash
# ============================================================
#  air-matrix · PostgreSQL 备份（pg_dump，在【宿主机】执行）
#  替代原 SQLite 文件拷贝：分别导出 dji_admin(主库) / dji_log(日志库) 为自定义格式 dump。
#  恢复示例：
#    docker compose exec -T air-matrix-postgres pg_restore -U dji -d dji_admin -c < dji_admin_xxx.dump
#  建议宿主机 cron 每日执行：
#    0 3 * * * /opt/air-matrix/deploy/scripts/backup-pg.sh >> /var/log/air-matrix-backup.log 2>&1
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT}"

if [ -f deploy/.env ]; then
  set -a
  # shellcheck disable=SC1091
  . deploy/.env
  set +a
fi

BACKUP_DIR="${BACKUP_DIR:-${ROOT}/backups}"
mkdir -p "${BACKUP_DIR}"
TS="$(date +%F_%H%M%S)"
PGUSER="${POSTGRES_USER:-dji}"

for DB in dji_admin dji_log; do
  OUT="${BACKUP_DIR}/${DB}_${TS}.dump"
  echo "[backup] pg_dump ${DB} -> ${OUT}"
  docker compose exec -T air-matrix-postgres pg_dump -U "${PGUSER}" -d "${DB}" -Fc > "${OUT}"
done

echo "[backup] 完成，备份目录：${BACKUP_DIR}"
ls -lh "${BACKUP_DIR}" | tail -n +1
