<template>
	<div class="pay-audit-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="操作动作" prop="action">
							<el-select v-model="state.queryParams.action" placeholder="操作动作" clearable style="width: 100%">
								<el-option v-for="item in actionOptions" :key="item.value" :label="item.label" :value="item.value" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="对象类型" prop="targetType">
							<el-input v-model="state.queryParams.targetType" placeholder="如 PayAccount / Order" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="对象标识" prop="targetNo">
							<el-input v-model="state.queryParams.targetNo" placeholder="对象标识（如账号类型）" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="操作人" prop="operatorName">
							<el-input v-model="state.queryParams.operatorName" placeholder="操作人姓名" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="操作时间" prop="startTime">
							<el-date-picker
								v-model="state.queryParams.startTime"
								type="datetime"
								placeholder="操作时间起"
								format="YYYY-MM-DD HH:mm:ss"
								value-format="YYYY-MM-DD HH:mm:ss"
								:shortcuts="shortcuts"
								class="w100"
							/>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="" prop="endTime">
							<el-date-picker
								v-model="state.queryParams.endTime"
								type="datetime"
								placeholder="操作时间止"
								format="YYYY-MM-DD HH:mm:ss"
								value-format="YYYY-MM-DD HH:mm:ss"
								:shortcuts="shortcuts"
								class="w100"
							/>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'payAudit/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents" @cell-dblclick="handleView">
				<template #toolbar_buttons>
					<el-button icon="ele-FolderOpened" type="primary" @click="exportAuditLog" v-auth="'payExport/exportAuditLog'" :loading="state.exporting"> 导出审计日志 </el-button>
					<el-tooltip content="本表只增不改：账号新增/编辑/追加额度/状态变更/删除、异常到账处理、数据导出都会留痕，含变更前后值" placement="top">
						<el-icon style="margin-left: 6px; color: var(--el-text-color-secondary)"><ele-QuestionFilled /></el-icon>
					</el-tooltip>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_action="{ row }">
					<el-tag>{{ actionLabel(row.action) }}</el-tag>
				</template>
				<template #row_buttons="{ row }">
					<el-button icon="ele-InfoFilled" text type="primary" @click="handleView({ row })"> 详情 </el-button>
				</template>
			</vxe-grid>
		</el-card>

		<el-dialog v-model="state.visible" draggable overflow destroy-on-close width="720px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Document /> </el-icon>
					<span> 审计详情 </span>
				</div>
			</template>
			<el-descriptions :column="2" border size="small">
				<el-descriptions-item label="操作时间">{{ fmtTime(state.detail.createTime) }}</el-descriptions-item>
				<el-descriptions-item label="操作动作">{{ actionLabel(state.detail.action) }}</el-descriptions-item>
				<el-descriptions-item label="对象类型">{{ state.detail.targetType || '-' }}</el-descriptions-item>
				<el-descriptions-item label="对象标识">{{ state.detail.targetNo || '-' }}</el-descriptions-item>
				<el-descriptions-item label="操作人">{{ state.detail.operatorName || '-' }}</el-descriptions-item>
				<el-descriptions-item label="操作IP">{{ state.detail.operatorIp || '-' }}</el-descriptions-item>
				<el-descriptions-item label="说明" :span="2">{{ state.detail.remark || '-' }}</el-descriptions-item>
			</el-descriptions>

			<el-divider content-position="left">变更前</el-divider>
			<pre class="json-pre">{{ pretty(state.detail.beforeJson) }}</pre>
			<el-divider content-position="left">变更后</el-divider>
			<pre class="json-pre">{{ pretty(state.detail.afterJson) }}</pre>
		</el-dialog>
	</div>
</template>

<!-- 业务审计日志（F7.3） -->
<script lang="ts" setup name="payAudit">
import { onMounted, reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';
import { useDateTimeShortCust } from '/@/hooks/dateTimeShortCust';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { downloadByData, getFileName } from '/@/utils/download';

import { getAPI } from '/@/utils/axios-utils';
import { PayAuditApi, PayExportApi } from '/@/api-services/system/api';
import { PagePayAuditLogInput, PayAuditLog, PayExportInput } from '/@/api-services/system/models';
import { resolveExportRange, toExportInput } from '/@/views/paycenter/utils/exportParams';

const shortcuts = useDateTimeShortCust();
const xGrid = ref<VxeGridInstance>();
const state = reactive({
	queryParams: {
		action: undefined,
		targetType: undefined,
		targetNo: undefined,
		operatorName: undefined,
		startTime: undefined,
		endTime: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'createTime', order: 'desc', descStr: 'desc' },
	},
	exporting: false,
	visible: false,
	detail: {} as PayAuditLog,
});

// 动作下拉：值取自后端 PayAuditActionEnum，中文与后端 [Description] 一致
const actionOptions = [
	{ label: '新增收款账号', value: 1 },
	{ label: '编辑收款账号', value: 2 },
	{ label: '追加额度', value: 3 },
	{ label: '变更账号状态', value: 4 },
	{ label: '删除收款账号', value: 5 },
	{ label: '异常到账处理', value: 6 },
	{ label: '数据导出', value: 7 },
	// 开放接口审计（2026-09-17 新增）：ApiCall = 资金动作由某个接入方发起；
	// ApiAuthFailure = 签名鉴权失败（401 发生在 MVC 之前，SysLogOp 采不到，只能落这张表）
	{ label: '开放接口调用', value: 8 },
	{ label: '开放接口认证失败', value: 9 },
];

// 本地存储参数
const localPageParamKey = 'localPageParam:payAudit';
// 表格参数配置
const options = useVxeTable<PayAuditLog>(
	{
		id: 'payAudit',
		name: '业务审计日志',
		columns: [
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'createTime', title: '操作时间', minWidth: 160, showOverflow: 'tooltip', formatter: ({ cellValue }) => fmtTime(cellValue) },
			{ field: 'action', title: '操作动作', minWidth: 120, slots: { default: 'row_action' } },
			{ field: 'targetType', title: '对象类型', minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'targetNo', title: '对象标识', minWidth: 140, showOverflow: 'tooltip' },
			{ field: 'remark', title: '说明', minWidth: 240, showOverflow: 'tooltip' },
			{ field: 'operatorName', title: '操作人', minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'operatorIp', title: '操作IP', minWidth: 130, showOverflow: 'tooltip' },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 90, showOverflow: true, slots: { default: 'row_buttons' } },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: ({ page, sort }) => handleQueryApi(page, sort) } },
		// 排序配置
		sortConfig: { defaultSort: Local.get(localPageParamKey)?.defaultSort || state.localPageParam.defaultSort },
		// 分页配置
		pagerConfig: { pageSize: Local.get(localPageParamKey)?.pageSize || state.localPageParam.pageSize },
		// 工具栏配置
		toolbarConfig: { export: true },
	}
);

// 页面初始化
onMounted(() => {
	state.localPageParam = Local.get(localPageParamKey) || state.localPageParam;
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PagePayAuditLogInput;
	return getAPI(PayAuditApi).apiPayAuditPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.action = undefined;
	state.queryParams.targetType = undefined;
	state.queryParams.targetNo = undefined;
	state.queryParams.operatorName = undefined;
	state.queryParams.startTime = undefined;
	state.queryParams.endTime = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 表格事件
const gridEvents: VxeGridListeners<PayAuditLog> = {
	// 只对 pager-config 配置时有效，分页发生改变时会触发该事件
	async pageChange({ pageSize }) {
		state.localPageParam.pageSize = pageSize;
		Local.set(localPageParamKey, state.localPageParam);
	},
	// 当排序条件发生变化时会触发该事件
	async sortChange({ field, order }) {
		state.localPageParam.defaultSort = { field: field, order: order!, descStr: 'desc' };
		Local.set(localPageParamKey, state.localPageParam);
	},
};

// 查看详情（含变更前后值）
const handleView = ({ row }: any) => {
	state.detail = JSON.parse(JSON.stringify(row));
	state.visible = true;
};

// 导出审计日志（F7.5）
const exportAuditLog = async () => {
	// 审计表只增不改、量随时间增长，导出必须给区间；没填按最近 7 天兜底（见 utils/exportParams.ts）
	const params = toExportInput<PayExportInput>(resolveExportRange(state.queryParams));

	state.exporting = true;
	try {
		const res = await getAPI(PayExportApi).apiPayExportExportAuditLogPost(params, { responseType: 'blob' });
		downloadByData(res.data as any, getFileName(res.headers));
		ElMessage.success('导出成功');
	} finally {
		state.exporting = false;
	}
};

// 动作中文
const actionLabel = (action: any) => actionOptions.find((item) => item.value === Number(action))?.label ?? '-';
// 时间展示
const fmtTime = (value: any) => {
	if (!value) return '';
	const d = new Date(value);
	if (isNaN(d.getTime())) return String(value);
	const p = (n: number) => String(n).padStart(2, '0');
	return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
};
// JSON 美化（后端存的是字符串，可能不是合法 JSON，原样返回兜底）
const pretty = (value: any) => {
	if (!value) return '—';
	try {
		return JSON.stringify(JSON.parse(value), null, 2);
	} catch {
		return String(value);
	}
};
</script>

<style lang="scss" scoped>
.json-pre {
	margin: 0;
	padding: 10px;
	max-height: 260px;
	overflow: auto;
	font-size: 12px;
	line-height: 1.5;
	white-space: pre-wrap;
	word-break: break-all;
	background: var(--el-fill-color-light);
	border-radius: 4px;
}
</style>
