<template>
	<div class="sys-stress-test">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="40vw">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Odometer /> </el-icon>
					<span> 接口压测参数 </span>
				</div>
			</template>
			<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto" v-loading="state.loading">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="请求地址" prop="requestUri" :rules="[{ required: true, message: '请求地址不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.requestUri" placeholder="请求地址" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="请求方式" :rules="[{ required: true, message: '请求方式不能为空', trigger: 'blur' }]">
							<el-select v-model="state.ruleForm.requestMethod" placeholder="请求方式">
								<el-option :value="'GET'">GET</el-option>
								<el-option :value="'PUT'">PUT</el-option>
								<el-option :value="'POST'">POST</el-option>
								<el-option :value="'DELETE'">DELETE</el-option>
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="压测轮数" :rules="[{ required: true, message: '压测轮数不能为空', trigger: 'blur' }]">
							<el-input-number v-model="state.ruleForm.numberOfRounds" placeholder="压测轮数" :min="1" class="w100" disabled />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="每轮请求数" :rules="[{ required: true, message: '每轮请求数不能为空', trigger: 'blur' }]">
							<el-input-number v-model="state.ruleForm.numberOfRequests" :min="1" :step="10" placeholder="每轮请求数" class="w100" disabled />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="最大并发量">
							<el-input-number v-model="state.ruleForm.maxDegreeOfParallelism" :min="1" :step="5" placeholder="最大并发量" class="w100" disabled />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-tabs v-model="state.activeName" addable @tab-add="addParams()">
							<el-tab-pane label="请求头(Headers)" name="1">
								<el-row v-for="(item, index) in state.ruleForm.headers" :key="index" :gutter="25" class="w100">
									<el-col :xs="24" :sm="2" :md="2" :lg="2" :xl="2" class="mb10">
										<el-button type="danger" size="small" icon="ele-Delete" text @click="() => state.ruleForm.headers.splice(index, 1)" />
									</el-col>
									<el-col :xs="24" :sm="4" :md="4" :lg="4" :xl="4" class="mb10">
										<el-input v-model="item[0]" placeholder="参数名" clearable />
									</el-col>
									<el-col :xs="24" :sm="18" :md="18" :lg="18" :xl="18" class="mb10">
										<el-input v-model="item[1]" placeholder="参数值" clearable />
									</el-col>
								</el-row>
							</el-tab-pane>
							<el-tab-pane label="请求体(Body)" name="2">
								<el-row v-for="(item, index) in state.ruleForm.requestParameters" :key="index" :gutter="25" class="w100">
									<el-col :xs="24" :sm="2" :md="2" :lg="2" :xl="2" class="mb10">
										<el-button type="danger" size="small" icon="ele-Delete" text @click="() => state.ruleForm.requestParameters.splice(index, 1)" />
									</el-col>
									<el-col :xs="24" :sm="4" :md="4" :lg="4" :xl="4" class="mb10">
										<el-input v-model="item[0]" placeholder="参数名" clearable />
									</el-col>
									<el-col :xs="24" :sm="18" :md="18" :lg="18" :xl="18" class="mb10">
										<el-input v-model="item[1]" placeholder="参数值" clearable />
									</el-col>
								</el-row>
							</el-tab-pane>
							<el-tab-pane label="路径参数(Path)" name="3">
								<el-row v-for="(item, index) in state.ruleForm.pathParameters" :key="index" :gutter="25" class="w100">
									<el-col :xs="24" :sm="2" :md="2" :lg="2" :xl="2" class="mb10">
										<el-button type="danger" size="small" icon="ele-Delete" text @click="() => state.ruleForm.pathParameters.splice(index, 1)" />
									</el-col>
									<el-col :xs="24" :sm="4" :md="4" :lg="4" :xl="4" class="mb10">
										<el-input v-model="item[0]" placeholder="参数名" clearable />
									</el-col>
									<el-col :xs="24" :sm="18" :md="18" :lg="18" :xl="18" class="mb10">
										<el-input v-model="item[1]" placeholder="参数值" clearable />
									</el-col>
								</el-row>
							</el-tab-pane>
							<el-tab-pane label="查询参数(Query)" name="4">
								<el-row v-for="(item, index) in state.ruleForm.queryParameters" :key="index" :gutter="25" class="w100">
									<el-col :xs="24" :sm="2" :md="2" :lg="2" :xl="2" class="mb10">
										<el-button type="danger" size="small" icon="ele-Delete" text @click="() => state.ruleForm.queryParameters.splice(index, 1)" />
									</el-col>
									<el-col :xs="24" :sm="4" :md="4" :lg="4" :xl="4" class="mb10">
										<el-input v-model="item[0]" placeholder="参数名" clearable />
									</el-col>
									<el-col :xs="24" :sm="18" :md="18" :lg="18" :xl="18" class="mb10">
										<el-input v-model="item[1]" placeholder="参数值" clearable />
									</el-col>
								</el-row>
							</el-tab-pane>
						</el-tabs>
					</el-col>
				</el-row>
			</el-form>
			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="() => (state.isShowDialog = false)">取 消</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit" v-reclick="1000">确 定</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="sysStressTest">
import { reactive, ref } from 'vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysCommonApi } from '/@/api-services/system/api';

const emits = defineEmits(['refreshData']);
const ruleFormRef = ref();
const state = reactive({
	isShowDialog: false,
	activeName: '1',
	loading: false,
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
	} as any,
});

// 格式化参数
const formatParameter = (params: any[] | {}) => {
	if (Array.isArray(params)) {
		return Object.fromEntries(params.filter((e) => e.length === 2));
	} else if (typeof params === 'object' && params !== null) {
		return Object.entries(params);
	}
	return {};
};

// 打开弹窗
const openDialog = (row: any) => {
	const newRow = { ...state.ruleForm, ...row }; // 合并默认值和新值
	state.ruleForm = {
		...newRow,
		requestMethod: row.requestMethod?.toUpperCase() ?? 'GET',
	};
	state.isShowDialog = true;
	ruleFormRef.value?.resetFields();
};

// 提交
const submit = () => {
	ruleFormRef.value.validate(async (valid: boolean) => {
		if (!valid) return;
		try {
			state.loading = true;

			// 创建一个新的对象来保存格式化后的数据
			const formattedRuleForm = {
				...state.ruleForm,
				headers: formatParameter(state.ruleForm.headers),
				pathParameters: formatParameter(state.ruleForm.pathParameters),
				queryParameters: formatParameter(state.ruleForm.queryParameters),
				requestParameters: formatParameter(state.ruleForm.requestParameters),
			} as any;

			// 确保所有可能是空对象的参数被正确设置为 undefined
			['headers', 'pathParameters', 'queryParameters', 'requestParameters'].forEach((paramKey) => {
				if (Object.keys(formattedRuleForm[paramKey] || {}).length === 0) {
					formattedRuleForm[paramKey] = undefined;
				}
			});

			emits(
				'refreshData',
				await getAPI(SysCommonApi)
					.apiSysCommonStressTestPost(formattedRuleForm, { timeout: 0 })
					.then((res) => res.data.result)
			);
			state.isShowDialog = false;
		} finally {
			state.loading = false;
		}
	});
};

// 添加参数事件
const addParams = () => {
	const paramType = ['headers', 'requestParameters', 'pathParameters', 'queryParameters'][+state.activeName - 1];
	if (Array.isArray(state.ruleForm[paramType])) {
		state.ruleForm[paramType].push([null, null]);
	} else if (typeof state.ruleForm[paramType] === 'object') {
		state.ruleForm[paramType] = [[null, null]];
	}
};

// 导出对象
defineExpose({ openDialog });
</script>
