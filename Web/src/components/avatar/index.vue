<template>
	<div>
		<el-row style="display: flex; justify-content: center">
			<el-form-item>
				<div>
					<el-image v-if="state.imageUrl" :src="state.imageUrl" class="avatar" :preview-src-list="[state.imageUrl]" :zoom-rate="1.2" :max-scale="7" :min-scale="0.2" />
					<el-image v-else-if="model" :src="model" class="avatar" :preview-src-list="[model]" :zoom-rate="1.2" :max-scale="7" :min-scale="0.2" />
					<el-icon
						v-else
						class="avatar-uploader-icon"
						@click="
							() => {
								if (!disabled) cameraDialogRef?.openDialog();
							}
						"
					>
						<Camera />
					</el-icon>
				</div>
			</el-form-item>
		</el-row>
		<el-row style="display: flex; justify-content: space-evenly; margin-top: 20px">
			<el-button type="primary" @click="() => cameraDialogRef?.openDialog()" :disabled="disabled">拍 摄</el-button>
			<el-button v-if="model || state.imageUrl" type="warning" @click="deletePhoto" :disabled="disabled">删 除</el-button>
			<el-upload v-else ref="uploadRef" :on-change="handleFileChange" :show-file-list="false" :auto-upload="false" accept=".jpg,.png,.jpeg,.bmp" :disabled="disabled">
				<template #trigger>
					<el-button type="primary" :disabled="disabled">上 传</el-button>
				</template>
			</el-upload>
		</el-row>
		<CameraDialog ref="cameraDialogRef" title="照片" :imageWidth="178" :imageHeight="178" @confirm="(file) => ((model = file), (state.imageUrl = null))" />
	</div>
</template>
<script setup lang="ts">
import { reactive, ref, watch } from 'vue';
import CameraDialog from '/@/components/cameraDialog/cameraDialog.vue';
import { ElMessage, UploadFile, UploadFiles } from 'element-plus';
import { Camera } from '@element-plus/icons-vue';
import { getAPI } from '/@/utils/axios-utils';
import { SysFileApi } from '/@/api-services/system';

const cameraDialogRef = ref<InstanceType<typeof CameraDialog> | null>(null);
const uploadRef = ref();
const model = defineModel();
const props = defineProps({
	disabled: {
		type: Boolean,
		default: false,
	},
	imageId: {
		type: [Number, String, null],
	},
});
const state = reactive({
	imageUrl: null as string | null,
});

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
	console.log(file);
	const reader = new FileReader();
	reader.readAsDataURL(file);
	reader.onload = () => {
		state.imageUrl = null;
		model.value = reader.result as string;
	};
	reader.onerror = (error) => {
		ElMessage.error('图片上传失败');
	};
};
const deletePhoto = () => {
	state.imageUrl = null;
	model.value = null;
};
watch(
	() => props.imageId,
	async (newVal) => {
		if (newVal) {
			const res = await getAPI(SysFileApi).apiSysFileFileGet(newVal as number);
			if (res.data.code === 200) {
				state.imageUrl = res.data.result?.url || null;
			} else {
				ElMessage.error(res.data.message || '图片加载失败');
			}
		} else {
			state.imageUrl = null;
		}
	}
);
</script>
<style lang="css" scoped>
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
