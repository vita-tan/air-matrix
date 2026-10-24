# 农脑无人机智能管理系统 · air-matrix

基于**大疆上云 API（DJI Cloud API）**的无人机智能管控平台：机巢/无人机管理、实时图传、GIS 图层叠加、航线任务、媒体与日志。

```
┌──────────────────────────────────────────────────────────────┐
│  前端 dji_vue (Vue3 + Cesium)          :8888                  │
│  后端 dji_server (.NET 10 + Furion)    :5005   Swagger: /kapi │
│  MQTT Broker (aedes)                   :1883                  │
│  流媒体 (RTMP / HTTP-FLV / HLS)        :1935 / :8000          │
└──────────────────────────────────────────────────────────────┘
```

## 目录结构

| 目录 | 说明 |
|---|---|
| `dji_server/` | .NET 10 后端（Furion + SqlSugar + SQLite），对接大疆上云 API |
| `dji_vue/` | Vue3 + Cesium 管理前端 |
| `services/` | 本地联调服务：MQTT Broker + RTMP/HTTP-FLV/HLS 流媒体 |
| `design/prototype/` | UI 设计原型（深色沉浸版 + 浅色工作台叠加版） |
| `start_dji.bat` | Windows 一键启动 4 个组件 |

## 快速启动

```bat
start_dji.bat
```

| 服务 | 地址 |
|---|---|
| 前端 | http://localhost:8888 |
| 后端 | http://localhost:5005 （Swagger `/kapi`） |
| MQTT | 127.0.0.1:1883 |
| 流媒体 | rtmp://127.0.0.1:1935/live · http://127.0.0.1:8000/live |

登录：`superadmin / 123456`（本地演示环境已关闭图形验证码）。

## ⚠️ 敏感配置（仓库不含，需自行准备）

以下内容**故意不纳入版本库**，首次运行必须自行准备：

1. 复制配置模板到配置目录：
   ```
   dji_server/docs/config-template/*.json  →  dji_server/Dji.Application/Configuration/
   ```
2. 生成 SM2 国密密钥对（或沿用既有密钥），填写 `Configuration/App.json` 的
   `Cryptogram:PublicKey` / `Cryptogram:PrivateKey`；
   同时把同一个公钥填到前端 `dji_vue/.env.development` 的 `VITE_SM2_PUBLIC_KEY`。
3. 填写 `Configuration/JWT.json` 的 `IssuerSigningKey`。
4. 大疆开发者凭证填 `Configuration/Dji.json` 的 `Dji:AppId / AppKey / AppLicense`
   （申请：<https://developer.dji.com/user/apps/>，App Type 选 **Cloud API**）。

> 数据库为 SQLite，首次启动自动建库建表（CodeFirst）。

## 近期变更

- 工作台首页在保留原有指标卡与图表的前提下，叠加「态势总览 / 今日飞行任务 / 事件列表」。
- 顶栏左上角新增「更多」全功能菜单：动态读取权限菜单（分组 + 关键字搜索），全部功能统一入口。
- 新增本地 MQTT Broker 与 RTMP/HTTP-FLV/HLS 流媒体，无需 Docker 即可跑通直播链路。
- 修复 MQTT 协议版本问题：客户端固定 `MqttProtocolVersion.V311`（大疆上云 API 使用 3.1.1）。

详见 [`README-MQTT-流媒体-接真机.md`](./README-MQTT-流媒体-接真机.md)。

## 分支策略

| 分支 | 用途 |
|---|---|
| `main` | 稳定版，随时可部署 |
| `develop` | 日常开发集成分支，功能分支合并到这里 |

```bash
git checkout develop        # 开发
git checkout -b feat/xxx    # 拉功能分支
```

## 技术栈

`.NET 10` · `Furion / Admin.NET` · `SqlSugar + SQLite` · `MQTTnet` · `Vue 3` · `Vite` · `Cesium` · `Element Plus` · `ECharts` · `Node.js`（aedes / node-media-server）
