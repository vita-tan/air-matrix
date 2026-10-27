<template>
	<div class="imagery-page">
		<div id="imageryCesium" class="map-canvas"></div>

		<!-- 加载遮罩 -->
		<div v-if="mapLoading" class="map-loading">
			<div class="spinner"></div>
			<p>地球加载中，请稍候…</p>
		</div>

		<!-- 左侧：在线无人机 -->
		<section class="glass device-panel">
			<header class="panel-head">
				<span class="panel-mark"><el-icon><ele-Monitor /></el-icon></span>
				<div class="panel-titles">
					<b>设备列表</b>
					<small>DEVICES LIST</small>
				</div>
				<span class="panel-count">{{ droneRows.length }}</span>
				<el-button text class="icon-btn" :loading="deviceLoading" @click="loadDevices">
					<el-icon><ele-Refresh /></el-icon>
				</el-button>
			</header>

			<div class="device-filter">
				<el-select v-model="statusFilter" size="small" class="status-select">
					<el-option label="全部" value="all" />
					<el-option label="在线" value="online" />
					<el-option label="作业中" value="working" />
					<el-option label="离线" value="offline" />
				</el-select>
				<el-input v-model="keyword" size="small" clearable placeholder="输入关键词以搜索设备">
					<template #prefix>
						<el-icon><ele-Search /></el-icon>
					</template>
				</el-input>
			</div>

			<el-scrollbar class="device-scroll">
				<div v-if="!filteredDrones.length" class="device-empty">
					<el-icon><ele-Box /></el-icon>
					<p>暂无设备</p>
				</div>
				<div
					v-for="row in filteredDrones"
					:key="row.sn"
					class="device-item"
					:class="{ active: activeSn === row.sn, offline: row.status === 'offline' }"
					@click="focusDrone(row)"
				>
					<span class="device-avatar" :style="{ '--c': statusMeta[row.status].color }">
						<el-icon><ele-Promotion /></el-icon>
					</span>
					<div class="device-main">
						<div class="device-name" :title="row.nick">{{ row.nick }}</div>
						<div class="device-meta">
							<span>{{ row.model || '无人机' }}</span>
							<span v-if="row.parentNick" class="divider">·</span>
							<span v-if="row.parentNick" :title="row.parentNick">{{ row.parentNick }}</span>
						</div>
					</div>
					<div class="device-side">
						<span class="status-chip" :style="{ color: statusMeta[row.status].color, borderColor: statusMeta[row.status].color + '66', background: statusMeta[row.status].color + '14' }">
							{{ statusMeta[row.status].text }}
						</span>
						<span v-if="row.battery != null" class="battery">
							<el-icon><ele-Lightning /></el-icon>{{ row.battery }}%
						</span>
					</div>
				</div>
			</el-scrollbar>

			<footer class="device-stats">
				<span><i style="background: #38d9ff"></i>待机 {{ counts.standby }}</span>
				<span><i style="background: #3ddc84"></i>作业 {{ counts.working }}</span>
				<span><i style="background: #ffb64d"></i>运维 {{ counts.ops }}</span>
				<span><i style="background: #7d8fa3"></i>离线 {{ counts.offline }}</span>
			</footer>
		</section>

		<!-- 右侧：底图与图层 -->
		<aside class="glass layer-panel">
			<div class="panel-caption">底图</div>
			<div class="basemap-row">
				<button class="basemap-card" :class="{ active: baseMap === 'satellite' }" @click="setBaseMap('satellite')">
					<span class="basemap-thumb satellite"></span>
					<span>卫星影像</span>
				</button>
				<button class="basemap-card" :class="{ active: baseMap === 'vector' }" @click="setBaseMap('vector')">
					<span class="basemap-thumb vector"></span>
					<span>矢量地图</span>
				</button>
			</div>

			<div class="panel-caption">图层</div>
			<div class="layer-list">
				<div v-for="item in layerItems" :key="item.key" class="layer-row">
					<span class="layer-name">
						<el-icon><component :is="item.icon" /></el-icon>
						{{ item.label }}
					</span>
					<el-switch size="small" :model-value="layers[item.key]" @change="(v) => onLayerChange(item.key, !!v)" />
				</div>
			</div>

			<div class="imagery-head">
				<span>遥感影像 <em>{{ geoMediaCount }}</em></span>
				<span class="imagery-head-actions">
					<el-button text size="small" @click="openUpload">
						<el-icon><ele-UploadFilled /></el-icon>上传
					</el-button>
					<el-button text size="small" :disabled="!geoMediaCount" @click="toggleAllOverlays">
						{{ allOverlayOn ? '清空叠加' : '全部叠加' }}
					</el-button>
				</span>
			</div>

			<el-scrollbar class="imagery-scroll">
				<div v-if="!mediaRows.length" class="imagery-empty" @click="openUpload">
					<el-icon><ele-UploadFilled /></el-icon>
					<p>暂无回传影像</p>
					<span>点击上传正射 / 多光谱成果图</span>
				</div>
				<div
					v-for="m in mediaRows"
					:key="m.uid"
					class="imagery-item"
					:class="{ on: m.overlay, disabled: !hasGeo(m) }"
					@click="onImageryClick(m)"
				>
					<img class="imagery-thumb" :src="m.url" :alt="m.fileName" loading="lazy" />
					<div class="imagery-meta">
						<div class="imagery-name" :title="m.fileName">{{ m.fileName }}</div>
						<div class="imagery-sub">{{ fmtTime(m.mediaCreateTime) }}<span v-if="m.relativeAltitude"> · {{ Math.round(m.relativeAltitude) }}m</span></div>
					</div>
					<span class="imagery-eye" :title="m.overlay ? '取消叠加' : '叠加到地图'">
						<el-icon><ele-View v-if="m.overlay" /><ele-Hide v-else /></el-icon>
					</span>
				</div>
			</el-scrollbar>
			<p class="overlay-tip">按拍摄点与相对高度估算地面覆盖范围叠加，非正射纠正</p>
		</aside>

		<!-- 左下：相机读数 -->
		<div class="glass map-readout">
			<span>{{ headingText }} {{ Math.round(readout.heading) }}°</span>
			<span class="sep"></span>
			<span>高度 {{ heightText }}</span>
		</div>

		<!-- 右下：地图工具 -->
		<div class="map-tools">
			<button class="tool-btn" title="放大" @click="zoomBy(0.4)">
				<el-icon><ele-Plus /></el-icon>
			</button>
			<button class="tool-btn" title="缩小" @click="zoomBy(-0.6)">
				<el-icon><ele-Minus /></el-icon>
			</button>
			<button class="tool-btn" title="回到浙江" @click="resetView">
				<el-icon><ele-Aim /></el-icon>
			</button>
		</div>

		<!-- 影像预览 -->
		<el-dialog v-model="previewVisible" :title="previewRow?.fileName || '影像预览'" width="760px" top="6vh" append-to-body destroy-on-close>
			<div v-if="previewRow" class="preview-box">
				<img :src="previewRow.url" :alt="previewRow.fileName" />
				<div class="preview-meta">
					<span>拍摄时间：{{ fmtTime(previewRow.mediaCreateTime) }}</span>
					<span v-if="previewRow.dockNick">回传机场：{{ previewRow.dockNick }}</span>
					<span v-if="hasGeo(previewRow)">坐标：{{ previewRow.longitude!.toFixed(6) }}, {{ previewRow.latitude!.toFixed(6) }}</span>
					<span v-else>无拍摄坐标</span>
				</div>
			</div>
			<template #footer>
				<el-button @click="previewVisible = false">关闭</el-button>
				<el-button type="primary" :disabled="!previewRow || !hasGeo(previewRow)" @click="overlayFromPreview">叠加到地图</el-button>
			</template>
		</el-dialog>

		<!-- 影像上传 -->
		<el-dialog v-model="uploadVisible" title="上传影像" width="580px" top="7vh" append-to-body destroy-on-close>
			<el-upload
				class="upload-drop"
				drag
				:auto-upload="false"
				:limit="1"
				accept=".jpg,.jpeg,.png,.webp"
				:file-list="uploadFileList"
				:on-change="onUploadPick"
				:on-remove="onUploadRemove"
				:on-exceed="onUploadExceed"
			>
				<el-icon class="el-icon--upload"><ele-UploadFilled /></el-icon>
				<div class="el-upload__text">拖拽图片到此处，或 <em>点击选择</em></div>
				<template #tip>
					<div class="el-upload__tip">支持 JPG / PNG / WebP，单张不超过 20MB；多光谱请先合成假彩色（如 NDVI）再导出</div>
				</template>
			</el-upload>

			<el-form class="upload-form" label-width="86px" label-position="left">
				<el-form-item label="中心经度" required>
					<el-input-number v-model="uploadForm.longitude" :precision="6" :step="0.000001" :controls="false" placeholder="WGS84 经度，如 120.243500" />
				</el-form-item>
				<el-form-item label="中心纬度" required>
					<el-input-number v-model="uploadForm.latitude" :precision="6" :step="0.000001" :controls="false" placeholder="WGS84 纬度，如 30.621500" />
				</el-form-item>
				<el-form-item label="相对高度">
					<el-input-number v-model="uploadForm.relativeAltitude" :min="1" :max="1000" :step="10" controls-position="right" />
					<span class="upload-unit">米，用于估算贴地范围</span>
				</el-form-item>
				<el-form-item label="偏航角">
					<el-input-number v-model="uploadForm.gimbalYawDegree" :min="-180" :max="180" :step="1" controls-position="right" />
					<span class="upload-unit">度</span>
				</el-form-item>
				<el-form-item label="拍摄时间">
					<el-date-picker v-model="uploadForm.mediaCreateTime" type="datetime" placeholder="默认当前时间" value-format="YYYY-MM-DD HH:mm:ss" />
				</el-form-item>
				<el-form-item label="任务ID">
					<el-input v-model="uploadForm.flightId" placeholder="可空，填写后媒体库可按任务筛选" clearable />
				</el-form-item>
			</el-form>

			<template #footer>
				<el-button @click="uploadVisible = false">取消</el-button>
				<el-button type="primary" :loading="uploading" @click="submitUpload">上传并叠加</el-button>
			</template>
		</el-dialog>
	</div>
</template>

<script setup lang="ts" name="imagery">
import { computed, onBeforeUnmount, onMounted, reactive, ref, shallowRef, watch } from 'vue';
import * as Cesium from 'cesium';
import { ElMessage } from 'element-plus';
import { pageDjiDevice } from '/@/api/main/djiDevice';
import { pageDjiMedia, uploadDjiMedia } from '/@/api/main/djiMedia';
import { listDjiFlyZone } from '/@/api/main/djiFlyZone';
import { useDeviceStore } from '/@/stores/useDeviceStore';
import { useWorkspaceStore } from '/@/stores/useWorkspaceStore';
import { wgs84ToGcj02 } from '/@/utils/coordTransform';

type DroneStatus = 'standby' | 'working' | 'ops' | 'offline';
type LayerKey = 'terrain' | 'boundary' | 'roads' | 'media' | 'imagery' | 'noFly' | 'flyable';

interface DroneRow {
	sn: string;
	nick: string;
	model: string;
	parentSn: string;
	parentNick: string;
	isOnline: boolean;
	status: DroneStatus;
	modeCode: number | null;
	longitude: number | null;
	latitude: number | null;
	height: number | null;
	battery: number | null;
}

interface MediaRow {
	id: number;
	uid: string;
	fileName: string;
	url: string;
	longitude: number | null;
	latitude: number | null;
	relativeAltitude?: number | null;
	absoluteAltitude?: number | null;
	gimbalYawDegree?: number | null;
	mediaCreateTime?: string;
	dockNick?: string;
	overlay?: boolean;
}

const ION_TOKEN = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiJkZjA0Mjc1OC02N2YwLTQzODAtYjY3Mi05ZThlN2YzNDY1NTEiLCJpZCI6MzYwODUxLCJpYXQiOjE3NjMyOTg4ODZ9.AKRDep447XQx5mJdGGNVr5SPeAUqQ16mioHdi1mw_YI';

const statusMeta: Record<DroneStatus, { text: string; color: string }> = {
	standby: { text: '待机', color: '#38d9ff' },
	working: { text: '作业', color: '#3ddc84' },
	ops: { text: '运维', color: '#ffb64d' },
	offline: { text: '离线', color: '#7d8fa3' },
};

const layerItems: { key: LayerKey; label: string; icon: string }[] = [
	{ key: 'terrain', label: '地形展示', icon: 'ele-Position' },
	{ key: 'boundary', label: '行政边界', icon: 'ele-MapLocation' },
	{ key: 'roads', label: '卫星路网', icon: 'ele-Guide' },
	{ key: 'media', label: '媒体数据', icon: 'ele-Picture' },
	{ key: 'imagery', label: '影像叠加', icon: 'ele-PictureRounded' },
	{ key: 'noFly', label: '禁飞区', icon: 'ele-CircleClose' },
	{ key: 'flyable', label: '适航区', icon: 'ele-CircleCheck' },
];

const deviceStore = useDeviceStore();
const spaceStore = useWorkspaceStore();

const workspaceId = ref(spaceStore.defSpace?.workspaceId ?? '');
const viewer = shallowRef<Cesium.Viewer>();
const mapReady = ref(false);
const mapLoading = ref(true);

const layers = reactive<Record<LayerKey, boolean>>({
	terrain: true,
	boundary: true,
	roads: false,
	media: true,
	imagery: true,
	noFly: true,
	flyable: true,
});

const baseMap = ref<'satellite' | 'vector'>('satellite');
const deviceLoading = ref(false);
const mediaLoading = ref(false);
const devices = ref<any[]>([]);
const docks = ref<Map<string, any>>(new Map());
const statusFilter = ref('all');
const keyword = ref('');
const activeSn = ref('');
const mediaRows = ref<MediaRow[]>([]);
const previewVisible = ref(false);
const previewRow = ref<MediaRow | null>(null);

const readout = reactive({ heading: 0, height: 0 });
const ZHEJIANG_RECT = Cesium.Rectangle.fromDegrees(118.0, 27.1, 123.2, 31.2);

let satelliteLayer: Cesium.ImageryLayer | null = null;
let vectorLayer: Cesium.ImageryLayer | null = null;
let roadsLayer: Cesium.ImageryLayer | null = null;
let boundaryDs: Cesium.GeoJsonDataSource | null = null;
let boundaryLines: Cesium.Entity[] = [];
let worldTerrain: Cesium.TerrainProvider | null = null;
let refreshTimer: number | undefined;

const droneEntityMap = new Map<string, Cesium.Entity>();
const mediaEntityMap = new Map<number, Cesium.Entity>();
const overlayEntityMap = new Map<number, Cesium.Entity>();
let zoneEntities: { entity: Cesium.Entity; isNoFly: boolean }[] = [];

/* ------------------------------ 设备 ------------------------------ */

const droneRows = computed<DroneRow[]>(() => {
	return devices.value.map((d) => {
		const live: any = deviceStore.droneOsds.get(d.sn);
		const dock = docks.value.get(d.parentSn);
		const modeCode = live?.modeCode ?? null;
		const online = !!live || !!d.isOnline;

		let status: DroneStatus = 'offline';
		if (online) {
			if (modeCode === 14) status = 'offline';
			else if (modeCode === 13) status = 'ops';
			else if (modeCode == null || modeCode === 0) status = 'standby';
			else status = 'working';
		}

		let lng = Number(live?.longitude) || Number(d.longitude) || 0;
		let lat = Number(live?.latitude) || Number(d.latitude) || 0;
		if ((!lng || !lat) && dock) {
			lng = Number(dock.longitude) || 0;
			lat = Number(dock.latitude) || 0;
		}

		return {
			sn: d.sn,
			nick: d.nick || d.nickName || d.sn,
			model: d.model || '',
			parentSn: d.parentSn || '',
			parentNick: dock?.nick || d.parentSnNick || d.parentSn || '',
			isOnline: online,
			status,
			modeCode,
			longitude: lng || null,
			latitude: lat || null,
			height: Number(live?.height ?? d.altitude) || null,
			battery: live?.battery?.capacityPercent ?? null,
		};
	});
});

const filteredDrones = computed(() => {
	const kw = keyword.value.trim().toLowerCase();
	return droneRows.value
		.filter((d) => {
			if (statusFilter.value === 'online' && !d.isOnline) return false;
			if (statusFilter.value === 'working' && d.status !== 'working') return false;
			if (statusFilter.value === 'offline' && d.status !== 'offline') return false;
			if (!kw) return true;
			return d.nick.toLowerCase().includes(kw) || d.sn.toLowerCase().includes(kw) || d.model.toLowerCase().includes(kw);
		})
		.sort((a, b) => Number(b.isOnline) - Number(a.isOnline) || a.nick.localeCompare(b.nick));
});

const counts = computed(() => {
	const c = { standby: 0, working: 0, ops: 0, offline: 0 };
	droneRows.value.forEach((d) => c[d.status]++);
	return c;
});

async function loadDevices() {
	deviceLoading.value = true;
	try {
		const res = await pageDjiDevice({ workspaceId: workspaceId.value || undefined });
		const tree = res.data.result ?? [];
		const nextDocks = new Map<string, any>();
		const list: any[] = [];
		const visit = (node: any) => {
			if (!node) return;
			const domain = Number(node.domain);
			if (domain === 3) nextDocks.set(node.sn, node);
			if (domain === 0) list.push(node);
			(node.children ?? []).forEach((child: any) => visit(child));
		};
		tree.forEach((node: any) => visit(node));
		docks.value = nextDocks;
		devices.value = list;
		syncDroneEntities();
	} catch (e) {
		console.error('[imagery] 设备加载失败', e);
	} finally {
		deviceLoading.value = false;
	}
}

function onWorkspaceChange(id: string) {
	workspaceId.value = id;
	clearMediaLayers();
	clearZones();
	loadDevices();
	loadMedia();
	loadFlyZones();
}

/* ------------------------------ 影像 ------------------------------ */

function hasGeo(m: MediaRow) {
	return m.longitude != null && m.latitude != null && m.longitude !== 0 && m.latitude !== 0;
}

const geoMediaCount = computed(() => mediaRows.value.filter(hasGeo).length);
const allOverlayOn = computed(() => {
	const rows = mediaRows.value.filter(hasGeo);
	return rows.length > 0 && rows.every((m) => m.overlay);
});

async function loadMedia() {
	mediaLoading.value = true;
	try {
		const res = await pageDjiMedia({ workspaceId: workspaceId.value || undefined, fileType: 1, page: 1, pageSize: 200 });
		const items = res.data.result?.items ?? [];
		// 后端 snowflake id 是 19 位，超出 JS 安全整数范围会丢精度；叠加/标记统一用 objectKey 作键
		mediaRows.value = items.map((m: any, i: number) => ({ ...m, uid: m.objectKey || m.url || `media-${i}`, overlay: false }));
		syncMediaEntities();
		if (layers.imagery) {
			mediaRows.value
				.filter(hasGeo)
				.slice(0, 3)
				.forEach((m) => setOverlay(m, true));
		}
	} catch (e) {
		console.error('[imagery] 影像加载失败', e);
	} finally {
		mediaLoading.value = false;
	}
}

function onImageryClick(m: MediaRow) {
	if (!hasGeo(m)) {
		ElMessage.warning('该影像缺少拍摄坐标，无法叠加到地图');
		return;
	}
	const next = !m.overlay;
	setOverlay(m, next);
	if (next) flyToPoint(m.longitude!, m.latitude!, 1600);
}

function overlayFromPreview() {
	const row = previewRow.value;
	if (!row || !hasGeo(row)) return;
	setOverlay(row, true);
	previewVisible.value = false;
	flyToPoint(row.longitude!, row.latitude!, 1600);
}

function toggleAllOverlays() {
	const target = !allOverlayOn.value;
	mediaRows.value.forEach((m) => {
		if (hasGeo(m)) setOverlay(m, target);
	});
}

function setOverlay(m: MediaRow, on: boolean) {
	const v = viewer.value;
	if (!v || !hasGeo(m)) return;

	if (on) {
		if (!overlayEntityMap.has(m.uid)) {
			const [lng, lat] = wgs84ToGcj02(m.longitude!, m.latitude!);
			// 照片不是正射成果，按相对高度与常见广角视场角估算地面覆盖范围
			const alt = Math.max(20, Number(m.relativeAltitude || m.absoluteAltitude || 120));
			const widthM = alt * 1.8;
			const heightM = widthM * 0.75;
			const yaw = -Cesium.Math.toRadians(Number(m.gimbalYawDegree ?? 0));
			const cos = Math.cos(yaw);
			const sin = Math.sin(yaw);
			const metersPerLng = 111320 * Math.cos((lat * Math.PI) / 180);
			const corners: [number, number][] = [
				[-widthM / 2, -heightM / 2],
				[widthM / 2, -heightM / 2],
				[widthM / 2, heightM / 2],
				[-widthM / 2, heightM / 2],
			].map(([x, y]) => [lng + (x * cos - y * sin) / metersPerLng, lat + (x * sin + y * cos) / 111320]);
			// 矩形贴地在当前 Cesium 版本不可用，改用贴地多边形承载影像纹理
			const entity = v.entities.add({
				polygon: {
					hierarchy: new Cesium.PolygonHierarchy(Cesium.Cartesian3.fromDegreesArray(corners.flat())),
					material: new Cesium.ImageMaterialProperty({ image: m.url, transparent: true, color: Cesium.Color.WHITE.withAlpha(0.92) }),
					classificationType: Cesium.ClassificationType.TERRAIN,
				},
				properties: { type: 'overlay', mediaId: m.uid },
			});
			entity.show = layers.imagery;
			overlayEntityMap.set(m.uid, entity);
		}
		m.overlay = true;
	} else {
		const entity = overlayEntityMap.get(m.uid);
		if (entity) {
			v.entities.remove(entity);
			overlayEntityMap.delete(m.uid);
		}
		m.overlay = false;
	}
}

/* ------------------------------ 上传影像 ------------------------------ */

const uploadVisible = ref(false);
const uploading = ref(false);
const uploadFile = ref<File | null>(null);
const uploadFileList = ref<any[]>([]);
const uploadForm = reactive({
	longitude: undefined as number | undefined,
	latitude: undefined as number | undefined,
	relativeAltitude: 120,
	gimbalYawDegree: 0,
	mediaCreateTime: '' as string,
	flightId: '',
});

function formatDateTime(d: Date) {
	const pad = (n: number) => String(n).padStart(2, '0');
	return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}

function openUpload() {
	uploadFile.value = null;
	uploadFileList.value = [];
	uploadForm.longitude = undefined;
	uploadForm.latitude = undefined;
	uploadForm.relativeAltitude = 120;
	uploadForm.gimbalYawDegree = 0;
	uploadForm.mediaCreateTime = formatDateTime(new Date());
	uploadForm.flightId = '';
	uploadVisible.value = true;
}

function resetUploadPick() {
	uploadFile.value = null;
	uploadFileList.value = [];
}

function onUploadPick(file: any, files: any[]) {
	const raw = file?.raw as File;
	if (!raw) return;
	if (!/\.(jpe?g|png|webp)$/i.test(raw.name)) {
		ElMessage.warning('仅支持 JPG / PNG / WebP，多光谱请先导出假彩色图');
		resetUploadPick();
		return;
	}
	if (raw.size > 20 * 1024 * 1024) {
		ElMessage.warning('单张影像不能超过 20MB');
		resetUploadPick();
		return;
	}
	uploadFile.value = raw;
	uploadFileList.value = files.slice(-1);
}

function onUploadRemove() {
	resetUploadPick();
}

function onUploadExceed(files: any[]) {
	// 只能传一张，重复选择时用新文件替换旧文件
	const raw = files[0] as File;
	if (!raw) return;
	uploadFile.value = raw;
	uploadFileList.value = [{ name: raw.name, raw }];
}

async function submitUpload() {
	if (!uploadFile.value) return ElMessage.warning('请选择影像文件');
	const { longitude, latitude, relativeAltitude, gimbalYawDegree } = uploadForm;
	if (longitude == null || latitude == null) return ElMessage.warning('请填写影像中心坐标');
	if (longitude < -180 || longitude > 180 || latitude < -90 || latitude > 90) return ElMessage.warning('坐标超出范围');
	if (longitude === 0 && latitude === 0) return ElMessage.warning('请填写有效坐标');

	uploading.value = true;
	try {
		const res: any = await uploadDjiMedia(uploadFile.value, {
			workspaceId: workspaceId.value,
			longitude,
			latitude,
			relativeAltitude,
			gimbalYawDegree,
			mediaCreateTime: uploadForm.mediaCreateTime || undefined,
			flightId: uploadForm.flightId || undefined,
		});
		const uploaded = res?.data?.result;
		uploadVisible.value = false;
		ElMessage.success('影像已上传');

		if (uploaded?.objectKey) {
			const row: MediaRow = {
				...uploaded,
				uid: uploaded.objectKey,
				overlay: false,
			};
			mediaRows.value = [row, ...mediaRows.value.filter((m) => m.uid !== row.uid)];
			syncMediaEntities();
			if (!layers.imagery) onLayerChange('imagery', true);
			setOverlay(row, true);
			flyToPoint(row.longitude!, row.latitude!, 1600);
		} else {
			await loadMedia();
		}
	} catch (e) {
		console.error('[imagery] 影像上传失败', e);
	} finally {
		uploading.value = false;
	}
}

function openPreview(m: MediaRow) {
	previewRow.value = m;
	previewVisible.value = true;
}

/* ------------------------------ 地图实体 ------------------------------ */

const iconCache = new Map<string, string>();

function droneIcon(color: string) {
	const cached = iconCache.get(color);
	if (cached) return cached;
	const size = 64;
	const canvas = document.createElement('canvas');
	canvas.width = size;
	canvas.height = size;
	const ctx = canvas.getContext('2d')!;
	ctx.shadowColor = color;
	ctx.shadowBlur = 10;
	ctx.beginPath();
	ctx.arc(32, 32, 19, 0, Math.PI * 2);
	ctx.fillStyle = 'rgba(4, 20, 32, 0.88)';
	ctx.fill();
	ctx.lineWidth = 2;
	ctx.strokeStyle = color;
	ctx.stroke();
	ctx.shadowBlur = 0;
	ctx.strokeStyle = color;
	ctx.lineWidth = 2;
	const rotors: [number, number][] = [
		[-8, -8],
		[8, -8],
		[-8, 8],
		[8, 8],
	];
	rotors.forEach(([dx, dy]) => {
		ctx.beginPath();
		ctx.arc(32 + dx, 32 + dy, 4, 0, Math.PI * 2);
		ctx.stroke();
		ctx.beginPath();
		ctx.moveTo(32, 32);
		ctx.lineTo(32 + dx, 32 + dy);
		ctx.stroke();
	});
	ctx.fillStyle = color;
	ctx.beginPath();
	ctx.arc(32, 32, 3.2, 0, Math.PI * 2);
	ctx.fill();
	const url = canvas.toDataURL();
	iconCache.set(color, url);
	return url;
}

let mediaIconUrl = '';

function mediaIcon() {
	if (mediaIconUrl) return mediaIconUrl;
	const size = 56;
	const canvas = document.createElement('canvas');
	canvas.width = size;
	canvas.height = size;
	const ctx = canvas.getContext('2d')!;
	ctx.fillStyle = 'rgba(4, 20, 32, 0.9)';
	ctx.strokeStyle = '#8ee8ff';
	ctx.lineWidth = 2;
	ctx.beginPath();
	ctx.roundRect(12, 16, 32, 24, 5);
	ctx.fill();
	ctx.stroke();
	ctx.beginPath();
	ctx.arc(28, 28, 6.5, 0, Math.PI * 2);
	ctx.stroke();
	ctx.beginPath();
	ctx.moveTo(22, 16);
	ctx.lineTo(26, 11);
	ctx.lineTo(33, 11);
	ctx.lineTo(36, 16);
	ctx.stroke();
	mediaIconUrl = canvas.toDataURL();
	return mediaIconUrl;
}

function syncDroneEntities() {
	const v = viewer.value;
	if (!v) return;
	const alive = new Set<string>();

	droneRows.value.forEach((d) => {
		if (d.longitude == null || d.latitude == null) return;
		alive.add(d.sn);
		const [lng, lat] = wgs84ToGcj02(d.longitude, d.latitude);
		const position = Cesium.Cartesian3.fromDegrees(lng, lat);
		const icon = droneIcon(statusMeta[d.status].color);
		const active = activeSn.value === d.sn;

		let entity = droneEntityMap.get(d.sn);
		if (!entity) {
			entity = v.entities.add({
				id: `drone-${d.sn}`,
				position,
				billboard: {
					image: icon,
					width: active ? 44 : 38,
					height: active ? 44 : 38,
					heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
					disableDepthTestDistance: Number.POSITIVE_INFINITY,
				},
				label: {
					text: d.nick,
					font: '12px "Microsoft YaHei", sans-serif',
					fillColor: Cesium.Color.fromCssColorString('#d9f3ff'),
					showBackground: true,
					backgroundColor: new Cesium.Color(0.02, 0.09, 0.15, 0.78),
					backgroundPadding: new Cesium.Cartesian2(8, 4),
					pixelOffset: new Cesium.Cartesian2(0, 26),
					verticalOrigin: Cesium.VerticalOrigin.TOP,
					disableDepthTestDistance: Number.POSITIVE_INFINITY,
				},
				properties: { type: 'drone', sn: d.sn },
			});
			droneEntityMap.set(d.sn, entity);
		} else {
			entity.position = position as any;
			if (entity.billboard) {
				entity.billboard.image = icon as any;
				entity.billboard.width = active ? 44 : 38;
				entity.billboard.height = active ? 44 : 38;
			}
			if (entity.label) entity.label.text = d.nick as any;
		}
	});

	droneEntityMap.forEach((entity, sn) => {
		if (!alive.has(sn)) {
			v.entities.remove(entity);
			droneEntityMap.delete(sn);
		}
	});
}

function syncMediaEntities() {
	const v = viewer.value;
	if (!v) return;
	mediaEntityMap.forEach((entity) => v.entities.remove(entity));
	mediaEntityMap.clear();

	mediaRows.value.forEach((m) => {
		if (!hasGeo(m)) return;
		const [lng, lat] = wgs84ToGcj02(m.longitude!, m.latitude!);
		const entity = v.entities.add({
			position: Cesium.Cartesian3.fromDegrees(lng, lat),
			billboard: {
				image: mediaIcon(),
				width: 27,
				height: 27,
				heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
				disableDepthTestDistance: Number.POSITIVE_INFINITY,
			},
			properties: { type: 'media', mediaId: m.uid },
			show: layers.media,
		});
		mediaEntityMap.set(m.uid, entity);
	});
}

function applyMediaVisibility() {
	mediaEntityMap.forEach((entity) => (entity.show = layers.media));
}

function applyOverlayVisibility() {
	overlayEntityMap.forEach((entity) => (entity.show = layers.imagery));
}

function clearMediaLayers() {
	mediaEntityMap.forEach((entity) => viewer.value?.entities.remove(entity));
	mediaEntityMap.clear();
	overlayEntityMap.forEach((entity) => viewer.value?.entities.remove(entity));
	overlayEntityMap.clear();
	mediaRows.value = [];
}

/* ------------------------------ 行政区 / 飞行区 ------------------------------ */

async function loadBoundary() {
	const v = viewer.value;
	if (!v) return;
	try {
		const base = import.meta.env.BASE_URL || '/';
		const url = `${base.endsWith('/') ? base : base + '/'}geojson/zhejiang.json`;
		const geojson = await fetch(url).then((r) => {
			if (!r.ok) throw new Error(`HTTP ${r.status}`);
			return r.json();
		});
		const ds = await Cesium.GeoJsonDataSource.load(geojson, {
			stroke: Cesium.Color.fromCssColorString('#4fe3ff'),
			fill: Cesium.Color.fromCssColorString('#14b8d4').withAlpha(0.2),
			strokeWidth: 2,
			clampToGround: true,
		});
		ds.show = layers.boundary;
		await v.dataSources.add(ds);
		boundaryDs = ds;

		// 贴地多边形无法绘制描边（Cesium 会禁用 outline），单独补一层贴地折线
		const rings: number[][][] = [];
		geojson.features.forEach((f: any) => {
			const g = f.geometry;
			if (!g) return;
			const polys = g.type === 'Polygon' ? [g.coordinates] : g.type === 'MultiPolygon' ? g.coordinates : [];
			polys.forEach((ringsOfPoly: number[][][]) => ringsOfPoly.forEach((ring) => rings.push(ring)));
		});
		const lineMaterial = Cesium.Color.fromCssColorString('#4fe3ff');
		rings.forEach((ring) => {
			if (!ring || ring.length < 3) return;
			const positions = Cesium.Cartesian3.fromDegreesArray(ring.flatMap(([lng, lat]) => [lng, lat]));
			boundaryLines.push(
				v.entities.add({
					polyline: { positions, width: 2, material: lineMaterial, clampToGround: true },
					show: layers.boundary,
				})
			);
		});
	} catch (e) {
		console.warn('[imagery] 行政边界加载失败', e);
	}
}

async function loadFlyZones() {
	const v = viewer.value;
	if (!v) return;
	try {
		const res = await listDjiFlyZone(workspaceId.value || undefined);
		const rows = res.data.result ?? [];
		rows.forEach((zone: any) => {
			const isNoFly = Number(zone.zoneType) === 1;
			const color = Cesium.Color.fromCssColorString(isNoFly ? '#ff5b5b' : '#3ddc84');
			const geometry = typeof zone.geometry === 'string' ? JSON.parse(zone.geometry) : zone.geometry;
			let entity: Cesium.Entity | null = null;

			if (zone.shape === 1 || geometry?.type === 'Point') {
				const coords = geometry?.coordinates ?? [];
				const lng = Number(Array.isArray(coords[0]) ? coords[0][0] : coords[0]);
				const lat = Number(Array.isArray(coords[0]) ? coords[0][1] : coords[1]);
				const radius = Number(zone.radius) || 0;
				if (lng && lat && radius > 0) {
					const [gLng, gLat] = wgs84ToGcj02(lng, lat);
					entity = v.entities.add({
						position: Cesium.Cartesian3.fromDegrees(gLng, gLat),
						ellipse: {
							semiMajorAxis: radius,
							semiMinorAxis: radius,
							material: color.withAlpha(0.16),
							outline: true,
							outlineColor: color.withAlpha(0.9),
							height: 0,
							heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
							classificationType: Cesium.ClassificationType.TERRAIN,
						},
					});
				}
			} else if (geometry?.type === 'Polygon' || geometry?.type === 'MultiPolygon') {
				const rings: number[][][] = geometry.type === 'MultiPolygon' ? geometry.coordinates.map((p: any) => p[0]) : [geometry.coordinates[0]];
				rings.forEach((ring) => {
					if (!Array.isArray(ring) || ring.length < 3) return;
					const positions = ring.map(([lng, lat]) => {
						const [gLng, gLat] = wgs84ToGcj02(Number(lng), Number(lat));
						return Cesium.Cartesian3.fromDegrees(gLng, gLat);
					});
					entity = v.entities.add({
						polygon: {
							hierarchy: new Cesium.PolygonHierarchy(positions),
							material: color.withAlpha(0.14),
							outline: true,
							outlineColor: color.withAlpha(0.9),
							height: 0,
							heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
							classificationType: Cesium.ClassificationType.TERRAIN,
						},
					});
				});
			}

			if (entity) {
				entity.show = isNoFly ? layers.noFly : layers.flyable;
				zoneEntities.push({ entity, isNoFly });
			}
		});
	} catch (e) {
		console.warn('[imagery] 飞行区加载失败', e);
	}
}

function applyZoneVisibility() {
	zoneEntities.forEach(({ entity, isNoFly }) => (entity.show = isNoFly ? layers.noFly : layers.flyable));
}

function clearZones() {
	zoneEntities.forEach(({ entity }) => viewer.value?.entities.remove(entity));
	zoneEntities = [];
}

/* ------------------------------ 底图与工具 ------------------------------ */

function amapLayer(style: string, host: string) {
	return new Cesium.UrlTemplateImageryProvider({
		url: `https://${host}.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=${style}&x={x}&y={y}&z={z}`,
		minimumLevel: 1,
		maximumLevel: 18,
	});
}

function setBaseMap(key: 'satellite' | 'vector') {
	baseMap.value = key;
	if (satelliteLayer) satelliteLayer.show = key === 'satellite';
	if (vectorLayer) vectorLayer.show = key === 'vector';
}

function onLayerChange(key: LayerKey, value: boolean) {
	layers[key] = value;
	if (key === 'boundary' && boundaryDs) {
		boundaryDs.show = value;
		boundaryLines.forEach((entity) => (entity.show = value));
	}
	if (key === 'roads' && roadsLayer) roadsLayer.show = value;
	if (key === 'media') applyMediaVisibility();
	if (key === 'imagery') applyOverlayVisibility();
	if (key === 'noFly' || key === 'flyable') applyZoneVisibility();
	if (key === 'terrain') toggleTerrain(value);
}

async function toggleTerrain(on: boolean) {
	const v = viewer.value;
	if (!v) return;
	if (on && !worldTerrain) {
		mapLoading.value = true;
		try {
			worldTerrain = await withTimeout(Cesium.createWorldTerrainAsync(), 9000);
			v.terrainProvider = worldTerrain;
			v.scene.globe.depthTestAgainstTerrain = true;
		} catch (e) {
			layers.terrain = false;
			ElMessage.warning('地形服务暂不可用，已回退平面地图');
		} finally {
			mapLoading.value = false;
		}
		return;
	}
	v.terrainProvider = on && worldTerrain ? worldTerrain : new Cesium.EllipsoidTerrainProvider();
	v.scene.globe.depthTestAgainstTerrain = on;
}

function flyToPoint(lng: number, lat: number, height = 2000) {
	const v = viewer.value;
	if (!v) return;
	const [gLng, gLat] = wgs84ToGcj02(lng, lat);
	const target = Cesium.Cartesian3.fromDegrees(gLng, gLat);
	v.camera.flyToBoundingSphere(new Cesium.BoundingSphere(target, 0), {
		offset: new Cesium.HeadingPitchRange(0, Cesium.Math.toRadians(-55), height),
		duration: 1.5,
	});
}

function focusDrone(row: DroneRow) {
	activeSn.value = row.sn;
	syncDroneEntities();
	if (row.longitude == null || row.latitude == null) {
		ElMessage.warning('该设备暂无定位信息');
		return;
	}
	flyToPoint(row.longitude, row.latitude, 1800);
}

function resetView() {
	viewer.value?.camera.flyTo({ destination: ZHEJIANG_RECT, duration: 1.5 });
}

function zoomBy(rate: number) {
	const v = viewer.value;
	if (!v) return;
	const height = v.camera.positionCartographic.height;
	if (rate > 0) v.camera.zoomIn(Math.max(300, height * rate));
	else v.camera.zoomOut(Math.max(600, height * Math.abs(rate)));
}

const headingText = computed(() => {
	const dirs = ['正北', '东北', '正东', '东南', '正南', '西南', '正西', '西北'];
	return dirs[Math.round(readout.heading / 45) % 8];
});

const heightText = computed(() => {
	const h = readout.height;
	if (!h) return '-';
	return h >= 1000 ? `${(h / 1000).toFixed(h >= 100000 ? 0 : 1)} km` : `${Math.round(h)} m`;
});

function updateReadout() {
	const camera = viewer.value?.camera;
	if (!camera) return;
	const heading = Cesium.Math.toDegrees(camera.heading);
	readout.heading = Math.round((((heading % 360) + 360) % 360)) % 360;
	readout.height = camera.positionCartographic?.height ?? 0;
}

/* ------------------------------ 初始化 ------------------------------ */

function withTimeout<T>(promise: Promise<T>, ms: number): Promise<T> {
	return Promise.race([promise, new Promise<T>((_, reject) => setTimeout(() => reject(new Error('timeout')), ms))]);
}

async function initMap() {
	Cesium.Ion.defaultAccessToken = ION_TOKEN;

	const credit = document.createElement('div');
	const v = new Cesium.Viewer('imageryCesium', {
		baseLayer: false,
		terrainProvider: new Cesium.EllipsoidTerrainProvider(),
		timeline: false,
		animation: false,
		baseLayerPicker: false,
		geocoder: false,
		homeButton: false,
		navigationHelpButton: false,
		sceneModePicker: false,
		infoBox: false,
		selectionIndicator: false,
		fullscreenButton: false,
		shouldAnimate: true,
		creditContainer: credit,
		contextOptions: { webgl: { alpha: true } },
	});
	viewer.value = v;

	// 地形在后台加载，避免 Ion 服务超时阻塞首屏
	Cesium.createWorldTerrainAsync()
		.then((tp) => {
			worldTerrain = tp;
			if (layers.terrain) {
				v.terrainProvider = tp;
				v.scene.globe.depthTestAgainstTerrain = true;
			}
		})
		.catch((e) => {
			console.warn('[imagery] 地形服务不可用，使用椭球地形', e);
			layers.terrain = false;
		});

	v.scene.globe.enableLighting = false;
	v.scene.globe.depthTestAgainstTerrain = false;
	v.scene.globe.baseColor = Cesium.Color.fromCssColorString('#04121e');
	v.scene.skyAtmosphere.show = true;
	v.scene.screenSpaceCameraController.minimumZoomDistance = 120;
	v.scene.screenSpaceCameraController.maximumZoomDistance = 20000000;
	v.camera.percentageChanged = 0.01;
	v.camera.changed.addEventListener(updateReadout);

	satelliteLayer = v.imageryLayers.addImageryProvider(amapLayer('6', 'webst02'));
	vectorLayer = v.imageryLayers.addImageryProvider(amapLayer('8', 'webrd02'));
	vectorLayer.show = false;
	roadsLayer = v.imageryLayers.addImageryProvider(amapLayer('8', 'webst02'));
	roadsLayer.show = false;

	v.camera.setView({ destination: ZHEJIANG_RECT });

	v.screenSpaceEventHandler.setInputAction((movement: Cesium.ScreenSpaceEventHandler.PositionedEvent) => {
		const picked = v.scene.pick(movement.position);
		if (!Cesium.defined(picked) || !picked.id || !(picked.id instanceof Cesium.Entity)) return;
		const entity = picked.id as Cesium.Entity;
		const props = entity.properties as any;
		const type = props?.type?.getValue?.();
		if (type === 'drone') {
			const sn = props?.sn?.getValue?.();
			const row = droneRows.value.find((d) => d.sn === sn);
			if (row) focusDrone(row);
		} else if (type === 'media') {
			const uid = String(props?.mediaId?.getValue?.() ?? '');
			const row = mediaRows.value.find((m) => m.uid === uid);
			if (row) openPreview(row);
		}
	}, Cesium.ScreenSpaceEventType.LEFT_CLICK);

	mapReady.value = true;
	updateReadout();
	await loadBoundary();
}

function fmtTime(value?: string) {
	if (!value) return '-';
	const date = new Date(value.includes('T') ? value : value.replace(' ', 'T'));
	if (Number.isNaN(date.getTime())) return value;
	const pad = (n: number) => String(n).padStart(2, '0');
	return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

watch(droneRows, () => syncDroneEntities(), { flush: 'post' });

watch(activeSn, () => syncDroneEntities());

onMounted(async () => {
	try {
		await initMap();
	} catch (e) {
		console.error('[imagery] 地图初始化失败', e);
		ElMessage.error('地图初始化失败，请检查网络或 Cesium 资源');
	} finally {
		mapLoading.value = false;
	}
	await Promise.allSettled([loadDevices(), loadMedia(), loadFlyZones()]);
	refreshTimer = window.setInterval(() => loadDevices(), 60000);
});

onBeforeUnmount(() => {
	window.clearInterval(refreshTimer);
	const v = viewer.value;
	if (v) {
		v.camera.changed.removeEventListener(updateReadout);
		v.screenSpaceEventHandler.removeInputAction(Cesium.ScreenSpaceEventType.LEFT_CLICK);
		v.destroy();
	}
	viewer.value = undefined;
	droneEntityMap.clear();
	mediaEntityMap.clear();
	overlayEntityMap.clear();
	zoneEntities = [];
	boundaryDs = null;
});
</script>

<style scoped lang="scss">
.imagery-page {
	position: relative;
	width: 100%;
	height: 100%;
	min-height: 560px;
	overflow: hidden;
	border-radius: 6px;
	background: #04121e;
	color: #d9ecff;
	font-size: 13px;
	--el-switch-on-color: #21c7e8;
	--el-switch-off-color: #3a5061;

	.map-canvas {
		position: absolute;
		inset: 0;

		:deep(.cesium-viewer-bottom) {
			display: none !important;
		}
		:deep(.cesium-widget canvas) {
			outline: none;
		}
	}
}

.glass {
	background: linear-gradient(180deg, rgba(9, 24, 38, 0.93), rgba(5, 15, 24, 0.88));
	border: 1px solid rgba(65, 190, 255, 0.24);
	border-radius: 10px;
	box-shadow: 0 18px 46px rgba(0, 0, 0, 0.5), inset 0 0 0 1px rgba(255, 255, 255, 0.03);
	backdrop-filter: blur(10px);
}

.map-loading {
	position: absolute;
	inset: 0;
	z-index: 20;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 12px;
	background: radial-gradient(circle at 50% 45%, rgba(8, 40, 64, 0.9), rgba(2, 9, 16, 0.96));
	color: #8fd6ff;
	letter-spacing: 1px;

	.spinner {
		width: 46px;
		height: 46px;
		border: 3px solid rgba(79, 227, 255, 0.15);
		border-top-color: #4fe3ff;
		border-radius: 50%;
		animation: spin 0.9s linear infinite;
	}
}

@keyframes spin {
	to {
		transform: rotate(360deg);
	}
}

/* ---------- 设备面板 ---------- */
.device-panel {
	position: absolute;
	left: 16px;
	top: 16px;
	bottom: 16px;
	width: 308px;
	z-index: 5;
	display: flex;
	flex-direction: column;
	overflow: hidden;
}

.panel-head {
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 12px 14px 10px;
	border-bottom: 1px solid rgba(65, 190, 255, 0.12);
}

.panel-mark {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 30px;
	height: 30px;
	border-radius: 50%;
	color: #062028;
	background: linear-gradient(135deg, #6cf0ff, #18c9a0);
	box-shadow: 0 0 18px rgba(64, 220, 255, 0.35);
	font-size: 16px;
}

.panel-titles {
	flex: 1;
	display: flex;
	flex-direction: column;
	line-height: 1.2;

	b {
		font-size: 15px;
		font-weight: 600;
		letter-spacing: 1px;
		color: #e6f8ff;
	}
	small {
		font-size: 9px;
		letter-spacing: 2px;
		color: rgba(150, 205, 235, 0.55);
		transform: scale(0.92);
		transform-origin: left center;
	}
}

.panel-count {
	min-width: 22px;
	height: 20px;
	padding: 0 7px;
	border-radius: 10px;
	background: rgba(64, 220, 255, 0.14);
	color: #6ce6ff;
	font-size: 11px;
	line-height: 20px;
	text-align: center;
}

.icon-btn {
	color: #7fb8d8;
	padding: 4px;

	&:hover {
		color: #4fe3ff;
	}
}

.device-filter {
	display: flex;
	gap: 8px;
	padding: 10px 12px;

	.status-select {
		width: 92px;
		flex: 0 0 92px;
	}
}

:deep(.device-filter .el-select__wrapper),
:deep(.device-filter .el-input__wrapper) {
	background: rgba(8, 26, 40, 0.85);
	box-shadow: 0 0 0 1px rgba(65, 190, 255, 0.18) inset;
	border-radius: 6px;
	color: #cfeaff;
}

:deep(.device-filter .el-input__inner) {
	color: #cfeaff;
}
:deep(.device-filter .el-input__inner::placeholder) {
	color: rgba(160, 200, 225, 0.45);
}
:deep(.device-filter .el-select__placeholder) {
	color: rgba(160, 200, 225, 0.6);
}

.device-scroll {
	flex: 1;
	min-height: 0;
	padding: 0 8px;
}

.device-empty {
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 10px;
	padding-top: 90px;
	color: rgba(150, 195, 220, 0.55);
	font-size: 12px;

	.el-icon {
		font-size: 34px;
		opacity: 0.65;
	}
}

.device-item {
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 10px;
	margin-bottom: 6px;
	border-radius: 8px;
	border: 1px solid transparent;
	cursor: pointer;
	transition: background 0.2s, border-color 0.2s;

	&:hover {
		background: rgba(63, 208, 255, 0.07);
	}
	&.active {
		border-color: rgba(63, 208, 255, 0.55);
		background: rgba(63, 208, 255, 0.12);
	}
	&.offline {
		opacity: 0.62;
	}
}

.device-avatar {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 34px;
	height: 34px;
	flex: 0 0 34px;
	border-radius: 50%;
	color: var(--c);
	background: color-mix(in srgb, var(--c) 14%, transparent);
	border: 1px solid color-mix(in srgb, var(--c) 45%, transparent);
	box-shadow: 0 0 14px color-mix(in srgb, var(--c) 18%, transparent);
	font-size: 16px;
}

.device-main {
	flex: 1;
	min-width: 0;
}

.device-name {
	font-size: 13px;
	color: #e3f5ff;
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.device-meta {
	margin-top: 3px;
	font-size: 11px;
	color: rgba(150, 195, 220, 0.55);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;

	.divider {
		margin: 0 4px;
	}
}

.device-side {
	display: flex;
	flex-direction: column;
	align-items: flex-end;
	gap: 4px;
}

.status-chip {
	padding: 1px 6px;
	border-radius: 4px;
	border: 1px solid;
	font-size: 10px;
	line-height: 16px;
}

.battery {
	display: inline-flex;
	align-items: center;
	gap: 2px;
	font-size: 10px;
	color: rgba(150, 205, 235, 0.7);
}

.device-stats {
	display: flex;
	justify-content: space-between;
	padding: 10px 14px;
	border-top: 1px solid rgba(65, 190, 255, 0.12);
	font-size: 11px;
	color: rgba(160, 205, 230, 0.7);

	span {
		display: inline-flex;
		align-items: center;
		gap: 5px;
	}
	i {
		width: 6px;
		height: 6px;
		border-radius: 50%;
		display: inline-block;
	}
}

/* ---------- 图层面板 ---------- */
.layer-panel {
	position: absolute;
	right: 16px;
	top: 16px;
	bottom: 16px;
	width: 272px;
	z-index: 5;
	display: flex;
	flex-direction: column;
	padding: 12px;
	overflow: hidden;
}

.panel-caption {
	margin: 2px 0 8px;
	font-size: 10px;
	letter-spacing: 2px;
	color: rgba(150, 205, 235, 0.55);
	text-transform: uppercase;
}

.basemap-row {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 8px;
	margin-bottom: 14px;
}

.basemap-card {
	display: flex;
	flex-direction: column;
	gap: 5px;
	padding: 5px;
	border-radius: 7px;
	border: 1px solid rgba(65, 190, 255, 0.16);
	background: rgba(7, 22, 34, 0.7);
	color: rgba(190, 222, 240, 0.75);
	font-size: 11px;
	cursor: pointer;
	transition: all 0.2s;

	&:hover {
		border-color: rgba(65, 190, 255, 0.45);
	}
	&.active {
		border-color: #3fe0ff;
		color: #e7fbff;
		box-shadow: 0 0 0 1px rgba(63, 224, 255, 0.35), 0 0 18px rgba(63, 224, 255, 0.18);
	}
}

.basemap-thumb {
	height: 52px;
	border-radius: 5px;
	background-size: cover;
	background-position: center;
	filter: saturate(1.05);
}

.basemap-thumb.satellite {
	background-image: linear-gradient(180deg, rgba(0, 0, 0, 0), rgba(0, 20, 32, 0.25)),
		url('https://webst02.is.autonavi.com/appmaptile?style=6&x=13&y=6&z=4');
}

.basemap-thumb.vector {
	background-image: linear-gradient(180deg, rgba(0, 0, 0, 0), rgba(0, 20, 32, 0.15)),
		url('https://webrd02.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x=13&y=6&z=4');
}

.layer-list {
	display: flex;
	flex-direction: column;
	gap: 2px;
	margin-bottom: 10px;
}

.layer-row {
	display: flex;
	align-items: center;
	justify-content: space-between;
	height: 32px;
	padding: 0 8px;
	border-radius: 6px;

	&:hover {
		background: rgba(63, 208, 255, 0.06);
	}
}

.layer-name {
	display: inline-flex;
	align-items: center;
	gap: 8px;
	color: rgba(210, 236, 250, 0.88);
	font-size: 12px;

	.el-icon {
		color: rgba(111, 214, 255, 0.75);
		font-size: 14px;
	}
}

.imagery-head {
	display: flex;
	align-items: center;
	justify-content: space-between;
	padding: 10px 4px 6px;
	border-top: 1px solid rgba(65, 190, 255, 0.12);
	font-size: 12px;
	color: #d9f1ff;

	em {
		font-style: normal;
		color: #5fe0ff;
		font-size: 11px;
		margin-left: 2px;
	}
}

.imagery-head :deep(.el-button) {
	color: #63d8ff;
	font-size: 11px;
	padding: 2px 4px;
}

.imagery-head :deep(.el-button .el-icon) {
	margin-right: 3px;
}

.imagery-head-actions {
	display: inline-flex;
	align-items: center;
	gap: 2px;
}

.imagery-scroll {
	flex: 1;
	min-height: 60px;
}

.imagery-empty {
	margin: 6px 2px;
	padding: 18px 0 16px;
	text-align: center;
	border: 1px dashed rgba(95, 208, 255, 0.22);
	border-radius: 7px;
	cursor: pointer;
	transition: all 0.2s;
	color: rgba(150, 195, 220, 0.45);

	.el-icon {
		font-size: 20px;
		color: rgba(95, 208, 255, 0.5);
	}

	p {
		margin: 6px 0 2px;
		font-size: 11px;
	}

	span {
		font-size: 10px;
		color: rgba(150, 195, 220, 0.3);
	}

	&:hover {
		border-color: rgba(63, 224, 255, 0.5);
		background: rgba(63, 224, 255, 0.06);
	}
}

.imagery-item {
	display: flex;
	align-items: center;
	gap: 8px;
	padding: 6px;
	border-radius: 7px;
	border: 1px solid transparent;
	cursor: pointer;
	transition: all 0.2s;

	&:hover {
		background: rgba(63, 208, 255, 0.07);
	}
	&.on {
		border-color: rgba(63, 224, 255, 0.4);
		background: rgba(63, 224, 255, 0.08);
	}
	&.disabled {
		opacity: 0.42;
		cursor: not-allowed;
	}
}

.imagery-thumb {
	width: 48px;
	height: 34px;
	flex: 0 0 48px;
	object-fit: cover;
	border-radius: 4px;
	border: 1px solid rgba(120, 200, 235, 0.2);
	background: #0a2030;
}

.imagery-meta {
	flex: 1;
	min-width: 0;
}

.imagery-name {
	font-size: 11px;
	color: #dcf1ff;
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.imagery-sub {
	margin-top: 2px;
	font-size: 10px;
	color: rgba(150, 195, 220, 0.5);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.imagery-eye {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 22px;
	height: 22px;
	color: rgba(140, 200, 230, 0.55);
	font-size: 13px;

	.imagery-item.on & {
		color: #5fe0ff;
	}
}

.overlay-tip {
	margin: 6px 2px 0;
	font-size: 10px;
	line-height: 1.5;
	color: rgba(150, 195, 220, 0.38);
}

/* ---------- 上传弹窗 ---------- */
.upload-drop :deep(.el-upload-dragger) {
	padding: 20px 10px;
}

.upload-form {
	margin-top: 14px;

	:deep(.el-form-item) {
		margin-bottom: 12px;
	}

	:deep(.el-input-number),
	:deep(.el-date-editor.el-input) {
		width: 230px;
	}

	:deep(.el-input) {
		width: 100%;
	}
}

.upload-unit {
	margin-left: 8px;
	font-size: 12px;
	color: var(--el-text-color-secondary);
}

/* ---------- 读数与工具 ---------- */
.map-readout {
	position: absolute;
	left: 340px;
	bottom: 16px;
	z-index: 4;
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 7px 14px;
	border-radius: 8px;
	font-family: 'JetBrains Mono', Consolas, monospace;
	font-size: 11px;
	letter-spacing: 0.5px;
	color: #9fd8ff;

	.sep {
		width: 1px;
		height: 11px;
		background: rgba(120, 200, 235, 0.25);
	}
}

.map-tools {
	position: absolute;
	right: 300px;
	bottom: 16px;
	z-index: 4;
	display: flex;
	gap: 8px;
}

.tool-btn {
	width: 34px;
	height: 34px;
	border-radius: 8px;
	border: 1px solid rgba(65, 190, 255, 0.24);
	background: rgba(6, 18, 29, 0.88);
	color: #9fd8ff;
	display: inline-flex;
	align-items: center;
	justify-content: center;
	cursor: pointer;
	font-size: 14px;
	transition: all 0.2s;

	&:hover {
		border-color: #3fe0ff;
		color: #6ce6ff;
		box-shadow: 0 0 14px rgba(63, 224, 255, 0.2);
	}
}

/* ---------- 预览 ---------- */
.preview-box {
	img {
		width: 100%;
		max-height: 62vh;
		object-fit: contain;
		background: #0b1520;
		border-radius: 4px;
	}
}

.preview-meta {
	display: flex;
	flex-wrap: wrap;
	gap: 14px;
	margin-top: 10px;
	font-size: 12px;
	color: #5c6b7a;
}

@media (max-width: 1280px) {
	.device-panel {
		width: 268px;
	}
	.layer-panel {
		width: 244px;
	}
	.map-readout {
		left: 300px;
	}
	.map-tools {
		right: 272px;
	}
}
</style>
