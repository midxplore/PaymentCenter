<template>
	<div class="sys-stress-test h100 overlay-none">
		<div>
			<NoticeBar text="接口压测会占用服务器大量的系统资源（内存、带宽），请慎重操作！！！" style="margin: 4px" />
		</div>

		<splitpanes class="default-theme">
			<pane size="20" class="vh100" style="overflow: auto">
				<el-card class="vh80" shadow="hover" header="" v-loading="state.loading">
					<el-row :gutter="10">
						<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb10">
							<el-select v-model="state.swaggerUrl" placeholder="接口分组" @change="queryTreeNode()">
								<el-option v-for="(item, index) in state.apiGroupList" :key="index" :label="item.name" :value="item.url" />
							</el-select>
						</el-col>
						<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb10">
							<el-input v-model="state.keywords" placeholder="接口名称" clearable>
								<template #append>
									<el-button icon="ele-Search" v-reclick="100" @click="queryTreeNode()" />
								</template>
							</el-input>
						</el-col>
					</el-row>
					<el-tree
						ref="treeRef"
						class="filter-tree overlay-y vh68"
						style="padding-bottom: 60px"
						:data="state.apiList"
						:props="{ children: 'children', label: 'summary' }"
						:filter-node-method="filterNode"
						node-key="id"
						highlight-current
						check-strictly
					>
						<template #default="{ node }">
							<el-icon v-if="node.level == 1" size="16" style="margin-right: 3px; display: inline; vertical-align: middle"><ele-Menu /></el-icon>
							<el-icon v-else size="16" style="margin-right: 3px; display: inline; vertical-align: middle"><ele-Link /></el-icon>
							{{ node.label }}
							<span class="node-button" v-if="!node.data.children">
								<el-button type="warning" icon="ele-Position" @click="treeNodeTest(node.data)" />
							</span>
						</template>
					</el-tree>
				</el-card>
			</pane>

			<pane size="80" class="vh100">
				<el-card class="main-container vh80" shadow="hover" header="" v-loading="state.loading" body-style="height:100vh; overflow:auto">
					<template #header>
						<el-button type="primary" icon="ele-Odometer" @click="showDialog(undefined)">接口压测</el-button>
					</template>
					<el-descriptions title="⚡压测参数" label-width="180px" :column="1" class="mb20" border>
						<el-descriptions-item label="请求地址" label-align="left" align="left">
							{{ state.ruleForm.requestUri }}
						</el-descriptions-item>
						<el-descriptions-item label="请求方式" label-align="left" align="left">
							<el-tag>{{ state.ruleForm.requestMethod?.toUpperCase() }}</el-tag>
						</el-descriptions-item>
						<el-descriptions-item label="压测轮数" label-align="left" align="left">
							{{ state.ruleForm.numberOfRounds ?? 1 }}
						</el-descriptions-item>
						<el-descriptions-item label="每轮请求数" label-align="left" align="left">
							{{ state.ruleForm.numberOfRequests ?? 1 }}
						</el-descriptions-item>
						<el-descriptions-item label="最大并发量" label-align="left" align="left">
							{{ state.ruleForm.maxDegreeOfParallelism ?? 1 }}
						</el-descriptions-item>
					</el-descriptions>

					<el-descriptions title="⚡压测结果" label-width="180px" :column="4" border>
						<el-descriptions-item label="总用时（秒）" label-align="left" align="left">
							<el-tag>{{ (state.result.totalTimeInSeconds ?? 0).toFixed(2) }}</el-tag>
						</el-descriptions-item>
						<el-descriptions-item label="成功请求次数" label-align="left" align="left">
							<el-tag type="success">{{ state.result.successfulRequests ?? 0 }}</el-tag>
						</el-descriptions-item>
						<el-descriptions-item label="失败请求次数" label-align="left" align="left">
							<el-tag type="danger">{{ state.result.failedRequests ?? 0 }}</el-tag>
						</el-descriptions-item>
						<el-descriptions-item label="每秒查询率（QPS）" label-align="left" align="left">
							<el-tag>{{ (state.result.queriesPerSecond ?? 0).toFixed(2) }}</el-tag>
						</el-descriptions-item>

						<el-descriptions-item label="最小响应时间（毫秒）" label-align="left" align="left">
							{{ (state.result.minResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>
						<el-descriptions-item label="最大响应时间（毫秒）" label-align="left" align="left">
							{{ (state.result.maxResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>
						<el-descriptions-item label="平均响应时间（毫秒）" span="3" label-align="left" align="left">
							{{ (state.result.averageResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>

						<el-descriptions-item label="P10 响应时间（毫秒）" label-align="left" align="left">
							{{ (state.result.percentile10ResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>
						<el-descriptions-item label="P25 响应时间（毫秒）" label-align="left" align="left">
							{{ (state.result.percentile25ResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>
						<el-descriptions-item label="P50 响应时间（毫秒）" label-align="left" align="left">
							{{ (state.result.percentile50ResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>
						<el-descriptions-item label="P75 响应时间（毫秒）" label-align="left" align="left">
							{{ (state.result.percentile75ResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>
						<el-descriptions-item label="P90 响应时间（毫秒）" label-align="left" align="left">
							{{ (state.result.percentile90ResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>
						<el-descriptions-item label="P99 响应时间（毫秒）" label-align="left" align="left">
							{{ (state.result.percentile99ResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>
						<el-descriptions-item label="P999 响应时间（毫秒）" label-align="left" align="left">
							{{ (state.result.percentile9999ResponseTime ?? 0).toFixed(2) }}
						</el-descriptions-item>
					</el-descriptions>
				</el-card>
			</pane>
		</splitpanes>

		<EditStressTest ref="editStressTestRef" @refreshData="refreshData" />
	</div>
</template>

<!-- 性能压力测试 -->
<script lang="ts" setup name="sysStressTest">
import { onMounted, reactive, ref } from 'vue';
import { ElTree } from 'element-plus';
import { Splitpanes, Pane } from 'splitpanes';
import 'splitpanes/dist/splitpanes.css';
import 'vue-json-pretty/lib/styles.css';

import EditStressTest from './component/editStressTest.vue';
import NoticeBar from '/@/components/noticeBar/index.vue';

import request, { getToken } from '/@/utils/request';
import { StressTestHarnessResult } from '/@/api-services/system/models';

const editStressTestRef = ref();
const treeRef = ref<InstanceType<typeof ElTree>>();
const state = reactive({
	loading: false,
	activeName: '',
	ruleForm: {
		requestUri: '',
		requestMethod: 'GET',
		numberOfRounds: 1,
		numberOfRequests: 10,
		maxDegreeOfParallelism: 10,
		requestParameters: [],
		queryParameters: [],
		pathParameters: [],
		headers: [],
	},
	keywords: undefined,
	swaggerUrl: '/swagger/Default/swagger.json',
	result: {} as StressTestHarnessResult,
	apiList: [] as Array<any>,
	apiGroupList: [] as any,
});

// 页面初始化
onMounted(async () => {
	state.apiGroupList = await getApiGroupList();
	state.apiList = await getApiList('');
});

// 获取接口分组列表
const getApiGroupList = async () => {
	try {
		const response = await request('/swagger-resources', { method: 'get' });
		return response.data
			.filter((resource: { name: string; url: string }) => !resource.url.toLowerCase().includes('all%20groups'))
			.map((resource: { name: string; url: string }) => {
				const rawUrl = resource.url || '';
				let fixedUrl = rawUrl.startsWith('//') ? rawUrl.substring(1) : rawUrl;
				if (!fixedUrl.startsWith('/') && !fixedUrl.includes('://')) {
					fixedUrl = '/' + fixedUrl;
				}
				return {
					name: decodeURIComponent(resource.name || ''),
					url: fixedUrl,
				};
			});
	} catch {
		return [];
	}
};

// 获取所有接口列表
const getApiList = (keywords: string | undefined) => {
	const emojiPattern =
		/[\u{2139}\u{2B05}-\u{2B07}\u{1F600}-\u{1F64F}\u{1F300}-\u{1F5FF}\u{1F680}-\u{1F6FF}\u{1F700}-\u{1F77F}\u{1F780}-\u{1F7FF}\u{1F800}-\u{1F8FF}\u{1F900}-\u{1F9FF}\u{1FA00}-\u{1FA6F}\u{1FA70}-\u{1FAFF}\u{2600}-\u{26FF}\u{2700}-\u{27BF}]/gu;
	return request(state.swaggerUrl, { method: 'get' }).then(({ data }) => {
		const pathMap = data.paths;
		const result = data.tags.map((e: any) => ({ path: e.name, summary: e.description?.replaceAll(emojiPattern, '') || e.name, children: [] }));
		Object.keys(pathMap).map((path) => {
			const method = Object.keys(pathMap[path])[0];
			const apiInfo = pathMap[path][method];
			if (keywords && apiInfo.summary?.indexOf(keywords) === -1) return;
			result
				.find((u: any) => u.path === apiInfo.tags[0])
				.children.push({
					path: path,
					method: method,
					summary: apiInfo.summary?.replaceAll(emojiPattern, '') ?? path,
					parameters: apiInfo.parameters,
					requestBody: apiInfo.requestBody,
					data: apiInfo,
				});
		});
		return result.filter((u: any) => u.children.length > 0);
	});
};

// 刷新数据
const refreshData = (data: StressTestHarnessResult) => {
	state.result = data;
};

// 打开接口参数设置页面
const showDialog = async (row: any) => {
	const newRow = row ?? { ...state.ruleForm };
	const convertToKeyValuePairs = (params: any) => {
		if (Array.isArray(params) && params.every((item) => Array.isArray(item) && item.length === 2)) {
			return params;
		} else if (typeof params === 'object' && params !== null) {
			return Object.entries(params);
		}
		return [];
	};

	state.ruleForm = {
		...newRow,
		requestParameters: convertToKeyValuePairs(newRow.requestParameters),
		queryParameters: convertToKeyValuePairs(newRow.queryParameters),
		pathParameters: convertToKeyValuePairs(newRow.pathParameters),
		headers: convertToKeyValuePairs(newRow.headers),
	};
	editStressTestRef.value.openDialog(state.ruleForm);
};

// 接口树节点按钮事件
const treeNodeTest = async (node: any) => {
	if (node.id == 0) return;
	state.ruleForm = {
		requestUri: location.origin + node.path,
		requestMethod: node.method,
		numberOfRounds: 1,
		numberOfRequests: 10,
		maxDegreeOfParallelism: 10,
		requestParameters: [],
		queryParameters: [],
		pathParameters: [],
		headers: [['Authorization', 'Bearer ' + getToken()]] as any,
	};
	showDialog(state.ruleForm);
};

// 查询树节点
const queryTreeNode = async () => {
	state.apiList = await getApiList(state.keywords);
};

const filterNode = (value: string, data: any) => {
	if (!value) return true;
	return data.name.includes(value);
};
</script>

<style lang="scss" scoped>
.card-header {
	width: 100%;
	display: flex;
	justify-content: space-between;
	align-items: center;
}
:deep(.el-collapse-item) {
	.el-collapse-item__arrow {
		float: right;
	}
}
:deep(.main-container) {
	.el-card__header {
		padding: 8px;
	}
}
.node-button {
	position: absolute;
	scale: 0.7;
	right: 0;
}
</style>
