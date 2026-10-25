#!/usr/bin/env bash
# ============================================================
#  air-matrix · （可选）SQLite -> PostgreSQL 数据迁移（在【宿主机】执行）
#  说明：本项目【全新部署】无需迁移 —— 后端 SqlSugar CodeFirst 会在
#        dji_admin / dji_log 上自动建表并写入种子数据（EnableInitTable/Seed）。
#        仅当你要把【旧环境已有 SQLite 数据】搬迁到新 PG 时才执行本脚本。
#  依赖：Docker（使用 dimitri/pgloader 镜像）；复用 postgres 容器网络命名空间，
#        以 localhost:5432 访问数据库，无需关心 compose 网络前缀。
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

PGUSER="${POSTGRES_USER:-dji}"
PGPASS="${POSTGRES_PASSWORD:-}"
SQLITE_ADMIN="${ROOT}/dji_server/Dji.Web.Entry/db/dji.admin.db"
SQLITE_LOG="${ROOT}/dji_server/Dji.Web.Entry/db/dji.log.db"

if [ ! -f "${SQLITE_ADMIN}" ] && [ ! -f "${SQLITE_LOG}" ]; then
  echo "[migrate] 未发现 SQLite 源库（${SQLITE_ADMIN} / ${SQLITE_LOG}）。"
  echo "[migrate] 全新环境无需迁移：后端 CodeFirst 会自动建表 + 种子。"
  exit 0
fi

if [ -z "${PGPASS}" ]; then
  echo "[migrate] 未设置 POSTGRES_PASSWORD（deploy/.env），无法连接 PG" >&2
  exit 1
fi

# 确保 postgres 正在运行（pgloader 复用其网络命名空间）
if ! docker ps --format '{{.Names}}' | grep -qx 'air-matrix-postgres'; then
  echo "[migrate] air-matrix-postgres 未运行，请先 bash deploy/scripts/up.sh" >&2
  exit 1
fi

run_migrate() {
  local src="$1"        # sqlite 文件绝对路径
  local db="$2"         # 目标库名 dji_admin / dji_log
  if [ ! -f "${src}" ]; then
    echo "[migrate] 跳过 ${db}：源库 ${src} 不存在"
    return 0
  fi
  local rel="${src#${ROOT}/}"
  local loadfile="${ROOT}/.pgloader_${db}.load"
  cat > "${loadfile}" <<EOF
LOAD DATABASE
  FROM sqlite:///${rel}
  INTO postgresql://${PGUSER}:${PGPASS}@localhost:5432/${db}
WITH
  include drop,
  create tables,
  create indexes,
  reset sequences,
  foreign keys
SET
  work_mem to '16MB',
  maintenance_work_mem to '512MB';
EOF
  echo "[migrate] pgloader ${src} -> ${db}"
  docker run --rm \
    --network "container:air-matrix-postgres" \
    -v "${ROOT}:/work" -w /work \
    dimitri/pgloader:latest \
    pgloader "/work/.pgloader_${db}.load"
  rm -f "${loadfile}"
}

echo "[migrate] 开始迁移（如目标库已有表，include drop 会先删表再重建；请确认无重要数据）"
run_migrate "${SQLITE_ADMIN}" "dji_admin"
run_migrate "${SQLITE_LOG}" "dji_log"

echo "[migrate] 完成。建议迁移后重启后端：docker compose restart air-matrix-backend"
