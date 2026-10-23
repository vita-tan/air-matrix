# 无人机管控平台 · 本地联调与接真机手册

> 本文档对应「空平台 → 可接真机」这一步：MQTT broker、流媒体服务、大疆凭证、NTP 校时。

## 一、组件与端口总览

| 组件 | 位置 | 端口 | 说明 |
|---|---|---|---|
| MQTT Broker (aedes) | `services/mqtt-broker.js` | **1883** | 本地联调；放行任意账号 |
| 流媒体 (node-media-server) | `services/media-server.js` | **1935**(RTMP) / **8000**(HTTP-FLV+HLS) | SRS 的 Windows 轻量替代 |
| 后端 dji_server | `dji_server/` | **5005** | Swagger: `/kapi` |
| 前端 dji_vue | `dji_vue/` | **8888** | 登录 `superadmin / 123456`（免验证码） |

本机局域网 IP（接真机时机场要访问）：**192.168.3.116**（WLAN）/ **10.19.128.123**（以太网，按机场所在网段选用）。

## 二、启动

双击 `start_dji.bat`，会开 4 个终端窗口：MQTT → 流媒体 → 后端 → 前端。

> 若本机装有 Docker，也可改用 Mosquitto / EMQX + SRS（见第五节），本手册的本地服务仅用于开发联调。

## 三、已验证（实测通过）

- MQTT：后端启动后成功连接 broker，并订阅全部 9 个主题（`sys/product/+/status`、`thing/product/+/osd|state|requests|requests_reply|services_reply|property/set_reply|events|drc/up`）。
- 流媒体：ffmpeg 推 RTMP → HTTP-FLV 拉流（FLV 头正确、500KB+ 数据）→ HLS 拉流（`#EXTM3U` + `.ts` 切片）均正常。
- 关键修复：后端 `MQTTnet 5.2` 默认用 **MQTT 5.0** 握手，而大疆上云 API 与 aedes 均为 **MQTT 3.1.1**，会被拒连。已在 `MqttService.cs` 固定 `.WithProtocolVersion(V311)`（与 DJI 规范一致，兼容 aedes / Mosquitto / EMQX）。

## 四、配置位置（`dji_server/Dji.Application/Configuration/Dji.json`）

当前值（本地联调）：

```jsonc
"Dji": {
  "AppId": "",              // ← 待填：大疆开发者 App ID
  "AppKey": "",             // ← 待填：大疆开发者 App Key
  "AppLicense": "",         // ← 待填：大疆开发者 App License
  "NtpServerHost": "ntp.aliyun.com",
  "NtpServerPort": 123,
  "FileBaseUrl": "http://192.168.3.116:5005",   // KMZ 下发用（机场需可达）
  "Live": {
    "Enabled": true,
    "RtmpPushBaseUrl": "rtmp://192.168.3.116:1935/live",   // 已配好：本机局域网IP
    "FlvPlayBaseUrl": "http://192.168.3.116:8000/live",
    "HlsPlayBaseUrl": "http://192.168.3.116:8000/live"
  }
},
"Mqtt": {
  "Server": "127.0.0.1", "Port": 1883,           // 后端连本地broker，保持127.0.0.1
  "Username": "JavaServer", "Password": "public"
}
```

> 注意：**大疆凭证只填后端 `Dji.json`**。前端 `.env` 里没有 `VITE_APP_ID/KEY/LICENSE`（已核实前端不读这些变量），不要重复填。

## 五、接真机步骤

1. **申请大疆凭证**：登录 <https://developer.dji.com/user/apps/> 创建 App，拿到 `App ID / App Key / App License`，填入 `Dji.json` 的 `Dji.AppId/AppKey/AppLicense`，重启后端。
2. **机场绑定云端**：在机场（DJI Dock）侧配置云平台为「自定义」：
   - MQTT 地址：`<本机局域网IP>:1883`（如 `192.168.3.116:1883`）
   - 填写同一个 App ID / App Key / License
   绑定成功后，后端 broker 日志会看到机场 SN 的 `sys/product/+/status` 上线报文。
3. ~~**改推流地址**~~ ✅ **已完成（2026-10-24）**：`RtmpPushBaseUrl` = `rtmp://192.168.3.116:1935/live`，`FileBaseUrl` = `http://192.168.3.116:5005`，`Flv/HlsPlayBaseUrl` = `http://192.168.3.116:8000/live`。已用该地址端到端验证（ffmpeg 推流 → HTTP-FLV/HLS 拉流均正常）。
   > 若机场在**另一网段**（接的是以太网 `10.19.128.123`），把这三处与 `FileBaseUrl` 里的 IP 换成 `10.19.128.123`，重启后端即可。
4. **放行防火墙**（管理员 PowerShell）。一键脚本已备好，**右键"使用 PowerShell 运行"**：
   ```
   E:\program\wurenji\drone-platform\setup-firewall.ps1
   ```
   等价的手动命令：
   ```powershell
   New-NetFirewallRule -DisplayName "DJI-MQTT-1883" -Direction Inbound -Protocol TCP -LocalPort 1883 -Action Allow -Profile Any
   New-NetFirewallRule -DisplayName "DJI-RTMP-1935" -Direction Inbound -Protocol TCP -LocalPort 1935 -Action Allow -Profile Any
   New-NetFirewallRule -DisplayName "DJI-Media-HTTP-8000" -Direction Inbound -Protocol TCP -LocalPort 8000 -Action Allow -Profile Any
   New-NetFirewallRule -DisplayName "DJI-Backend-API-5005" -Direction Inbound -Protocol TCP -LocalPort 5005 -Action Allow -Profile Any
   ```
   > 当前网络 `tuhao1301` 为 **Public** 类型，默认阻止入站；上面用 `-Profile Any` 已覆盖。四个端口均已确认绑定 `0.0.0.0`，规则生效后机场即可访问。
5. **NTP 校时**（机场与云端偏差 >30s 会拒绝立即任务）。机场侧已由 `NtpServerHost` 下发；本机建议也校时（管理员 PowerShell，本会话无权限执行）：
   ```powershell
   w32tm /config /manualpeerlist:"ntp.aliyun.com,0x9" /syncfromflags:manual /update
   net start w32time
   w32tm /resync
   w32tm /query /status
   ```

## 六、生产环境替换建议

| 本地联调 | 生产建议 |
|---|---|
| aedes | **Mosquitto 2.x** 或 **EMQX 5.x**（支持 MQTT 3.1.1+5.0、ACL、持久化、TLS） |
| node-media-server | **SRS**（RTMP + HTTP-FLV + HLS，生产级并发） |

换 Mosquitto/EMQX 后，后端只需改 `Mqtt.Server/Port/Username/Password`；协议 3.1.1 两者都支持，无需改代码。换 SRS 后，把 `Live` 三个地址指向 SRS 的 1935/8080 即可。

## 七、排障

- **MQTT 连不上**：① 确认 broker 在 `1883` 监听；② 抓 broker 日志看有无 `auth/connected`；③ 若用 MQTT 5-only 客户端对接 aedes 会握手失败（本案已固定 3.1.1）。
- **直播黑屏**：① 机场推流地址是否为**本机局域网 IP**（不是 127.0.0.1）；② 防火墙 1935/8000 是否放行；③ 用 ffmpeg 推一路测试流验证流媒体本身正常。
- **立即任务被拒**：多为时钟偏差，检查 `NtpServerHost` 与机场时钟。
