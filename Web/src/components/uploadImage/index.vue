<template>
	<div>
		<el-row style="display: flex; justify-content: center">
			<el-form-item>
				<div>
					<el-image v-if="model" :src="defaultImage ?? model" class="avatar" :preview-src-list="defaultImage ? [] : [model]" :zoom-rate="1.2" :max-scale="7" :min-scale="0.2" />
					<el-icon v-else class="avatar-uploader-icon"><Component :is="icon" /></el-icon>
				</div>
			</el-form-item>
		</el-row>
		<el-row style="display: flex; justify-content: space-evenly; margin-top: 20px">
			<el-upload v-if="!model" ref="uploadRef" :on-change="handleFileChange" :show-file-list="false" :auto-upload="false" :accept="accept" :disabled="disabled">
				<template #trigger>
					<el-button type="primary">上 传</el-button>
				</template>
			</el-upload>
			<el-button v-else type="primary" @click="handleFileDelete" :disabled="disabled">删 除</el-button>
		</el-row>
	</div>
</template>

<script setup lang="ts">
import { ElMessage, UploadFile, UploadFiles } from 'element-plus';

const model = defineModel();
const props = defineProps({
	defaultImage: {
		type: String,
		default: null,
	},
	disabled: {
		type: Boolean,
		default: false,
	},
	accept: {
		type: String,
		default: '.jpg,.png,.jpeg,.bmp',
	},
	icon: {
		type: String,
		default: 'ele-Picture',
	},
});

const handleFileDelete = () => {
	model.value = null;
};

const handleFileChange = (file: UploadFile, fileList: UploadFiles) => {
	if (fileList.length > 1) fileList.shift();
	if (fileList[0].raw) {
		convertToBase64(fileList[0].raw);
	} else {
		ElMessage.error('图片上传失败');
	}
};

const convertToBase64 = (file: File) => {
	if (!file) return;
	const reader = new FileReader();
	reader.readAsDataURL(file);
	reader.onload = () => {
		model.value = reader.result as string;
	};
	reader.onerror = (error) => {
		ElMessage.error('图片上传失败');
	};
};
</script>

<style scoped lang="scss">
.avatar-uploader-icon {
	font-size: 28px;
	background-color: #eff0f1;
	width: 130px;
	height: 130px;
	text-align: center;
}
.avatar {
	width: 130px;
	height: 130px;
	display: block;
}
</style>
