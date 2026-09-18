<template>
	<div>
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="1140">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Promotion /> </el-icon>
					<span> 页面动画效果 </span>
				</div>
			</template>

			<div style="display: flex; flex-wrap: wrap; text-align: center; width: 100%">
				<template v-for="(itm, idx) in state.animateLidt" :key="idx">
					<div
						v-if="itm.isShow"
						:class="itm.name"
						class="transBox animate__animated animate__delay-.2s"
						:style="`${idx == state.idx ? 'background: var(--el-color-primary);color:#FFF' : ''}`"
						@click="cliAnimate(itm, idx)"
					>
						{{ itm.label || itm.name }}
					</div>
					<div v-else class="transBox">{{ itm.label || itm.name }}</div>
				</template>
			</div>

			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="cancel">取 消</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit">确 定</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup>
import { reactive } from 'vue';

const emits = defineEmits(['getAnimation']);
const state = reactive({
	idx: 999,
	isShowDialog: false,
	animateLidt: [
		{ name: 'animate__bounce', isShow: true, value: 'bounce', label: '弹跳' },		
		{ name: 'animate__flash', isShow: true, value: 'flash', label: '闪烁' },
		{ name: 'animate__rubberBand', isShow: true, value: 'rubberBand', label: '拉伸带' },
		{ name: 'animate__bounceIn', isShow: true, value: 'bounceIn', label: '弹簧' },
		{ name: 'animate__shakeX', isShow: true, value: 'shakeX', label: '左右晃动' },
		{ name: 'animate__shakeY', isShow: true, value: 'shakeY', label: '上下晃动' },
		{ name: 'animate__tada', isShow: true, value: 'tada', label: '摆动' },
		{ name: 'animate__swing', isShow: true, value: 'swing', label: '左右扇形摇摆' },
		{ name: 'animate__wobble', isShow: true, value: 'wobble', label: '扇形摇摆' },
		{ name: 'animate__jello', isShow: true, value: 'jello', label: '上下拉扯晃动' },
		{ name: 'animate__bounceInDown', isShow: true, value: 'bounceInDown', label: '向下弹入' },
		{ name: 'animate__bounceInRight', isShow: true, value: 'bounceInRight', label: '向左弹入' },
		{ name: 'animate__bounceInLeft', isShow: true, value: 'bounceInRight', label: '向右弹入' },
		{ name: 'animate__bounceInUp', isShow: true, value: 'bounceInUp', label: '向上弹入' },
		{ name: 'animate__fadeIn', isShow: true, value: 'fadeIn', label: '渐显' },
		{ name: 'animate__fadeInUp', isShow: true, value: 'fadeInUp', label: '向上进入' },
		{ name: 'animate__fadeInDown', isShow: true, value: 'fadeInDown', label: '向下进入' },
		{ name: 'animate__fadeInRight', isShow: true, value: 'fadeInLeft', label: '向右进入' },
		{ name: 'animate__fadeInLeft', isShow: true, value: 'fadeInLeft', label: '向右进入' },
		{ name: 'animate__fadeInTopLeft', isShow: true, value: 'fadeInTopLeft', label: '左上方进入' },
		{ name: 'animate__fadeInTopRight', isShow: true, value: 'fadeInTopRight', label: '右上方进入' },
		{ name: 'animate__fadeInBottomLeft', isShow: true, value: 'fadeInBottomLeft', label: '左下方进入' },
		{ name: 'animate__fadeInBottomRight', isShow: true, value: 'fadeInBottomRight', label: '右下方进入' },
		{ name: 'animate__fadeInUpBig', isShow: true, value: 'fadeInUpBig', label: '向上长距离进入' },
		{ name: 'animate__fadeInDownBig', isShow: true, value: 'fadeInDownBig', label: '向下长距离进入' },
		{ name: 'animate__fadeInRightBig', isShow: true, value: 'fadeInRightBig', label: '向左长距离进入' },
		{ name: 'animate__fadeInLeftBig', isShow: true, value: 'fadeInLeftBig', label: '向右长距离进入' },
		{ name: 'animate__lightSpeedInRight', isShow: true, value: 'lightSpeedInRight', label: '光速从右侧进入' },
		{ name: 'animate__lightSpeedInLeft', isShow: true, value: 'lightSpeedInLeft', label: '光速从左侧进入' },
		{ name: 'animate__zoomIn', isShow: true, value: 'zoomIn', label: '由小变大进入' },
		{ name: 'animate__zoomInLeft', isShow: true, value: 'zoomInLeft', label: '左变大进入' },
		{ name: 'animate__zoomInRight', isShow: true, value: 'zoomInRight', label: '右变大进入' },
		{ name: 'animate__zoomInDown', isShow: true, value: 'zoomInDown', label: '向下变大进入' },
		{ name: 'animate__zoomInUp', isShow: true, value: 'zoomInUp', label: '向上变大进入' },
		{ name: 'animate__rotateIn', isShow: true, value: 'rotateIn', label: '旋转进入' },
		// { name: 'animate__flip', isShow: true, value: 'flip', label: 'Y轴旋转' },
		{ name: 'animate__flipInY', isShow: true, value: 'flipInY', label: '中心Y轴旋转' },
		{ name: 'animate__rotateInDownLeft', isShow: true, value: 'rotateInDownLeft', label: '左顺时针旋转' },
		{ name: 'animate__rotateInDownRight', isShow: true, value: 'rotateInDownRight', label: '左逆时针旋转' },
		{ name: 'animate__rotateInUpLeft', isShow: true, value: 'rotateInUpLeft', label: '右逆时针旋转' },
		{ name: 'animate__rotateInUpRight', isShow: true, value: 'rotateInUpRight', label: '右顺时针旋转' },
	],
});

// 打开弹窗
const openDialog = async (row: any) => {
	if (row.value) {
		state.animateLidt.forEach((itm, idx) => {
			if (itm.value == row.value) return (state.idx = idx);
		});
	}
	state.isShowDialog = true;
};

// 取消
const cancel = () => {
	state.isShowDialog = false;
};

const submit = () => {
	if (state.idx == 999) return cancel();
	emits('getAnimation', state.animateLidt[state.idx]);
	cancel();
};

const cliAnimate = (itm: { isShow: boolean }, idx: number) => {
	itm.isShow = false;
	state.idx = idx;
	setTimeout(() => {
		itm.isShow = true;
	}, 1);
};

// 导出对象
defineExpose({ openDialog });
</script>

<style lang="scss" scoped>
.transBox {
	width: 200px;
	color: var(--el-color-primary);
	margin: 10px;
	cursor: pointer;
	padding: 10px 20px;
	border-radius: 4px;
	text-align: center;
	border: 1px var(--el-color-primary) dotted;

	// background: var(--el-color-primary);
}
</style>
