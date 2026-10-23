/**
 * 本地 MQTT Broker（开发/联调用）
 * ------------------------------------------------------------
 * 用于让后端 dji_server 与（未来的）大疆机场/飞机通过 MQTT 通信。
 * 端口：1883（与 Dji.json -> Mqtt.Port 对应）
 *
 * 安全说明：
 *  - 演示环境为了方便联调，authenticate 直接放行任意用户名/密码；
 *  - 生产环境请替换为 Mosquitto / EMQX，并使用 ACL + 独立账号。
 *
 * 启动：node mqtt-broker.js   （或 npm run mqtt）
 */
const aedes = require('aedes')();
const net = require('net');

const PORT = Number(process.env.MQTT_PORT || 1883);

aedes.authenticate = (client, username, password, callback) => {
  const u = username ? username.toString() : '';
  // 演示环境：放行任意凭证，便于后端(JavaServer/public)与真机同时接入
  console.log(`[mqtt] auth client=${client.id} user=${u || '(anon)'}`);
  callback(null, true);
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
server.listen(PORT, () => console.log(`[mqtt] broker listening on 0.0.0.0:${PORT}`));
