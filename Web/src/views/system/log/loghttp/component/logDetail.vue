<template>
	<el-dialog v-model="state.visible" draggable overflow destroy-on-close>
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Document /> </el-icon>
				<span> 日志详情 </span>
			</div>
		</template>
		<el-scrollbar height="calc(100vh - 250px)">
			<el-form :model="data" label-width="auto">
				<el-descriptions :column="3" :label-width="80" border>
					<el-descriptions-item label="请求地址" :width="380">
						{{ data.requestUrl?.indexOf('?') == -1 ? data.requestUrl : data.requestUrl?.substring(0, data.requestUrl.indexOf('?')) }}
					</el-descriptions-item>
					<el-descriptions-item :span="2" label="状态码">
						{{ data.statusCode }}
					</el-descriptions-item>
					<el-descriptions-item label="客户端">
						{{ data.httpClientName }}
					</el-descriptions-item>
					<el-descriptions-item :span="2" label="接口描述">
						{{ data.httpApiDesc }}
					</el-descriptions-item>
					<el-descriptions-item label="开始时间">
						{{ data.startTime }}
					</el-descriptions-item>
					<el-descriptions-item label="结束时间">
						{{ data.endTime }}
					</el-descriptions-item>
					<el-descriptions-item label="耗时"> {{ data.elapsed }} ms </el-descriptions-item>
					<el-descriptions-item label="创建人">
						{{ data.createUserName }}
					</el-descriptions-item>
					<el-descriptions-item :span="2" label="创建时间">
						{{ data.createTime }}
					</el-descriptions-item>
					<el-descriptions-item :span="3" label="请求头" class-name="row-line">
						<vue-json-pretty :data="data.requestHeaders" showLength showIcon showLineNumber showSelectController />
						<el-button @click="comFunc.copyText(JSON.stringify(data.requestHeaders))" v-if="data.requestHeaders" style="float: right" icon="ele-CopyDocument" />
					</el-descriptions-item>
					<el-descriptions-item :span="3" label="请求参数" class-name="row-line" v-if="data.requestUrl?.indexOf('?') != -1">
						<el-row v-for="(value, key, index) in queryObject">
							<span class="query-key">{{ key }}</span> = <span class="query-value">{{ value }}</span
							>&
						</el-row>
					</el-descriptions-item>
					<el-descriptions-item :span="3" label="请求体" class-name="row-line">
						<vue-json-pretty :data="data.requestBody" showLength showIcon showLineNumber showSelectController />
						<el-button @click="comFunc.copyText(JSON.stringify(data.requestBody))" v-if="data.requestBody" style="float: right" icon="ele-CopyDocument" />
					</el-descriptions-item>
					<el-descriptions-item :span="3" label="请求明文" class-name="row-line" v-if="data.requestBodyPlaintext">
						<vue-json-pretty :data="data.requestBodyPlaintext" showLength showIcon showLineNumber showSelectController />
						<el-button @click="comFunc.copyText(JSON.stringify(data.requestBodyPlaintext))" v-if="data.requestBodyPlaintext" style="float: right" icon="ele-CopyDocument" />
					</el-descriptions-item>
					<el-descriptions-item :span="3" label="响应头" class-name="row-line">
						<vue-json-pretty :data="data.responseHeaders" showLength showIcon showLineNumber showSelectController />
						<el-button @click="comFunc.copyText(JSON.stringify(data.responseHeaders))" v-if="data.responseHeaders" style="float: right" icon="ele-CopyDocument" />
					</el-descriptions-item>
					<el-descriptions-item :span="3" label="响应体" class-name="row-line">
						<vue-json-pretty :data="data.responseBody" showLength showIcon showLineNumber showSelectController />
						<el-button @click="comFunc.copyText(JSON.stringify(data.responseBody))" v-if="data.responseBody" style="float: right" icon="ele-CopyDocument" />
					</el-descriptions-item>
					<el-descriptions-item :span="3" label="响应明文" class-name="row-line" v-if="data.responseBodyPlaintext">
						<vue-json-pretty :data="data.responseBodyPlaintext" showLength showIcon showLineNumber showSelectController />
						<el-button @click="comFunc.copyText(JSON.stringify(data.responseBodyPlaintext))" v-if="data.responseBodyPlaintext" style="float: right" icon="ele-CopyDocument" />
					</el-descriptions-item>
					<el-descriptions-item :span="3" label="异常信息" class-name="row-line" v-if="data.exception">
						<vue-json-pretty :data="data.exception" showLength showIcon showLineNumber showSelectController />
						<el-button @click="comFunc.copyText(JSON.stringify(data.exception))" v-if="data.exception" style="float: right" icon="ele-CopyDocument" />
					</el-descriptions-item>
				</el-descriptions>
			</el-form>
		</el-scrollbar>
		<template #footer>
			<el-button @click="state.visible = false">关闭</el-button>
		</template>
	</el-dialog>
</template>

<!-- 远程请求日志 -->
<script lang="ts" setup>
import { ref, reactive, computed } from 'vue';
import VueJsonPretty from 'vue-json-pretty';
import { StringToObj } from '/@/utils/json-utils';
import commonFunction from '/@/utils/commonFunction';

import { SysLogHttp } from '/@/api-services/system/models';

const comFunc = commonFunction();
const state = reactive({
	visible: false,
});
const data = ref<SysLogHttp>({});
const openDialog = async (row: any) => {
	state.visible = true;
	Object.assign(data.value, row);
	data.value.requestUrl = StringToObj(row.requestUrl);
	data.value.requestHeaders = StringToObj(row.requestHeaders);
	data.value.requestBody = StringToObj(row.requestBody);
	data.value.requestBodyPlaintext = StringToObj(row.requestBodyPlaintext);
	data.value.responseHeaders = StringToObj(row.responseHeaders);
	data.value.responseBody = StringToObj(row.responseBody);
	data.value.responseBodyPlaintext = StringToObj(row.responseBodyPlaintext);
	data.value.exception = StringToObj(row.exception);
};

const queryObject = computed(() => Object.fromEntries(new URLSearchParams(new URL(data.value?.requestUrl ?? '').search)) ?? {});

defineExpose({
	openDialog,
});
</script>

<style lang="less" scoped>
.query-value {
	color: #13ce66;
}
.query-key {
	color: #d55fde;
}
:deep(td.row-line) {
	border-bottom: 1.5px solid #dadadd !important;
}
</style>
