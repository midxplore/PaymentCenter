<template>
	<div class="pay-account-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="收款类型" prop="type">
							<el-select v-model="state.queryParams.type" placeholder="收款类型" clearable style="width: 100%">
								<el-option v-for="item in state.typeOptions" :key="item.value" :label="item.label" :value="item.value" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="账号状态" prop="status">
							<el-select v-model="state.queryParams.status" placeholder="账号状态" clearable style="width: 100%">
								<el-option v-for="item in statusOptions" :key="item.value" :label="item.label" :value="item.value" />
							</el-select>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'payAccount/page'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'payAccount/add'"> 新增 </el-button>
					<el-tooltip content="额度 = 总额度 − 已用 − 预占。预占是「订单已创建但还没到账」的金额，过期会自动释放。" placement="top">
						<el-icon style="margin-left: 6px; color: var(--el-text-color-secondary)"><ele-QuestionFilled /></el-icon>
					</el-tooltip>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_type="{ row }">
					{{ state.typeLabelMap[row.type] ?? row.type }}
				</template>
				<template #row_account="{ row }">
					<div class="account-cell">
						<el-image v-if="row.qrImageUrl" :src="row.qrImageUrl" :preview-src-list="[row.qrImageUrl]" fit="cover" class="qr-thumb" preview-teleported />
						<span>{{ row.accountInfo || (row.qrImageUrl ? '收款码' : '-') }}</span>
					</div>
				</template>
				<template #row_status="{ row }">
					<el-tag :type="statusTagType(row.status)">{{ row.statusText }}</el-tag>
				</template>
				<template #row_remainingQuota="{ row }">
					<span :class="{ 'text-danger': Number(row.remainingQuota) <= 0 }">{{ money(row.remainingQuota) }}</span>
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip content="编辑" placement="top">
						<el-button icon="ele-Edit" size="small" text type="primary" @click="handleEdit(row)" v-auth="'payAccount/update'" />
					</el-tooltip>
					<el-tooltip content="追加额度" placement="top">
						<el-button icon="ele-Coin" size="small" text type="warning" @click="handleAddQuota(row)" v-auth="'payAccount/addQuota'" />
					</el-tooltip>
					<el-tooltip :content="row.status === 1 ? '停用' : '启用'" placement="top">
						<el-button
							:icon="row.status === 1 ? 'ele-VideoPause' : 'ele-VideoPlay'"
							size="small"
							text
							:type="row.status === 1 ? 'info' : 'success'"
							:disabled="row.status === 3"
							@click="handleSetStatus(row)"
							v-auth="'payAccount/setStatus'"
						/>
					</el-tooltip>
					<el-tooltip content="删除" placement="top">
						<el-button icon="ele-Delete" size="small" text type="danger" @click="handleDelete(row)" v-auth="'payAccount/delete'" />
					</el-tooltip>
				</template>
			</vxe-grid>
		</el-card>

		<EditPayAccount ref="editRef" :title="state.title" @handleQuery="handleQuery" />
		<AddQuota ref="quotaRef" @handleQuery="handleQuery" />
	</div>
</template>

<!-- 收款账号管理（F1） -->
<script lang="ts" setup name="payAccount">
import { onMounted, reactive, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { auth } from '/@/utils/authFunction';

import EditPayAccount from '/@/views/paycenter/account/component/editPayAccount.vue';
import AddQuota from '/@/views/paycenter/account/component/addQuota.vue';

import { getAPI } from '/@/utils/axios-utils';
import { PayAccountApi } from '/@/api-services/system/api';
import { PagePayAccountInput, PayAccountOutput, PayTypeOption } from '/@/api-services/system/models';

const xGrid = ref<VxeGridInstance>();
const editRef = ref<InstanceType<typeof EditPayAccount>>();
const quotaRef = ref<InstanceType<typeof AddQuota>>();
const state = reactive({
	queryParams: {
		type: undefined,
		status: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'createTime', order: 'desc', descStr: 'desc' },
	},
	title: '',
	typeOptions: [] as Array<PayTypeOption>,
	// 收款类型值 → 中文标签（列表里存的是字典值，如 wxpay）
	typeLabelMap: {} as Record<string, string>,
});

// 状态选项（值取自后端 PayAccountStatusEnum：1启用 2停用 3已用完）
const statusOptions = [
	{ label: '启用', value: 1 },
	{ label: '停用', value: 2 },
	{ label: '已用完', value: 3 },
];

// 本地存储参数
const localPageParamKey = 'localPageParam:payAccount';
// 表格参数配置
const options = useVxeTable<PayAccountOutput>(
	{
		id: 'payAccount',
		name: '收款账号',
		columns: [
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'type', title: '收款类型', minWidth: 120, slots: { default: 'row_type' } },
			{ field: 'accountInfo', title: '账号信息', minWidth: 240, slots: { default: 'row_account' } },
			{ field: 'totalQuota', title: '总额度', minWidth: 110, align: 'right', formatter: ({ cellValue }) => money(cellValue) },
			{ field: 'usedQuota', title: '已用额度', minWidth: 110, align: 'right', formatter: ({ cellValue }) => money(cellValue) },
			{ field: 'lockedQuota', title: '预占额度', minWidth: 110, align: 'right', formatter: ({ cellValue }) => money(cellValue) },
			{ field: 'remainingQuota', title: '剩余可用', minWidth: 110, align: 'right', slots: { default: 'row_remainingQuota' } },
			{ field: 'status', title: '状态', minWidth: 90, slots: { default: 'row_status' } },
			{ field: 'remark', title: '备注', minWidth: 140, showOverflow: 'tooltip' },
			{ field: 'createUserName', title: '创建人', minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'createTime', title: '创建时间', minWidth: 160, showOverflow: 'tooltip', formatter: ({ cellValue }) => fmtTime(cellValue) },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 150, showOverflow: true, slots: { default: 'row_buttons' } },
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
onMounted(async () => {
	state.localPageParam = Local.get(localPageParamKey) || state.localPageParam;

	// 收款类型下拉取自字典（后台可自行增删类型，前端不写死）
	const res = await getAPI(PayAccountApi).apiPayAccountTypeOptionsGet();
	state.typeOptions = res.data.result ?? [];
	state.typeLabelMap = state.typeOptions.reduce((acc: Record<string, string>, item) => {
		if (item.value) acc[item.value] = item.label ?? item.value;
		return acc;
	}, {});
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PagePayAccountInput;
	return getAPI(PayAccountApi).apiPayAccountPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.type = undefined;
	state.queryParams.status = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 表格事件
const gridEvents: VxeGridListeners<PayAccountOutput> = {
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
	// 双击行事件
	async cellDblclick({ row }) {
		if (auth('payAccount/update')) await handleEdit(row);
	},
};

// 打开新增页面（type 是字典字符串如 bank/wxpay，不要塞数字）
const handleAdd = () => {
	state.title = '新增收款账号';
	editRef.value?.openDialog({ type: undefined, totalQuota: 0 });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = '编辑收款账号';
	editRef.value?.openDialog(row);
};

// 追加额度
const handleAddQuota = (row: any) => {
	quotaRef.value?.openDialog(row);
};

// 启用/停用
const handleSetStatus = (row: any) => {
	const target = row.status === 1 ? 2 : 1;
	const label = target === 1 ? '启用' : '停用';
	ElMessageBox.confirm(`确定${label}收款账号：【${accountLabel(row)}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(PayAccountApi).apiPayAccountSetStatusPost({ id: row.id, status: target });
			ElMessage.success(`${label}成功`);
			await handleQuery();
		})
		.catch(() => {});
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(`确定删除收款账号：【${accountLabel(row)}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(PayAccountApi).apiPayAccountDeletePost({ id: row.id });
			ElMessage.success('删除成功');
			await handleQuery();
		})
		.catch(() => {});
};

const accountLabel = (row: any) => row.accountInfo || (row.qrImageUrl ? '收款码图片' : row.type || '');
const money = (value: any) => (value === undefined || value === null ? '-' : Number(value).toFixed(2));
// 时间展示
const fmtTime = (value: any) => {
	if (!value) return '';
	const d = new Date(value);
	if (isNaN(d.getTime())) return String(value);
	const p = (n: number) => String(n).padStart(2, '0');
	return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
};
// 状态色（1启用 2停用 3已用完）
const statusTagType = (status: any) => {
	switch (Number(status)) {
		case 1:
			return 'success';
		case 2:
			return 'info';
		case 3:
			return 'danger';
		default:
			return 'info';
	}
};
</script>

<style lang="scss" scoped>
.text-danger {
	color: var(--el-color-danger);
	font-weight: 600;
}
.account-cell {
	display: flex;
	align-items: center;
	gap: 8px;
}
.qr-thumb {
	width: 36px;
	height: 36px;
	flex: none;
	border-radius: 2px;
}
</style>
