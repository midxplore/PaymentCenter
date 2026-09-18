<template>
	<el-dialog v-model="state.isShowDialog" width="900px" draggable :close-on-click-modal="false">
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
				<span> 拍照 </span>
			</div>
		</template>
		<el-scrollbar class="camera-container">
			<div v-if="state.cameraError" class="error-container">
				<el-alert :title="state.cameraError" type="error" show-icon />
				<!-- 提示不支持访问摄像头的指导 -->
				<div style="margin-top: 16px; text-align: left; background: #fdf6ec; border-radius: 4px; padding: 10px; font-size: 13px">
					<h3>提示不支持访问摄像头的配置方法：</h3>
					<ol style="margin: 8px 0 0 18px; padding: 0">
						<li class="mb15">
							在浏览器地址栏输入
							<a style="color: #409eff" href="javascript:void(0);" @click="() => comFunc.copyText(state.configUrl)">{{ state.configUrl }}</a>
							并回车
						</li>
						<li class="mb15">
							启用配置项，并在输入框粘贴当前站点地址：<a style="color: #409eff" href="javascript:void(0);" @click="() => comFunc.copyText(state.origin)">{{ state.origin }}</a>
							<el-image :src="cameraConfig" style="max-width: 100%; margin-top: 8px" />
						</li>
						<li class="mb15">重启浏览器</li>
					</ol>
				</div>

				<!-- 提示无权访问摄像头的指导 -->
				<div style="margin-top: 16px; text-align: left; background: #f6f6f6; border-radius: 4px; padding: 10px; font-size: 13px">
					<h3>提示无权访问摄像头的配置方法：</h3>
					<ol style="margin: 8px 0 0 18px; padding: 0">
						<li class="mb15">点击浏览器地址栏左侧图标</li>
						<li class="mb15">允许浏览器访问权限</li>
						<el-image :src="cameraConfig2" style="max-width: 100%; margin-top: 8px" />
					</ol>
				</div>
			</div>

			<div v-show="!state.cameraError" class="content-container">
				<!-- 左侧摄像头区域 -->
				<div class="camera-panel">
					<video ref="videoRef" autoplay playsinline muted width="500" height="350" style="background: #000"></video>
					<canvas ref="canvasRef" style="display: none"></canvas>
				</div>

				<!-- 右侧预览区域 -->
				<div class="preview-panel" style="width: 400px; height: 300px">
					<div v-if="state.capturedImage" class="image-preview" style="width: 100%; height: 100%">
						<el-image :src="state.capturedImage" fit="contain" :style="{ width: '100%', height: '100%' }" />
					</div>
					<div v-else class="placeholder">
						<i class="el-icon-picture-outline" style="font-size: 48px; color: #c0c4cc"></i>
						<p style="color: #909399; margin-top: 10px">暂无照片</p>
					</div>
				</div>
			</div>
		</el-scrollbar>

		<template #footer>
			<div class="dialog-footer">
				<el-button @click="captureImage" v-if="!state.cameraError">拍照</el-button>
				<el-button type="primary" :disabled="!state.capturedImage" @click="confirmImage" v-if="!state.cameraError">确认</el-button>
				<el-button @click="closeDialog">取消</el-button>
			</div>
		</template>
	</el-dialog>
</template>

<script lang="ts" setup name="CameraDialog">
import { reactive, ref, onUnmounted } from 'vue';
import { ElMessage } from 'element-plus';
import commonFunction from '/@/utils/commonFunction';
import cameraConfig from '/@/assets/camera-config.png';
import cameraConfig2 from '/@/assets/camera-config2.png';

// 定义组件属性
interface Props {
	imageWidth?: number;
	imageHeight?: number;
	watermarkText?: string; // 水印文字
	watermarkFontSize?: number; // 水印字体大小
	watermarkColor?: string; // 水印颜色
	watermarkDensity?: number; // 水印密度
	showWatermark?: boolean; // 是否显示水印
	watermarkAngle?: number; // 水印角度
}

const props = withDefaults(defineProps<Props>(), {
	imageWidth: 600,
	imageHeight: 400,
	watermarkText: '水印',
	watermarkFontSize: 20,
	watermarkColor: 'rgba(255, 255, 255, 0.3)',
	watermarkDensity: 0.5,
	showWatermark: false,
	watermarkAngle: -30,
});

const comFunc = commonFunction();
const videoRef = ref<HTMLVideoElement | null>(null);
const canvasRef = ref<HTMLCanvasElement | null>(null);
const streamRef = ref<MediaStream | null>(null);

const state = reactive({
	configUrl: 'chrome://flags/#unsafely-treat-insecure-origin-as-secure',
	origin: window.location.origin,
	isShowDialog: false,
	cameraError: '',
	capturedImage: '',
});

// 设置面板尺寸
const panelWidth = ref(400);
const panelHeight = ref(300);

const emits = defineEmits(['confirm']);

// 打开对话框
const openDialog = () => {
	state.isShowDialog = true;
	state.capturedImage = '';
	state.cameraError = '';

	// 根据指定的图片尺寸计算面板高度，保持宽高比
	const aspectRatio = props.imageWidth / props.imageHeight;
	panelHeight.value = Math.round(panelWidth.value / aspectRatio);

	setTimeout(() => {
		initCamera();
	}, 0);
};

// 初始化摄像头
const initCamera = async () => {
	try {
		if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
			state.cameraError = '当前浏览器不支持访问摄像头';
			return;
		}

		const constraints = {
			video: {
				facingMode: 'environment',
				width: { ideal: props.imageWidth },
				height: { ideal: props.imageHeight },
			},
			audio: false,
		};

		streamRef.value = await navigator.mediaDevices.getUserMedia(constraints);

		if (videoRef.value) {
			videoRef.value.srcObject = streamRef.value;
		}
	} catch (err: any) {
		console.error('获取摄像头权限失败:', err);
		state.cameraError = '无法访问摄像头: ' + (err.message || '未知错误');
		ElMessage.error('无法访问摄像头，请检查设备连接和权限设置');
	}
};

// 拍照
const captureImage = () => {
	if (!videoRef.value || !canvasRef.value) return;

	const video = videoRef.value;
	const canvas = canvasRef.value;
	const context = canvas.getContext('2d');
	if (!context) {
		ElMessage.error('无法获取画布上下文');
		return;
	}

	// 使用视频流的原始分辨率
	const videoWidth = video.videoWidth;
	const videoHeight = video.videoHeight;
	canvas.width = videoWidth;
	canvas.height = videoHeight;

	context.drawImage(video, 0, 0, videoWidth, videoHeight);

	if (props.showWatermark) {
		drawWatermark(context, videoWidth, videoHeight);
	}

	// 设置图片质量为最高
	state.capturedImage = canvas.toDataURL('image/jpeg', 1.0);
};

// 绘制优化排列的水印
const drawWatermark = (context: CanvasRenderingContext2D, width: number, height: number) => {
	const { watermarkText, watermarkFontSize, watermarkColor, watermarkAngle, watermarkDensity } = props;

	// 保存当前绘图状态
	context.save();

	// 设置水印样式
	context.font = `bold ${watermarkFontSize}px Arial`;
	context.fillStyle = watermarkColor;
	context.textAlign = 'left';
	context.textBaseline = 'middle';

	// 计算水印文本的尺寸
	const textMetrics = context.measureText(watermarkText);
	const textWidth = textMetrics.width;
	const textHeight = watermarkFontSize;

	// 根据密度参数计算水印间距
	const density = Math.max(0.1, Math.min(1.0, watermarkDensity));

	// 优化间距计算，使布局更友好
	// 当密度为0.1时，间距为文本宽度的4倍
	// 当密度为1.0时，间距为文本宽度的1.5倍
	const spacingFactor = 5 - density * 3;
	const spacingX = textWidth * spacingFactor;
	const spacingY = textHeight * spacingFactor * 1.2;

	// 将角度转换为弧度
	const angleInRadians = (watermarkAngle * Math.PI) / 180;

	// 旋转画布到指定角度
	context.translate(width / 2, height / 2);
	context.rotate(angleInRadians);

	// 计算绘制范围
	const diagonal = Math.sqrt(width * width + height * height);
	const startX = -diagonal / 2;
	const endX = diagonal / 2;
	const startY = -diagonal / 2;
	const endY = diagonal / 2;

	// 在旋转后的坐标系中绘制网格状水印
	for (let y = startY; y < endY; y += spacingY) {
		for (let x = startX; x < endX; x += spacingX) {
			// 创建交错网格效果，使布局更友好
			const adjustedX = x + (Math.floor((y - startY) / spacingY) % 2 === 0 ? 0 : spacingX / 2);
			context.fillText(watermarkText, adjustedX, y);
		}
	}

	// 恢复绘图状态
	context.restore();
};

// 确认使用照片
const confirmImage = () => {
	if (state.capturedImage) {
		emits('confirm', state.capturedImage);
		closeDialog();
	}
};

// 关闭对话框
const closeDialog = () => {
	state.isShowDialog = false;
	stopCamera();
};

// 停止摄像头
const stopCamera = () => {
	if (streamRef.value) {
		const tracks = streamRef.value.getTracks();
		tracks.forEach((track) => track.stop());
		streamRef.value = null;
	}

	if (videoRef.value) {
		videoRef.value.srcObject = null;
	}

	state.capturedImage = '';
};

// 组件卸载时停止摄像头
onUnmounted(() => {
	stopCamera();
});

defineExpose({ openDialog });
</script>

<style scoped>
.camera-container {
	height: 100%;
	text-align: center;
	max-height: 400px !important;
}

.content-container {
	display: flex;
	gap: 20px;
	justify-content: center;
	align-items: flex-start;
}

.camera-panel {
	width: 400px;
	height: 300px;
	border: 1px solid #dcdfe6;
	border-radius: 4px;
	overflow: hidden;
	background: #000;
	display: flex;
	justify-content: center;
	align-items: center;
}

.preview-panel {
	border: 1px solid #dcdfe6;
	border-radius: 4px;
	overflow: hidden;
	background: #f5f5f5;
	display: flex;
	justify-content: center;
	align-items: center;
}

.placeholder {
	text-align: center;
	color: #909399;
}

.error-container {
	height: 100%;
	padding: 20px;
	text-align: center;
}

.dialog-footer {
	text-align: right;
}
</style>
