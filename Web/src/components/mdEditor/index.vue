<template>
	<div class="editor-container">
		<MdEditor v-model="state.editorVal" @onUploadImg="onUploadImg" />
	</div>
</template>

<script setup lang="ts" name="wngEditor">
import { reactive, watch } from 'vue';
import { MdEditor } from 'md-editor-v3';
import { ElMessage } from 'element-plus';
import { getAPI } from '/@/utils/axios-utils';
import { SysFileApi } from '/@/api-services/system/api';

// 定义父组件传过来的值
const props = defineProps({
	// 双向绑定，用于获取 editor.getHtml()
	getHtml: String,
});

const state = reactive({
	editorVal: '',
});

// 接口服务地址
const baseUrl = reactive(window.__env__.VITE_API_URL) + '/';

// 监听双向绑定值改变，用于回显
watch(
	() => props.getHtml,
	(val: any) => {
		state.editorVal = val;
	},
	{
		immediate: true,
	}
);
const onUploadImg = async (files: any[], callback: (arg0: any[]) => void) => {
	const res = await Promise.all(
		files.map((file: any) => {
			return new Promise((rev) => {
				getAPI(SysFileApi)
					.apiSysFileUploadFilesPostForm([file])
					.then((res) => {
						if (res.data.type == 'success' && res.data.result != null) {
							console.log(baseUrl + res.data.result[0].url);
							rev(baseUrl + res.data.result[0].url);
						} else {
							ElMessage.error('上传失败！');
						}
					})
					.catch(() => {
						ElMessage.error('上传失败！');
					});
			});
		})
	);

	callback(res.map((item) => item));
};
</script>

<style lang="scss" scoped>
.editor-container {
	::v-deep(.md-editor-icon) {
		width: 22px !important;
		height: 22px !important;
	}
	::v-deep(.md-editor-toolbar-wrapper .md-editor-toolbar-item) {
		padding: 0;
		margin: 0 1px;
	}
}
</style>
