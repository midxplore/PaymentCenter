<template>
	<div class="editor-container">
		<Toolbar :editor="editorRef" :mode="mode" />
		<Editor :mode="mode" :defaultConfig="state.editorConfig" :style="{ height }" v-model="state.editorVal" @onCreated="handleCreated" @onChange="handleChange" />
	</div>
</template>

<script setup lang="ts" name="wngEditor">
import { reactive, shallowRef, watch, onBeforeUnmount } from 'vue';
import { ElMessage } from 'element-plus';

import { IDomEditor } from '@wangeditor/editor';
import { Toolbar, Editor } from '@wangeditor/editor-for-vue';
// https://www.wangeditor.com/v5/for-frame.html#vue3
import '@wangeditor/editor/dist/css/style.css';

import { getAPI } from '/@/utils/axios-utils';
import { SysFileApi } from '/@/api-services/system/api';

// 定义父组件传过来的值
const props = defineProps({
	// 是否禁用
	disable: {
		type: Boolean,
		default: () => false,
	},
	// 内容框默认 placeholder
	placeholder: {
		type: String,
		default: () => '请输入内容...',
	},
	// https://www.wangeditor.com/v5/getting-started.html#mode-%E6%A8%A1%E5%BC%8F
	// 模式，可选 <default|simple>，默认 default
	mode: {
		type: String,
		default: () => 'default',
	},
	// 高度
	height: {
		type: String,
		default: () => '310px',
	},
	// 双向绑定，用于获取 editor.getHtml()
	getHtml: String,
	// 双向绑定，用于获取 editor.getText()
	getText: String,
	// 文件以base64格式内嵌到内容中
	useFileToBase64: {
		type: Boolean,
		default: () => false,
	},
	// 禁用图片懒加载
	lazyLoadImage: {
		type: Boolean,
		default: () => true,
	},
	// 限制使用base64格式上传的文件最大值，单位KB
	useFileToBase64WithMaxSize: {
		type: Number,
		default: () => 1024 * 5, // 5MB
	},
});

// 定义子组件向父组件传值/事件
const emit = defineEmits(['update:getHtml', 'update:getText']);

// 定义变量内容
const editorRef = shallowRef();
const state = reactive({
	editorConfig: {
		placeholder: props.placeholder,
		lazyLoadImage: props.lazyLoadImage,
		// 菜单配置
		MENU_CONF: {
			uploadImage: {
				fieldName: 'file',
				customUpload(file: File) {
					if (props.useFileToBase64) {
						if (file.size > props.useFileToBase64WithMaxSize * 1024) {
							ElMessage.error(`上传图片大小不能超过 ${props.useFileToBase64WithMaxSize} KB！`);
							return;
						}

						const reader = new FileReader();
						reader.readAsDataURL(file);
						reader.onload = () => {
							const base64 = reader.result as string;
							editorRef.value.insertNode({ type: 'image', src: base64, alt: file.name, href: base64, children: [{ text: '' }] });
						};
						reader.onerror = () => {
							ElMessage.error('上传失败！');
						};
						return;
					}
					getAPI(SysFileApi)
						.apiSysFileUploadFilesPostForm([file])
						.then(({ data }) => {
							if (data.type == 'success' && data.result && data.result[0]) {
								let url = data.result[0].url?.indexOf('http') !== 0 ? '/' + data.result[0].url : data.result[0].url;
								editorRef.value.insertNode({ type: 'image', src: url, alt: data.result[0].fileName, href: url, children: [{ text: '' }] });
							} else {
								ElMessage.error('上传失败！');
							}
						});
				},
			},
			insertImage: {
				checkImage(src: string): boolean | string | undefined {
					if (src.indexOf('http') !== 0) {
						return '图片网址必须以 http/https 开头';
					}
					return true;
				},
			},
		},
	},
	editorVal: props.getHtml,
});

// 编辑器回调函数
const handleCreated = (editor: IDomEditor) => {
	editorRef.value = editor;
};

// 编辑器内容改变时
const handleChange = (editor: IDomEditor) => {
	emit('update:getHtml', editor.getHtml());
	emit('update:getText', editor.getText());
};

// 页面销毁时
onBeforeUnmount(() => {
	const editor = editorRef.value;
	if (editor == null) return;
	editor.destroy();
});

// 监听是否禁用改变
// https://gitee.com/lyt-top/vue-next-admin/issues/I4LM7I
watch(
	() => props.disable,
	(bool) => {
		const editor = editorRef.value;
		if (editor == null) return;
		bool ? editor.disable() : editor.enable();
	},
	{
		deep: true,
	}
);

// 监听双向绑定值改变，用于回显
watch(
	() => props.getHtml,
	(val) => {
		state.editorVal = val;
	},
	{
		deep: true,
	}
);
</script>

<style lang="less">
.editor-container {
	overflow-y: hidden;
	.w-e-bar-item {
		.w-e-select-list {
			height: 150px;
			z-index: 10 !important;
		}
	}
	.w-e-toolbar {
		// 给工具栏换行
		flex-wrap: wrap;
		z-index: 4 !important;
	}
	.w-e-menu {
		// 最重要的一句代码
		z-index: auto !important;
		.w-e-droplist {
			// 触发工具栏后的显示框调高
			z-index: 2 !important;
		}
	}
}
</style>
