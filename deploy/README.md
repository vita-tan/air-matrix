# air-matrix · 线上生产部署手册（Docker Compose）

> 域名：**air-matrix.crmappai.xyz**（A 记录 → 腾讯云公网 IP `1.13.24.81`）
> 服务器：腾讯云 TencentOS Server 3.3（RHEL8 系），建议 2 核 4G 起
> 架构：nginx 单点公网入口（80/443）+ 内网服务（backend/mqtt/media/minio/postgres）
> 公网端口：`80/443/1883/1935`；内网端口：`5005/8000/9000/9001/5432`

---

## 0. 前置准备（一次性）

1. **域名解析**：在域名控制台为 `air-matrix.crmappai.xyz` 添加 **A 记录**指向 `1.13.24.81`。
2. **腾讯云安全组入站**放行：`80/tcp`、`443/tcp`、`1883/tcp`、`1935/tcp`；
   **SSH(22)** 仅对运维来源 IP 开放（禁止 `0.0.0.0/0`）。
   `5005/8000/9000/9001/5432` **不要**对公网开放。
3. **安装 Docker Engine + Compose 插件**（TencentOS/RHEL8）：
   ```bash
   sudo yum install -y yum-utils
   sudo yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
   sudo yum install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
   sudo systemctl enable --now docker
   docker compose version   # 确认 compose 插件可用
   ```

## 1. 拉取代码

```bash
sudo git clone -b develop <仓库地址> /opt/air-matrix
cd /opt/air-matrix
```

## 2. 准备敏感配置（均不入库）

```bash
# 2.1 环境变量
cp deploy/.env.example deploy/.env
vim deploy/.env          # 填 POSTGRES_PASSWORD / MINIO_* / MQTT_* / JWT_SIGNING_KEY / SM2_* / DJI_*

# 2.2 后端配置（从脱敏模板复制后改真实值）
mkdir -p deploy/config
cp dji_server/docs/config-template/* deploy/config/
```

逐个修改 `deploy/config/`：

| 文件 | 必改项 |
|---|---|
| `Database.json` | `ConnectionString` 中 `<POSTGRES_PASSWORD>` → 真实口令（两库都改）；Host 保持 `air-matrix-postgres` |
| `Upload.json` | `AccessKey`/`SecretKey` → `djicloud` 账号；`PublicBaseUrl` 保持 `https://air-matrix.crmappai.xyz`（不带 `/minio`，否则机场直传签名失败） |
| `Dji.json` | `AppId/AppKey/AppLicense` 填大疆真实值；`Mqtt.Username/Password` 与 `.env` 一致；`FileBaseUrl`/`Live.*` 保持公网地址 |
| `JWT.json` | `IssuerSigningKey` → 取 `.env` 的 `JWT_SIGNING_KEY`（64 位十六进制） |
| `App.json` | `Cryptogram.PublicKey/PrivateKey` → 新生成的 SM2 密钥对（私钥仅此文件） |

**生成 SM2 密钥对**（openssl ≥ 1.1.1）：
```bash
openssl ecparam -name SM2 -genkey -noout -out sm2.key
# 导出私钥/公钥十六进制（按项目所需的 04 开头格式），分别填入 App.json 与 .env 的 SM2_PUBLIC_KEY
```

**前端生产变量**：
```bash
cp dji_vue/.env.production.example dji_vue/.env.production
# 编辑 VITE_SM2_PUBLIC_KEY = 后端 App.json 的 Cryptogram.PublicKey（务必完全一致）
```

## 3. 构建前端

```bash
bash deploy/scripts/build-frontend.sh
```
该脚本用 `node:20` 容器执行 `npm ci && npm run build`，产出 `dji_vue/dist`，
并将其复制进命名卷 `air_matrix_dist`（nginx 只读挂载）。

## 4. 构建后端与中间件镜像

```bash
set -a; . deploy/.env; set +a   # 载入变量（compose ${VAR} 插值来源）
docker compose build
```
- backend：`dji_server/` 上下文，`Dji.Web.Entry/Dockerfile` 多阶段（sdk:10.0 publish → aspnet:10.0）。
- mqtt/media：`services/` 上下文，`Dockerfile.mqtt` / `Dockerfile.media`（media 内置 ffmpeg）。

## 5. 启动服务

```bash
bash deploy/scripts/up.sh        # 内部：source deploy/.env + docker compose up -d
# 或：set -a; . deploy/.env; set +a; docker compose up -d
```
启动顺序（由 depends_on 保证）：
`postgres/minio(healthy)` → `minio-init 建桶+账号` / `mqtt` / `media` → `backend` → `nginx` → `certbot`。
- postgres 首次启动执行 `deploy/postgres/init.sql` 预建 `dji_admin`/`dji_log` 两库；
- minio-init 自动建 `dji-media` 桶 + 匿名读 + `djicloud` 账号；
- backend 启动后按 CodeFirst 在 PG 上建表 + 种子。

## 6. 申请 HTTPS 证书

```bash
bash deploy/certbot/init-ssl.sh
```
脚本会：自签占位证书（让 nginx 先起 443）→ 启 nginx → certbot webroot 申请真实证书 → reload。

**续期**（建议宿主机 cron，每天一次）：
```bash
docker compose run --rm air-matrix-certbot renew --webroot -w /var/www/certbot \
  && docker compose exec -T air-matrix-nginx nginx -s reload
```

## 7. 验证清单

- [ ] `https://air-matrix.crmappai.xyz` 首页可打开（无证书告警）。
- [ ] `https://air-matrix.crmappai.xyz/kapi` Swagger 可访问（iframe 菜单「系统接口」指向该地址）。
- [ ] 外网 MQTT：`mosquitto_pub -h air-matrix.crmappai.xyz -p 1883 -u <MQTT_USERNAME> -P <MQTT_PASSWORD> -t test -m hi` 成功。
- [ ] RTMP：`ffmpeg -re -i test.mp4 -c copy -f flv rtmp://air-matrix.crmappai.xyz:1935/live/dji_test` 推流，浏览器经 `/live/dji_test.flv` 可拉流。
- [ ] MinIO：`https://air-matrix.crmappai.xyz/dji-media/...` 匿名可下载；用 `djicloud` AK/SK 对该地址做 SigV4 PUT 返回 200（实机上传同路径）。
- [ ] PG：`docker compose exec air-matrix-postgres psql -U dji -d dji_admin -c '\dt'` 能看到业务表。
- [ ] 大疆控制台绑定真机：MQTT 地址 `air-matrix.crmappai.xyz:1883`，填 App ID/Key/License，NTP `ntp.aliyun.com`。

## 8. 常用运维

| 操作 | 命令 |
|---|---|
| 启停 | `bash deploy/scripts/up.sh` / `bash deploy/scripts/down.sh`（`-v` 连卷删除，慎用） |
| 一键部署 | `bash deploy/scripts/deploy.sh` |
| PG 备份 | `bash deploy/scripts/backup-pg.sh`（pg_dump 两库） |
| 数据迁移（可选） | `bash deploy/scripts/migrate-db.sh`（全新环境自动跳过） |
| 看日志 | `docker compose logs -f air-matrix-backend` |

## 卷与端口速查

| 卷 | 容器内挂载 | 用途 |
|---|---|---|
| `air_matrix_pg` | postgres `/var/lib/postgresql/data` | 业务库数据 |
| `air_matrix_minio_data` | minio `/data` | 对象存储数据 |
| `air_matrix_hls` | media `/app/media` | HLS 切片 |
| `air_matrix_certs` | nginx/certbot `/etc/letsencrypt` | TLS 证书 |
| `air_matrix_www` | nginx/certbot `/var/www/certbot` | ACME 挑战 |
| `air_matrix_dist` | nginx `/usr/share/nginx/html` | 前端静态资源 |

> 注：卷在 `docker-compose.yml` 中显式命名（`name: air_matrix_*`），与本文件中的脚本引用一致。
