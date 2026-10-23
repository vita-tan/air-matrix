/**
 * 本地流媒体服务（SRS 的 Windows 轻量替代）
 * ------------------------------------------------------------
 * 用途：无人机直播链路验证，无需 Docker 即可跑通
 *   - RTMP 推流入口   : rtmp://127.0.0.1:1935/live/<stream>
 *   - HTTP-FLV 拉流   : http://127.0.0.1:8000/live/<stream>.flv    （低延迟，前端 flv.js 首选）
 *   - HLS 拉流        : http://127.0.0.1:8000/live/<stream>/index.m3u8 （兼容性兜底，需 ffmpeg）
 *
 * 与 Dji.json -> Live 的对应关系：
 *   RtmpPushBaseUrl = rtmp://127.0.0.1:1935/live
 *   FlvPlayBaseUrl  = http://127.0.0.1:8000/live
 *   HlsPlayBaseUrl  = http://127.0.0.1:8000/live
 *
 * 启动：node media-server.js   （或 npm run media）
 * 可选环境变量：FFMPEG_PATH（默认取本机 ffmpeg；缺失时仅 HLS 不可用，HTTP-FLV 仍正常）
 */
const NodeMediaServer = require('node-media-server');

const FFMPEG = process.env.FFMPEG_PATH || 'D:/ffmpeg/ffmpeg-master-latest-win64-gpl/bin/ffmpeg.exe';

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
    mediaroot: 'E:/program/wurenji/drone-platform/services/media' // HLS 切片输出目录（需可写）
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
nms.run();
console.log(`[media] RTMP :1935 + HTTP-FLV/HLS :8000 started (ffmpeg=${FFMPEG})`);
