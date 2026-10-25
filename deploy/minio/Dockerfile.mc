FROM alpine:3.19
# minio-init 使用：仅需要 mc 客户端（init.sh 由 compose 挂载执行）
COPY mc /usr/local/bin/mc
RUN chmod +x /usr/local/bin/mc
ENTRYPOINT ["/usr/local/bin/mc"]
