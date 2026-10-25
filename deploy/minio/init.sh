#!/bin/sh
# ============================================================
#  air-matrix · MinIO 初始化（幂等）
#  由 compose 的 air-matrix-minio-init（minio/mc 镜像）一次性执行：
#    1. 建桶 dji-media（已存在则跳过）
#    2. 桶匿名 GetObject 下载策略（公开读，供浏览器预览）
#    3. 创建上传专用账号 djicloud（MINIO_ACCESS_KEY/SECRET_KEY），
#       并绑定仅限 dji-media 读写的策略（写入仍需 AK/SK，不开放匿名写）
#  环境变量（来自 deploy/.env，经 compose 注入）：
#    MINIO_ROOT_USER / MINIO_ROOT_PASSWORD  （MinIO 管理员）
#    MINIO_ACCESS_KEY / MINIO_SECRET_KEY    （djicloud 子账号）
# ============================================================
set -e

MINIO_ALIAS="local"
MINIO_HOST="http://air-matrix-minio:9000"
BUCKET="dji-media"
POLICY_NAME="dji-media-rw"

echo "[minio-init] 配置 mc alias -> ${MINIO_HOST}"
mc alias set ${MINIO_ALIAS} ${MINIO_HOST} "${MINIO_ROOT_USER}" "${MINIO_ROOT_PASSWORD}" >/dev/null

echo "[minio-init] 建桶 ${BUCKET}（已存在则跳过）"
mc mb --ignore-existing ${MINIO_ALIAS}/${BUCKET}

echo "[minio-init] 设置 ${BUCKET} 匿名下载（公开读）"
mc anonymous set download ${MINIO_ALIAS}/${BUCKET}

# 仅限 dji-media 读写的策略
POLICY_FILE="/tmp/${POLICY_NAME}.json"
cat > "${POLICY_FILE}" <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject", "s3:ListBucket"],
      "Resource": ["arn:aws:s3:::${BUCKET}", "arn:aws:s3:::${BUCKET}/*"]
    }
  ]
}
EOF

echo "[minio-init] 创建/更新策略 ${POLICY_NAME}"
if mc admin policy info ${MINIO_ALIAS} ${POLICY_NAME} >/dev/null 2>&1; then
  mc admin policy remove ${MINIO_ALIAS} ${POLICY_NAME} >/dev/null 2>&1 || true
fi
mc admin policy create ${MINIO_ALIAS} ${POLICY_NAME} "${POLICY_FILE}"

echo "[minio-init] 创建/绑定上传账号 ${MINIO_ACCESS_KEY}"
if mc admin user info ${MINIO_ALIAS} "${MINIO_ACCESS_KEY}" >/dev/null 2>&1; then
  echo "[minio-init] 账号 ${MINIO_ACCESS_KEY} 已存在，跳过创建"
else
  mc admin user add ${MINIO_ALIAS} "${MINIO_ACCESS_KEY}" "${MINIO_SECRET_KEY}"
fi
mc admin policy attach ${MINIO_ALIAS} ${POLICY_NAME} --user "${MINIO_ACCESS_KEY}"

echo "[minio-init] 完成：桶 ${BUCKET} / 账号 ${MINIO_ACCESS_KEY} / 策略 ${POLICY_NAME}"
