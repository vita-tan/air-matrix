/**
 * MQTT Broker（aedes）—— 生产加固版
 * ------------------------------------------------------------
 * 用途：让后端 dji_server 与（未来的）大疆机场/飞机通过 MQTT 通信。
 * 端口：1883（与 Dji.json -> Mqtt.Port 对应）
 *
 * 安全说明（v2.1 加固，对应架构设计 A.7#9 / A.9）：
 *  - 通过环境变量注入账号口令做鉴权：MQTT_USERNAME / MQTT_PASSWORD。
 *  - 仅当【两者都设置】时才强制校验；任一为空则退回「放行任意凭证」，
 *    以兼容本地/开发联调（无需改码即可跑通）。
 *  - 生产环境务必在 deploy/.env 中设置强口令，并与 Dji.json -> Mqtt.Username/Password 保持一致。
 *  - 公网 1883 另需腾讯云安全组按机场出网 IP 白名单收紧（见部署文档）。
 *
 * 启动：node mqtt-broker.js   （或 npm run mqtt）
 * 环境变量：MQTT_PORT(默认1883) / MQTT_USERNAME / MQTT_PASSWORD
 */
const aedes = require('aedes')();
const net = require('net');

const PORT = Number(process.env.MQTT_PORT || 1883);
const EXPECTED_USER = process.env.MQTT_USERNAME || '';
const EXPECTED_PASS = process.env.MQTT_PASSWORD || '';
// 仅当账号与口令都配置时才启用强制鉴权；否则放行任意凭证（本地联调兼容）
const AUTH_ENABLED = EXPECTED_USER.length > 0 && EXPECTED_PASS.length > 0;

aedes.authenticate = (client, username, password, callback) => {
  const u = username ? username.toString() : '';
  const p = password ? password.toString() : ''; // Buffer -> string

  if (!AUTH_ENABLED) {
    // 本地/开发：未配置 env 账号时维持「放行任意凭证」，方便后端与真机同时接入
    console.log(`[mqtt] auth(bypass) client=${client.id} user=${u || '(anon)'} (MQTT_USERNAME/PASSWORD 未设置)`);
    return callback(null, true);
  }

  const ok = u === EXPECTED_USER && p === EXPECTED_PASS;
  if (ok) {
    console.log(`[mqtt] auth(ok) client=${client.id} user=${u}`);
  } else {
    console.warn(`[mqtt] auth(fail) client=${client.id} user=${u || '(anon)'} —— 账号或口令不匹配`);
  }
  // 第二个参数为「是否允许连接」；校验失败传 false
  callback(null, ok);
};

aedes.on('client', (c) => console.log(`[mqtt] + connected  ${c.id}`));
aedes.on('clientDisconnect', (c) => console.log(`[mqtt] - disconnected ${c.id}`));
aedes.on('clientError', (c, e) => console.log(`[mqtt] ! client error ${c && c.id} ${e.message}`));
aedes.on('subscribe', (subs, c) =>
  console.log(`[mqtt] sub ${c.id} -> ${subs.map((s) => s.topic).join(', ')}`));
aedes.on('publish', (packet, client) => {
  if (!client) return; // broker 自身回流，忽略
  console.log(`[mqtt] pub ${client.id} -> ${packet.topic}`);
});

const server = net.createServer(aedes.handle);
server.listen(PORT, () => {
  console.log(`[mqtt] broker listening on 0.0.0.0:${PORT}${AUTH_ENABLED ? ' (auth=on)' : ' (auth=bypass)'}`);
});
