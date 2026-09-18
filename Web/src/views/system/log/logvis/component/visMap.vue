<template>
	<div class="visMap-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-MapLocation /> </el-icon>
					<span> {{ props.title }} </span>
				</div>
			</template>

			<div id="map"></div>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="visMap">
import { nextTick, reactive, ref } from 'vue';

import 'ol/ol.css';
import Map from 'ol/Map';
import View from 'ol/View';
import OSM from 'ol/source/OSM';
import XYZ from 'ol/source/XYZ';
import TileWMS from 'ol/source/TileWMS';
import { Heatmap as HeatmapLayer, Tile as TileLayer } from 'ol/layer';
import Feature from 'ol/Feature';
import { Geometry, Point } from 'ol/geom';
import VectorSource from 'ol/source/Vector';
import { transform } from 'ol/proj';

import gcoord from 'gcoord';

import { getAPI } from '/@/utils/axios-utils';
import { SysLogVisApi } from '/@/api-services/system/api';

const emits = defineEmits(['pickupCoord']);
const props = defineProps({
	title: String,
	isPickupCoord: Boolean,
});
const state = reactive({
	isShowDialog: false,
});
let map: Map | undefined = undefined;

// 初始化地图
const initMap = () => {
	// OpenStreetMap 图层
	const osmLayer = new TileLayer({
		source: new OSM(),
	});

	// ArcGIS地图
	const arcGISLayer = new TileLayer({
		source: new XYZ({
			url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}',
			crossOrigin: 'anonymous',
		}),
	});

	// WMS 图层 - Geoserver 服务
	const wmsLayer = new TileLayer({
		source: new TileWMS({
			url: 'https://your-geoserver-url/wms',
			params: {
				LAYERS: 'your-layer-name',
				TILED: true,
			},
		}),
	});

	// 高德地图的 XYZ 瓦片源-卫星图
	const amapLayer1 = new TileLayer({
		source: new XYZ({
			url: 'http://webst0{1-4}.is.autonavi.com/appmaptile?style=7&x={x}&y={y}&z={z}&lang=zh_cn&scale=1&size=1',
		}),
	});
	// 高德地图的 XYZ 瓦片源-卫星图
	const amapLayer2 = new TileLayer({
		source: new XYZ({
			url: 'http://webst0{1-4}.is.autonavi.com/appmaptile?style=6&x={x}&y={y}&z={z}&lang=zh_cn&scale=1&size=1',
		}),
	});
	// 高德地图的 XYZ 瓦片源-道路图
	const amapLayer3 = new TileLayer({
		source: new XYZ({
			url: 'http://webst0{1-4}.is.autonavi.com/appmaptile?style=8&x={x}&y={y}&z={z}&lang=zh_cn&scale=1&size=1',
		}),
	});

	var gd = gcoord.transform(
		[104.88, 30.22], // 经纬度坐标
		gcoord.WGS84, // 当前坐标系
		gcoord.EPSG3857 // 目标坐标系
	);

	map = new Map({
		target: 'map',
		// layers: [amapLayer1],
		layers: [amapLayer2, amapLayer3],
		view: new View({
			// projection: 'EPSG:4326', // WGS84坐标系
			projection: 'EPSG:3857', // Web墨卡托坐标系
			center: gd, // 初始中心点
			zoom: 4, // 初始缩放级别
		}),
	});

	// 拾取经纬度坐标
	map.on('singleclick', function (e) {
		let coordinate = transform(e.coordinate, 'EPSG:3857', 'EPSG:4326');
		emits('pickupCoord', coordinate);
		if (props.isPickupCoord) state.isShowDialog = false;
	});
};

// 打开弹窗
const openDialog = () => {
	state.isShowDialog = true;

	if (map == undefined) {
		nextTick(() => {
			initMap();

			// 拾取经纬度坐标模式不加载日志数据
			if (!props.isPickupCoord) loadVidLog();
		});
	}
};

// 加载热力图-访问日志数据
const loadVidLog = async () => {
	var res = await getAPI(SysLogVisApi).apiSysLogVisListGet();
	var pts = res.data.result ?? [];

	if (pts.length > 0) {
		var featrues: Feature<Point>[] = [];
		pts.forEach(function (item: any) {
			var gd = gcoord.transform([item.longitude, item.latitude], gcoord.WGS84, gcoord.EPSG3857);
			let feature = new Feature({ geometry: new Point(gd) });
			featrues.push(feature);
		});

		// 初始化热力图层
		var heatmapVectorSource = new VectorSource({
			features: featrues,
		});
		var heatmapLayer = new HeatmapLayer({
			source: heatmapVectorSource,
			radius: 5, // 半径大小
			blur: 20, // 模糊
			// gradient: ['#00f', '#0ff', '#0f0', '#ff0', '#f00'], // 热力图颜色
		});
		map?.addLayer(heatmapLayer);
	}
};

// 导出对象
defineExpose({ openDialog });
</script>

<style scoped lang="scss">
:deep(.el-overlay .el-overlay-dialog .el-dialog__body) {
	padding: 0 !important;
	background-color: transparent !important;
}
:deep(.el-dialog) {
	--el-dialog-padding-primary: 0px;
}
:deep(.el-dialog__header) {
	margin: 0 !important;
}
#map {
	width: 100%;
	height: 800px;
	background-color: transparent !important;
}
</style>
