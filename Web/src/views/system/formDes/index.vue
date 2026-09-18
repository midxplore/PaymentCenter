<template>
	<div class="sys-formDes-container">
		<v-form-designer class="form-designer" ref="vFormDesignRef"></v-form-designer>
	</div>
</template>

<!-- 表单设计 -->
<script lang="ts" setup name="sysFormDes">
import { ref } from 'vue';

const vFormDesignRef = ref(null);
</script>

<style lang="scss" scoped>
body {
	margin: 0; // 去除页面垂直滚动条
}

.form-designer {
	overflow: unset !important;
	background-color: var(--el-color-white) !important;
}
// ::v-deep(.form-widget-container) {
// 	background-color: var(--el-color-white) !important;
// }
// ::v-deep(.field-widget-item) {
// 	background-color: var(--el-color-white) !important;
// }
// ::v-deep(.container-widget-item) {
// 	background-color: var(--el-color-white) !important;
// }
</style>
