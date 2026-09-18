<template>
	<div class="sys-notice-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item :label="$t('message.list.title')" prop="title">
							<el-input v-model="state.queryParams.title" :placeholder="$t('message.list.title')" clearable @keyup.enter="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item :label="$t('message.list.type')" prop="type">
							<el-select v-model="state.queryParams.type" :placeholder="$t('message.list.type')" clearable @clear="state.queryParams.type = undefined">
								<el-option :label="$t('message.list.notice')" :value="1" />
								<el-option :label="$t('message.list.announcement')" :value="2" />
							</el-select>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysNotice/page'" :loading="options.loading"> {{ $t('message.list.query') }} </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> {{ $t('message.list.reset') }} </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'sysNotice/add'"> {{ $t('message.list.add') }} </el-button>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_type="{ row }">
					<el-tag v-if="row.type === 1" type="success">{{ $t('message.list.notice') }}</el-tag>
					<el-tag v-else type="warning">{{ $t('message.list.announcement') }}</el-tag>
				</template>
				<template #row_status="{ row }">
					<el-tag v-if="row.status === 1" type="info">{{ $t('message.list.published') }}</el-tag>
					<el-tag v-else type="warning">{{ $t('message.list.unpublished') }}</el-tag>
				</template>
				<template #row_record="{ row }">
					<ModifyRecord :data="row" />
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip :content="$t('message.list.edit')" placement="top">
						<el-button icon="ele-Edit" size="small" text type="primary" @click="handleEdit(row)" v-auth="'sysNotice/update'" :disabled="row.status === 1" />
					</el-tooltip>
					<el-tooltip :content="$t('message.list.delete')" placement="top">
						<el-button icon="ele-Delete" size="small" text type="danger" @click="handleDelete(row)" v-auth="'sysNotice/delete'" :disabled="row.status === 1" />
					</el-tooltip>
					<el-button icon="ele-Position" size="small" text type="primary" @click="handlePublic(row)" v-auth="'sysNotice/public'" :disabled="row.status === 1">{{
						$t('message.list.publish')
					}}</el-button>
				</template>
			</vxe-grid>
		</el-card>

		<EditNotice ref="editNoticeRef" :title="state.title" @handleQuery="handleQuery" />
	</div>
</template>

<!-- 通知公告 -->
<script lang="ts" setup name="sysNotice">
import { onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import commonFunction from '/@/utils/commonFunction';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { useI18n } from 'vue-i18n';
import { auth } from '/@/utils/authFunction';

import EditNotice from '/@/views/system/notice/component/editNotice.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysNoticeApi } from '/@/api-services/system/api';
import { SysNotice, PageNoticeInput } from '/@/api-services/system/models';

const i18n = useI18n();
const xGrid = ref<VxeGridInstance>();
const editNoticeRef = ref<InstanceType<typeof EditNotice>>();
const { removeHtml } = commonFunction();
const state = reactive({
	queryParams: {
		title: undefined,
		type: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'orderNo', order: 'asc', descStr: 'desc' },
	},
	visible: false,
	title: '',
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysNotice';
// 表格参数配置
const options = useVxeTable<SysNotice>(
	{
		id: 'sysNotice',
		name: i18n.t('message.list.notice'),
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: i18n.t('message.list.seq'), width: 60, fixed: 'left' },
			{ field: 'title', title: i18n.t('message.list.title'), minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'content', title: i18n.t('message.list.content'), minWidth: 180, showOverflow: 'tooltip', slots: { default: (scope: any) => removeHtml(scope.row.content) } },
			{ field: 'type', title: i18n.t('message.list.type'), minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_type' } },
			{ field: 'createTime', title: i18n.t('message.list.createTime'), minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'status', title: i18n.t('message.list.status'), minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'publicUserName', title: i18n.t('message.list.publisher'), minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'publicTime', title: i18n.t('message.list.publishTime'), minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'record', title: i18n.t('message.list.record'), width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: i18n.t('message.list.operation'), fixed: 'right', width: 180, showOverflow: true, slots: { default: 'row_buttons' } },
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
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageNoticeInput;
	return getAPI(SysNoticeApi).apiSysNoticePagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.title = undefined;
	state.queryParams.type = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 打开新增页面
const handleAdd = () => {
	state.title = i18n.t('message.list.addNoticeAnnouncement');
	editNoticeRef.value?.openDialog({ type: 1 });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = i18n.t('message.list.editNoticeAnnouncement');
	editNoticeRef.value?.openDialog(row);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(`${i18n.t('message.list.confirmDeleteNoticeAnnouncement')}【${row.title}】?`, i18n.t('message.list.hint'), {
		confirmButtonText: i18n.t('message.list.confirmButtonText'),
		cancelButtonText: i18n.t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysNoticeApi).apiSysNoticeDeletePost({ id: row.id });
			handleQuery();
			ElMessage.success(i18n.t('message.list.successDelete'));
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<SysNotice> = {
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
		if (auth('sysNotice/update') && row.status !== 1) await handleEdit(row);
	},
};

// 发布
const handlePublic = (row: any) => {
	ElMessageBox.confirm(`${i18n.t('message.list.confirmPublishNoticeAnnouncement')}【${row.title}】，${i18n.t('message.list.irreversible')}?`, i18n.t('message.list.hint'), {
		confirmButtonText: i18n.t('message.list.confirmButtonText'),
		cancelButtonText: i18n.t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysNoticeApi).apiSysNoticePublicPost({ id: row.id });
			handleQuery();
			ElMessage.success(i18n.t('message.list.publishSuccess'));
		})
		.catch(() => {});
};
</script>
