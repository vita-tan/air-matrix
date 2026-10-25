-- ============================================================
--  air-matrix · PostgreSQL 初始化脚本
--  挂载：compose 中 air-matrix-postgres 的
--        ./deploy/postgres/init.sql -> /docker-entrypoint-initdb.d/init.sql
--  触发：postgres 容器【首次启动】自动执行（官方镜像约定）。
--  作用：预建业务库 dji_admin（主库）/ dji_log（日志库）。
--        角色（dji）与口令由 compose 的 POSTGRES_USER/POSTGRES_PASSWORD
--        环境变量（官方镜像）创建，无需在此创建。
--  幂等：已存在则跳过，可安全重复执行。
--  若改用云 RDS：删除 air-matrix-postgres 服务，并在
--        deploy/config/Database.json 把连接串指向 RDS 即可，本文件可忽略。
-- ============================================================

-- 主库
SELECT 'CREATE DATABASE "dji_admin"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'dji_admin')
\gexec

-- 日志库（与 Admin.NET 日志隔离设计一致）
SELECT 'CREATE DATABASE "dji_log"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'dji_log')
\gexec
