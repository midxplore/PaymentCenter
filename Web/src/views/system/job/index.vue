<template>
	<div class="sys-job-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="作业编号" prop="jobId">
							<el-input v-model="state.queryParams.jobId" placeholder="作业编号" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="组名称">
							<el-select v-model="state.queryParams.groupName" placeholder="组名称" clearable>
								<el-option v-for="item in state.groupsData" :key="item" :label="item" :value="item" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="描述信息" prop="description">
							<el-input v-model="state.queryParams.description" placeholder="描述信息" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />
			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysJob/pageJobDetail'" :loading="optionsJob.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="optionsJob.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGridJob" class="xGrid-style" v-bind="optionsJob" v-on="gridEventsJob">
				<template #toolbar_buttons>
					<el-button-group>
						<el-tooltip content="增加作业">
							<el-button icon="ele-CirclePlus" @click="handleAdd" v-auth="'sysJob/addJobDetail'"> </el-button>
						</el-tooltip>
						<el-tooltip content="启动所有作业">
							<el-button icon="ele-VideoPlay" @click="startAllJob" />
						</el-tooltip>
						<el-tooltip content="暂停所有作业">
							<el-button icon="ele-VideoPause" @click="pauseAllJob" />
						</el-tooltip>
					</el-button-group>
					<el-button-group style="margin: 0 12px">
						<el-tooltip content="强制唤醒作业调度器">
							<el-button icon="ele-AlarmClock" @click="cancelSleep" />
						</el-tooltip>
						<el-tooltip content="强制触发所有作业持久化">
							<el-button icon="ele-Connection" @click="persistAll" />
						</el-tooltip>
					</el-button-group>
					<el-button icon="ele-Coin" @click="openJobCluster" plain> 集群控制 </el-button>
					<el-button type="primary" icon="ele-Grid" @click="openJobDashboard" plain> 任务看板 </el-button>
					<el-button-group style="padding-left: 12px">
						<el-button icon="ele-Expand" @click="handleExpand"> 全部展开 </el-button>
						<el-button icon="ele-Fold" @click="handleFold"> 全部折叠 </el-button>
					</el-button-group>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_content="{ row }">
					<el-row :gutter="10" class="w100">
						<el-col :xs="24" :sm="12" :md="12" :lg="8" :xl="6" class="mb10 mt10" v-for="(item, idx) in row.jobTriggers" :key="idx">
							<el-card shadow="hover" :body-style="{ padding: '10px', color: 'gray' }">
								<div class="font15" style="text-align: center; font-weight: bold; color: black">触发器</div>
								<el-divider border-style="dashed" style="margin: 10px 0" />

								<div class="mt10">
									<span class="jobTriggers"> 编号 </span>：
									<span class="jobTriggersValue">{{ item.triggerId }}</span>
								</div>
								<div class="mt10">
									<span class="jobTriggers"> 类型 </span>：
									<span class="jobTriggersValue">{{ item.triggerType }}</span>
								</div>
								<div class="mt10">
									<span class="jobTriggers"> 描述 </span>：
									<span class="jobTriggersValue">{{ item.description }}</span>
								</div>
								<div class="mt10">
									<span class="jobTriggers"> 状态 </span>：
									<el-tag type="warning" effect="plain" v-if="item.status == 0"> 积压 </el-tag>
									<el-tag type="info" effect="plain" v-if="item.status == 1"> 就绪 </el-tag>
									<el-tag type="success" effect="plain" v-if="item.status == 2"> 正在运行 </el-tag>
									<el-tag type="danger" effect="plain" v-if="item.status == 3"> 暂停 </el-tag>
									<el-tag type="danger" effect="plain" v-if="item.status == 4"> 阻塞 </el-tag>
									<el-tag type="info" effect="plain" v-if="item.status == 5"> 由失败进入就绪 </el-tag>
									<el-tag type="danger" effect="plain" v-if="item.status == 6"> 归档 </el-tag>
									<el-tag type="danger" effect="plain" v-if="item.status == 7"> 崩溃 </el-tag>
									<el-tag type="danger" effect="plain" v-if="item.status == 8"> 超限 </el-tag>
									<el-tag type="danger" effect="plain" v-if="item.status == 9"> 无触发时间 </el-tag>
									<el-tag type="danger" effect="plain" v-if="item.status == 10"> 未启动 </el-tag>
									<el-tag type="danger" effect="plain" v-if="item.status == 11"> 未知作业触发器 </el-tag>
									<el-tag type="danger" effect="plain" v-if="item.status == 12"> 未知作业处理程序 </el-tag>
								</div>
								<div class="flex" style="justify-content: space-between">
									<div class="mt10">
										<span class="jobTriggers"> 触发次数 </span>：
										<span class="jobTriggersValue">{{ item.numberOfRuns }}</span>
									</div>
									<div class="mt10">
										<span class="jobTriggers" style="width: 6rem"> 最大触发次数 </span>：
										<span class="jobTriggersValue">{{ item.maxNumberOfRuns ?? '0' }}</span>
									</div>
								</div>
								<div class="flex" style="justify-content: space-between">
									<div class="mt10">
										<span class="jobTriggers"> 出错次数 </span>：
										<span class="jobTriggersValue">{{ item.numberOfErrors ?? 0 }}</span>
									</div>
									<div class="mt10">
										<span class="jobTriggers" style="width: 6rem"> 最大出错次数 </span>：
										<span class="jobTriggersValue">{{ item.maxnumberOfErrors ?? '0' }}</span>
									</div>
								</div>
								<div class="flex" style="justify-content: space-between">
									<div class="mt10">
										<span class="jobTriggers"> 重试次数 </span>：
										<span class="jobTriggersValue">{{ item.numRetries ?? 0 }}</span>
									</div>
									<div class="mt10">
										<span class="jobTriggers" style="width: 6rem"> 重试间隔ms </span>：
										<span class="jobTriggersValue">{{ item.retryTimeout ?? '0' }}</span>
									</div>
								</div>
								<div class="flex" style="justify-content: space-between">
									<div class="mt10">
										<span class="jobTriggers"> 起始时间 </span>：
										<span class="jobTriggersValue"> {{ item.startTime == null ? '无' : formatDate(new Date(item.startTime), 'YYYY-mm-dd') }} </span>
									</div>
									<div class="mt10">
										<span class="jobTriggers" style="width: 6rem"> 结束时间 </span>：
										<span class="jobTriggersValue">{{ item.endTime == null ? '无' : formatDate(new Date(item.endTime), 'YYYY-mm-dd') }}</span>
									</div>
								</div>
								<div class="flex" style="justify-content: space-between">
									<div class="mt10">
										<span class="jobTriggers"> 运行时间 </span>：
										<span class="jobTriggersValue"> {{ item.lastRunTime == null ? '无' : formatDate(new Date(item.lastRunTime), 'YYYY-mm-dd') }} </span>
									</div>
									<div class="mt10">
										<span class="jobTriggers" style="width: 6rem"> 下次运行时间 </span>：
										<span class="jobTriggersValue">{{ item.nextRunTime == null ? '无' : formatDate(new Date(item.nextRunTime), 'YYYY-mm-dd') }}</span>
									</div>
								</div>
								<div class="mt10">
									<span class="jobTriggers" style="width: 8rem"> 是否立即启动 </span>：
									<el-tag v-if="item.startNow == true"> 是 </el-tag>
									<el-tag type="info" v-else> 否 </el-tag>
								</div>
								<div class="mt10">
									<span class="jobTriggers" style="width: 8rem"> 是否启动时执行一次 </span>：
									<el-tag v-if="item.runOnStart == true"> 是 </el-tag>
									<el-tag type="info" v-else> 否 </el-tag>
								</div>
								<div class="mt10">
									<span class="jobTriggers" style="width: 8rem"> 是否重置触发次数 </span>：
									<el-tag v-if="item.resetOnlyOnce == true"> 是 </el-tag>
									<el-tag type="info" v-else> 否 </el-tag>
								</div>

								<el-divider border-style="dashed" style="margin: 10px 0" />
								<div class="flex" style="justify-content: flex-end">
									<el-tooltip content="启动触发器">
										<el-button type="primary" icon="ele-VideoPlay" @click="startTrigger(item)"> 启动 </el-button>
									</el-tooltip>
									<el-tooltip content="暂停触发器">
										<el-button type="primary" icon="ele-VideoPause" @click="pauseTrigger(item)"> 暂停 </el-button>
									</el-tooltip>
									<el-tooltip content="编辑触发器">
										<el-button type="primary" icon="ele-Edit" @click="openEditJobTrigger(item)"> 编辑 </el-button>
									</el-tooltip>
									<el-tooltip content="删除触发器">
										<el-button type="danger" icon="ele-Delete" @click="delJobTrigger(item)"> 删除 </el-button>
									</el-tooltip>
								</div>
							</el-card>
						</el-col>
					</el-row>
				</template>
				<template #row_jobId="{ row }">
					<div style="display: flex; align-items: center">
						<el-icon><timer /></el-icon>
						<span style="margin-left: 5px">{{ (row as JobDetailOutput).jobDetail?.jobId }}</span>
					</div>
				</template>
				<template #row_concurrent="{ row }">
					<el-tag type="success" v-if="(row as JobDetailOutput).jobDetail?.concurrent == true"> 并行 </el-tag>
					<el-tag type="warning" v-else> 串行 </el-tag>
				</template>
				<template #row_createType="{ row }">
					<el-tag type="info" v-if="(row as JobDetailOutput).jobDetail?.createType == JobCreateTypeEnum.NUMBER_0"> 内置 </el-tag>
					<el-tag type="warning" v-if="(row as JobDetailOutput).jobDetail?.createType == JobCreateTypeEnum.NUMBER_1"> 脚本 </el-tag>
					<el-tag type="success" v-if="(row as JobDetailOutput).jobDetail?.createType == JobCreateTypeEnum.NUMBER_2"> HTTP请求 </el-tag>
				</template>
				<template #row_includeAnnotation="{ row }">
					<el-tag v-if="(row as JobDetailOutput).jobDetail?.includeAnnotation == true"> 是 </el-tag>
					<el-tag v-else> 否 </el-tag>
				</template>
				<template #row_properties="{ row }">
					<span v-if="(row as JobDetailOutput).jobDetail?.createType != JobCreateTypeEnum.NUMBER_2"> {{ (row as JobDetailOutput).jobDetail?.properties }} </span>
					<div v-else style="text-align: center">
						<el-popover placement="left" :width="400" trigger="hover">
							<template #reference>
								<el-tag effect="plain" type="info"> 请求参数 </el-tag>
							</template>
							<el-descriptions title="Http 请求参数" :column="1" size="small" :border="true">
								<el-descriptions-item label="请求地址" label-align="right" label-class-name="job-index-descriptions-label-style">
									{{ getHttpJobMessage((row as JobDetailOutput).jobDetail?.properties).requestUri }}
								</el-descriptions-item>
								<el-descriptions-item label="请求方法" label-align="right" label-class-name="job-index-descriptions-label-style">
									{{ getHttpMethodDesc(getHttpJobMessage((row as JobDetailOutput).jobDetail?.properties).httpMethod) }}
								</el-descriptions-item>
								<el-descriptions-item label="请求报文体" label-align="right" label-class-name="job-index-descriptions-label-style">
									{{ getHttpJobMessage((row as JobDetailOutput).jobDetail?.properties).body }}
								</el-descriptions-item>
								<el-descriptions-item label="超时时间" label-align="right" label-class-name="job-index-descriptions-label-style">
									{{ getHttpJobMessage((row as JobDetailOutput).jobDetail?.properties).timeout }}
								</el-descriptions-item>
							</el-descriptions>
						</el-popover>
					</div>
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip content="执行记录">
						<el-button size="small" type="primary" icon="ele-Timer" text @click="openJobTriggerRecord(row)"> </el-button>
					</el-tooltip>
					<el-tooltip content="增加触发器">
						<el-button size="small" type="primary" icon="ele-CirclePlus" text @click="openAddJobTrigger(row)"> </el-button>
					</el-tooltip>
					<el-tooltip content="执行作业">
						<el-button size="small" type="primary" icon="ele-CircleCheck" text @click="runJob(row)" />
					</el-tooltip>
					<el-tooltip content="启动作业">
						<el-button size="small" type="primary" icon="ele-VideoPlay" text @click="startJob(row)" />
					</el-tooltip>
					<el-tooltip content="暂停作业">
						<el-button size="small" type="primary" icon="ele-VideoPause" text @click="pauseJob(row)" />
					</el-tooltip>
					<el-tooltip content="取消作业">
						<el-button size="small" type="primary" icon="ele-CircleClose" text @click="cancelJob(row)" />
					</el-tooltip>
					<el-tooltip content="编辑作业">
						<el-button size="small" type="primary" icon="ele-Edit" text @click="handleEdit(row)" v-auth="'sysJob/updateJobDetail'"> </el-button>
					</el-tooltip>
					<el-tooltip content="删除作业">
						<el-button size="small" type="danger" icon="ele-Delete" text @click="handleDelete(row)" v-auth="'sysJob/deleteJobDetail'"> </el-button>
					</el-tooltip>
				</template>
			</vxe-grid>
		</el-card>

		<el-drawer v-model="state.isVisibleDrawer" size="60%">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Clock /> </el-icon>
					<span> 作业触发器运行记录</span>
				</div>
			</template>
			<el-card class="full-table" shadow="hover">
				<vxe-grid ref="xGridRecord" class="xGrid-style" v-bind="optionsRecord" v-on="gridEventsRecord">
					<template #toolbar_buttons>
						<el-button icon="ele-DeleteFilled" type="danger" @click="handleClearJobTriggerRecord"> 清空 </el-button>
					</template>
					<template #toolbar_tools> </template>
					<template #empty>
						<el-empty :image-size="200" />
					</template>
					<template #row_status="{ row }">
						<el-tag type="warning" effect="plain" v-if="(row as SysJobTrigger).status == 0"> 积压 </el-tag>
						<el-tag type="info" effect="plain" v-if="(row as SysJobTrigger).status == 1"> 就绪 </el-tag>
						<el-tag type="success" effect="plain" v-if="(row as SysJobTrigger).status == 2"> 正在运行 </el-tag>
						<el-tag type="danger" effect="plain" v-if="(row as SysJobTrigger).status == 3"> 暂停 </el-tag>
						<el-tag type="danger" effect="plain" v-if="(row as SysJobTrigger).status == 4"> 阻塞 </el-tag>
						<el-tag type="info" effect="plain" v-if="(row as SysJobTrigger).status == 5"> 由失败进入就绪 </el-tag>
						<el-tag type="danger" effect="plain" v-if="(row as SysJobTrigger).status == 6"> 归档 </el-tag>
						<el-tag type="danger" effect="plain" v-if="(row as SysJobTrigger).status == 7"> 崩溃 </el-tag>
						<el-tag type="danger" effect="plain" v-if="(row as SysJobTrigger).status == 8"> 超限 </el-tag>
						<el-tag type="danger" effect="plain" v-if="(row as SysJobTrigger).status == 9"> 无触发时间 </el-tag>
						<el-tag type="danger" effect="plain" v-if="(row as SysJobTrigger).status == 10"> 未启动 </el-tag>
						<el-tag type="danger" effect="plain" v-if="(row as SysJobTrigger).status == 11"> 未知作业触发器 </el-tag>
						<el-tag type="danger" effect="plain" v-if="(row as SysJobTrigger).status == 12"> 未知作业处理程序 </el-tag>
					</template>
				</vxe-grid>
			</el-card>
		</el-drawer>

		<EditJobDetail ref="editJobDetailRef" :title="state.editJobDetailTitle" @handleQuery="handleQuery" />
		<EditJobTrigger ref="editJobTriggerRef" :title="state.editJobTriggerTitle" @handleQuery="handleQuery" />
		<JobCluster ref="editJobClusterRef" />
	</div>
</template>

<!-- 定时任务 -->
<script lang="ts" setup name="sysJob">
import { onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { useRouter } from 'vue-router';
import { Timer } from '@element-plus/icons-vue';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { auth } from '/@/utils/authFunction';

import EditJobDetail from '/@/views/system/job/component/editJobDetail.vue';
import EditJobTrigger from '/@/views/system/job/component/editJobTrigger.vue';
import JobCluster from '/@/views/system/job/component/jobCluster.vue';

import { getAPI } from '/@/utils/axios-utils';
import { formatDate } from '/@/utils/formatTime';

import { SysJobApi } from '/@/api-services/system/api';
import { JobCreateTypeEnum, JobDetailOutput, SysJobTrigger, SysJobTriggerRecord, PageJobDetailInput, PageJobTriggerRecordInput } from '/@/api-services/system/models';

const xGridJob = ref<VxeGridInstance>();
const xGridTrigger = ref<VxeGridInstance>();
const xGridRecord = ref<VxeGridInstance>();
const router = useRouter();
const editJobDetailRef = ref<InstanceType<typeof EditJobDetail>>();
const editJobTriggerRef = ref<InstanceType<typeof EditJobTrigger>>();
const editJobClusterRef = ref<InstanceType<typeof JobCluster>>();
const state = reactive({
	queryParams: {
		jobId: undefined,
		groupName: undefined,
		description: undefined,
	},
	jobPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'id', order: 'asc', descStr: 'desc' } as any,
	},
	recordPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'id', order: 'desc', descStr: 'desc' } as any,
		// ★ 这里必须显式声明 jobId：openJobTriggerRecord() 会写 state.recordPageParam.jobId，
		//   而它同时又是「查触发器运行记录」的查询参数。原先没声明，靠运行时动态加属性才「碰巧能用」
		//   —— 一旦有人把查询参数改成显式构造（或换掉响应式实现），jobId 就会被静默丢掉，
		//   表现为「打开任意作业的运行记录都查到全部记录」。
		jobId: undefined as number | undefined,
	},
	editJobDetailTitle: '',
	editJobTriggerTitle: '',
	loading2: false,
	isVisibleDrawer: false,
	triggerRecordData: [] as any,
	currentJob: {} as any,
	visible: false,
	title: '',
	groupsData: [] as Array<string>,
});

// 表格参数配置
const optionsJob = useVxeTable<JobDetailOutput>(
	{
		id: 'sysJob',
		name: '作业信息',
		columns: [
			// { type: 'checkbox', width: 40 },
			{ field: 'seq', type: 'seq', title: '序号', width: 50, fixed: 'left' },
			{ field: 'expand', type: 'expand', width: 40, slots: { content: 'row_content' } },
			{ field: 'jobDetail.jobId', title: '作业编号', minWidth: 180, showOverflow: 'tooltip', slots: { default: 'row_jobId' } },
			{ field: 'jobDetail.groupName', title: '组名称', minWidth: 80, showOverflow: 'tooltip' },
			{ field: 'jobDetail.jobType', title: '类型', minWidth: 180, showOverflow: 'tooltip' },
			// { field: 'jobDetail.assemblyName', title: '程序集', minWidth: 100, showOverflow: 'tooltip', sortable: true },
			{ field: 'jobDetail.description', title: '描述', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'jobDetail.concurrent', title: '执行方式', minWidth: 80, showOverflow: 'tooltip', slots: { default: 'row_concurrent' } },
			{ field: 'jobDetail.createType', title: '作业创建类型', minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_createType' } },
			{ field: 'jobDetail.includeAnnotation', title: '扫描特性触发器', minWidth: 120, showOverflow: 'tooltip', slots: { default: 'row_includeAnnotation' } },
			{ field: 'jobDetail.updatedTime', title: '更新时间', minWidth: 130, showOverflow: 'tooltip' },
			{ field: 'jobDetail.properties', title: '额外数据', minWidth: 140, showOverflow: 'tooltip', slots: { default: 'row_properties' } },
			{ field: 'buttons', title: '操作', minWidth: 250, showOverflow: true, slots: { default: 'row_buttons' }, fixed: 'right' },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: ({ page, sort }) => handleQueryApi(page, sort) } },
		// 排序配置
		sortConfig: { defaultSort: state.jobPageParam.defaultSort },
		// 分页配置
		pagerConfig: { pageSize: state.jobPageParam.pageSize },
		// 工具栏配置
		toolbarConfig: { export: true },
	}
);

// 页面初始化
onMounted(async () => {
	const { data } = await getAPI(SysJobApi).apiSysJobListJobGroupPost();
	state.groupsData = data.result ?? [];
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageJobDetailInput;
	return getAPI(SysJobApi).apiSysJobPageJobDetailPost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	reset ? await xGridJob.value?.commitProxy('reload') : await xGridJob.value?.commitProxy('query');
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.jobId = undefined;
	state.queryParams.groupName = undefined;
	state.queryParams.description = undefined;
	await xGridJob.value?.commitProxy('reload');
};

// 打开新增作业页面
const handleAdd = () => {
	state.editJobDetailTitle = '添加作业';
	editJobDetailRef.value?.openDialog({ concurrent: true, includeAnnotations: true, groupName: 'default', createType: JobCreateTypeEnum.NUMBER_2 });
};

// 打开编辑作业页面
const handleEdit = (row: JobDetailOutput) => {
	state.editJobDetailTitle = '编辑作业';
	editJobDetailRef.value?.openDialog(row.jobDetail);
};

// 删除作业
const handleDelete = (row: JobDetailOutput) => {
	ElMessageBox.confirm(`确定删除作业：【${row.jobDetail?.jobId}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysJobApi).apiSysJobDeleteJobDetailPost({ jobId: row.jobDetail?.jobId });
			await handleQuery();
			ElMessage.success('删除成功');
		})
		.catch(() => {});
};

// 作业表格事件
const gridEventsJob: VxeGridListeners<JobDetailOutput> = {
	// 只对 pager-config 配置时有效，分页发生改变时会触发该事件
	async pageChange({ pageSize }) {
		state.jobPageParam.pageSize = pageSize;
	},
	// 当排序条件发生变化时会触发该事件
	async sortChange({ field, order }) {
		state.jobPageParam.defaultSort = { field: field, order: order!, descStr: 'desc' };
	},
	// 双击行事件
	async cellDblclick({ row }) {
		if (auth('sysJob/updateJobDetail')) await handleEdit(row);
	},
};

// 打开新增触发器页面
const openAddJobTrigger = (row: JobDetailOutput) => {
	state.editJobTriggerTitle = '添加触发器';
	editJobTriggerRef.value?.openDialog({
		jobId: row.jobDetail?.jobId,
		retryTimeout: 1000,
		startNow: true,
		runOnStart: true,
		resetOnlyOnce: true,
		triggerType: 'Furion.Schedule.PeriodTrigger',
	});
};

// 打开编辑触发器页面
const openEditJobTrigger = (row: SysJobTrigger) => {
	state.editJobTriggerTitle = '编辑触发器';
	editJobTriggerRef.value?.openDialog(row);
};

// 删除触发器
const delJobTrigger = (row: SysJobTrigger) => {
	ElMessageBox.confirm(`确定删除触发器：【${row.triggerId}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysJobApi).apiSysJobDeleteJobTriggerPost({ jobId: row.jobId, triggerId: row.triggerId });
			await handleQuery();
			ElMessage.success('删除成功');
		})
		.catch(() => {});
};

// 启动所有作业
const startAllJob = async () => {
	await getAPI(SysJobApi).apiSysJobStartAllJobPost();
	ElMessage.success('启动所有作业');
};

// 暂停所有作业
const pauseAllJob = async () => {
	await getAPI(SysJobApi).apiSysJobPauseAllJobPost();
	ElMessage.success('暂停所有作业');
};

// 执行某个作业
const runJob = async (row: JobDetailOutput) => {
	await getAPI(SysJobApi).apiSysJobRunJobPost({ jobId: row.jobDetail?.jobId });
	ElMessage.success('执行作业');
};

// 启动某个作业
const startJob = async (row: JobDetailOutput) => {
	await getAPI(SysJobApi).apiSysJobStartJobPost({ jobId: row.jobDetail?.jobId });
	ElMessage.success('启动作业');
};

// 暂停某个作业
const pauseJob = async (row: JobDetailOutput) => {
	await getAPI(SysJobApi).apiSysJobPauseJobPost({ jobId: row.jobDetail?.jobId });
	ElMessage.success('暂停作业');
};

// 取消某个作业
const cancelJob = async (row: JobDetailOutput) => {
	await getAPI(SysJobApi).apiSysJobCancelJobPost({ jobId: row.jobDetail?.jobId });
	ElMessage.success('取消作业');
};

// 启动触发器
const startTrigger = async (row: SysJobTrigger) => {
	await getAPI(SysJobApi).apiSysJobStartTriggerPost({ jobId: row.jobId, triggerId: row.triggerId });
	ElMessage.success('启动触发器');
};

// 暂停触发器
const pauseTrigger = async (row: SysJobTrigger) => {
	await getAPI(SysJobApi).apiSysJobPauseTriggerPost({ jobId: row.jobId, triggerId: row.triggerId });
	ElMessage.success('暂停触发器');
};

// 强制唤醒作业调度器
const cancelSleep = async () => {
	await getAPI(SysJobApi).apiSysJobCancelSleepPost();
	ElMessage.success('强制唤醒作业调度器');
};

// 强制触发所有作业持久化
const persistAll = async () => {
	await getAPI(SysJobApi).apiSysJobPersistAllPost();
	ElMessage.success('强制触发所有作业持久化');
};

// 打开集群控制页面
const openJobCluster = () => {
	editJobClusterRef.value?.openDrawer();
};

// 打开任务看板
const openJobDashboard = () => {
	router.push({
		path: '/platform/job/dashboard',
	});
};

// 根据任务属性获取 HttpJobMessage
const getHttpJobMessage = (properties: string | undefined | null): HttpJobMessage => {
	return editJobDetailRef.value?.getHttpJobMessage(properties)!;
};

// 获取请求方法的对应描述
const getHttpMethodDesc = (httpMethodStr: string | undefined | null): string => {
	if (httpMethodStr === undefined || httpMethodStr === null || httpMethodStr === '') return '';
	for (const key in editJobDetailRef.value?.httpMethodDef) {
		if (editJobDetailRef.value?.httpMethodDef[key] === httpMethodStr) return key;
	}
	return '';
};

//全部展开
const handleExpand = () => {
	xGridJob.value?.setAllRowExpand(true);
};

//全部折叠
const handleFold = () => {
	xGridJob.value?.clearRowExpand();
};

// 打开作业触发器运行记录
const openJobTriggerRecord = async (row: any) => {
	state.currentJob = row;
	state.recordPageParam.jobId = row?.jobDetail?.jobId;
	state.isVisibleDrawer = true;
	await handleQueryRecord();
};

// 表格参数配置-触发器
const optionsRecord = useVxeTable<SysJobTriggerRecord>(
	{
		id: 'sysJobRecord',
		name: '执行记录',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 50, fixed: 'left' },
			{ field: 'jobId', title: '作业编号', minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'triggerId', title: '触发器编号', minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'numberOfRuns', title: '当前运行次数', minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'lastRunTime', title: '最近运行时间', minWidth: 130, showOverflow: 'tooltip' },
			{ field: 'nextRunTime', title: '下一次运行时间', minWidth: 130, showOverflow: 'tooltip' },
			{ field: 'status', title: '触发器状态', minWidth: 110, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'result', title: '执行结果', minWidth: 200, showOverflow: 'title' },
			{ field: 'elapsedTime', title: '耗时(ms)', minWidth: 80, showOverflow: 'tooltip' },
			{ field: 'createdTime', title: '创建时间', minWidth: 130, showOverflow: 'tooltip' },
		],
	}, // vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: ({ page, sort }) => handleQueryRecordApi(page, sort) } },
		// 排序配置
		sortConfig: { defaultSort: state.jobPageParam.defaultSort },
		// 分页配置
		pagerConfig: { pageSize: state.jobPageParam.pageSize },
		// 工具栏配置
		toolbarConfig: { export: true },
	}
);

// 查询api-作业触发器运行记录
const handleQueryRecordApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.recordPageParam, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageJobTriggerRecordInput;
	return getAPI(SysJobApi).apiSysJobPageJobTriggerRecordPost(params);
};

// 查询操作-作业触发器运行记录
const handleQueryRecord = async () => {
	await xGridRecord.value?.commitProxy('query');
};

// 作业表格事件
const gridEventsRecord: VxeGridListeners<SysJobTriggerRecord> = {
	// 只对 pager-config 配置时有效，分页发生改变时会触发该事件
	async pageChange({ pageSize }) {
		state.recordPageParam.pageSize = pageSize;
	},
	// 当排序条件发生变化时会触发该事件
	async sortChange({ field, order }) {
		state.recordPageParam.defaultSort = { field: field, order: order!, descStr: 'desc' };
	},
};

// 清空作业触发器运行记录
const handleClearJobTriggerRecord = async () => {
	ElMessageBox.confirm(`确定要清空日志?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			optionsRecord.loading = true;
			await getAPI(SysJobApi).apiSysJobClearJobTriggerRecordPost();
			optionsRecord.loading = false;
			ElMessage.success('清空成功');
			await handleQueryRecord();
		})
		.catch(() => {});
};
</script>

<style>
/* 此样式不能为 scoped */
.job-index-descriptions-label-style {
	width: 80px;
}
</style>

<style lang="scss" scoped>
:deep(.el-drawer__body) {
	padding: 5px;
	display: flex;
	flex-direction: column;
	height: 100%;
}
.full-table {
	flex: 1;

	:deep(.el-card__body) {
		height: 100%;
		display: flex;
		flex-direction: column;
	}
}
.jobTriggers {
	width: 4rem;
	display: inline-block;
	align-items: justify;
	text-align-last: justify;
}
.jobTriggersValue {
	color: black;
	min-width: 4.5rem;
	display: inline-block;
}
</style>
