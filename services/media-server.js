/**
 * 流媒体服务（node-media-server）—— 容器化适配版
 * ------------------------------------------------------------
 * 用途：无人机直播链路，单机够用（后续可换 SRS 做集群）。
 *   - RTMP 推流入口   : rtmp://0.0.0.0:1935/live/<stream>
 *   - HTTP-FLV 拉流   : http://0.0.0.0:8000/live/<stream>.flv    （低延迟，前端 flv.js 首选）
 *   - HLS 拉流        : http://0.0.0.0:8000/live/<stream>/index.m3u8 （兼容性兜底，需 ffmpeg）
 *
 * 与 Dji.json -> Live 的对应关系（生产经 nginx /live 反代，对外为 HTTPS）：
 *   RtmpPushBaseUrl = rtmp://air-matrix.crmappai.xyz:1935/live
 *   FlvPlayBaseUrl  = https://air-matrix.crmappai.xyz/live
 *   HlsPlayBaseUrl  = https://air-matrix.crmappai.xyz/live
 *
 * 容器化要点（v2.1）：
 *   - mediaroot 指向可写卷（默认 /app/media，由 compose 的 dji_hls 卷挂载），不依赖宿主机绝对路径。
 *   - FFMPEG_PATH 指向镜像内置 ffmpeg（Dockerfile.media 已安装），缺失时仅 HLS 不可用。
 *   - RTMP_PORT / MEDIA_HTTP_PORT 由环境变量注入，便于 compose 编排。
 *
 * 启动：node media-server.js   （或 npm run media）
 */
const NodeMediaServer = require('node-media-server');

const FFMPEG = process.env.FFMPEG_PATH || 'ffmpeg'; // 容器内走 PATH 中的 ffmpeg（Dockerfile.media 已装）
const MEDIA_ROOT = process.env.MEDIA_ROOT || '/app/media'; // HLS 切片输出目录（需可写）

const config = {
  rtmp: {
    port: Number(process.env.RTMP_PORT || 1935),
    chunk_size: 60000,
    gop_cache: true,
    ping: 30,
    ping_timeout: 60
  },
  http: {
    port: Number(process.env.MEDIA_HTTP_PORT || 8000),
    allow_origin: '*',
    mediaroot: MEDIA_ROOT
  },
  // HLS 兜底（兼容性好、延迟稍高）；HTTP-FLV 不依赖 ffmpeg
  trans: {
    ffmpeg: FFMPEG,
    tasks: [
      { app: 'live', hls: true, hlsFlags: '[hls_time=2:hls_list_size=3:hls_flags=delete_segments]' }
    ]
  }
};

const nms = new NodeMediaServer(config);
nms.on('preConnect', (id) => console.log(`[media] preConnect ${id}`));
nms.on('postPublish', (id, streamPath) => console.log(`[media] postPublish ${streamPath}`));
nms.on('donePublish', (id, streamPath) => console.log(`[media] donePublish ${streamPath}`));
nms.on('error', (e) => console.error(`[media] error`, e));
nms.run();
console.log(`[media] RTMP :${config.rtmp.port} + HTTP-FLV/HLS :${config.http.port} started (mediaroot=${MEDIA_ROOT}, ffmpeg=${FFMPEG})`);
