<template>
	<div class="notice-container">
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
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" :loading="options.loading"> {{ $t('message.list.query') }} </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> {{ $t('message.list.reset') }} </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents" @cell-dblclick="({ row }: any) => handleView(row)">
				<template #toolbar_buttons> </template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_type="{ row }">
					<el-tag v-if="row.sysNotice.type === 1" type="success">{{ $t('message.list.notice') }}</el-tag>
					<el-tag v-else type="warning">{{ $t('message.list.announcement') }}</el-tag>
				</template>
				<template #row_readStatus="{ row }">
					<el-tag v-if="row.readStatus === 1" type="info">{{ $t('message.list.read') }}</el-tag>
					<el-tag v-else type="danger">{{ $t('message.list.unread') }}</el-tag>
				</template>
				<template #row_buttons="{ row }">
					<el-tooltip :content="$t('message.list.detail')" placement="top">
						<el-button icon="ele-InfoFilled" text type="primary" @click="handleView(row)"> </el-button>
					</el-tooltip>
				</template>
			</vxe-grid>
		</el-card>

		<el-dialog v-model="state.isShowDialog" draggable width="769px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Bell /> </el-icon>
					<span> {{ $t('message.list.messageDetail') }} </span>
				</div>
			</template>
			<div class="w-e-text-container">
				<div v-html="state.content" data-slate-editor></div>
			</div>
			<template #footer>
				<span class="dialog-footer">
					<el-button type="primary" @click="state.isShowDialog = false">{{ $t('message.list.confirm') }}</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<!-- 通知公告 -->
<script setup lang="ts" name="notice">
import { onMounted, reactive, ref } from 'vue';
import commonFunction from '/@/utils/commonFunction';
import '@wangeditor/editor/dist/css/style.css';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { useI18n } from 'vue-i18n';

import { getAPI } from '/@/utils/axios-utils';
import { SysNoticeApi } from '/@/api-services/system/api';
import { SysNoticeUser, PageNoticeInput } from '/@/api-services/system/models';

const { t } = useI18n();
const xGrid = ref<VxeGridInstance>();
const { removeHtml } = commonFunction();
const state = reactive({
	queryParams: {
		title: undefined,
		type: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'id', order: 'asc', descStr: 'desc' },
	},
	isShowDialog: false,
	title: '',
	content: '',
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysNoticeUser';
// 表格参数配置
const options = useVxeTable<SysNoticeUser>(
	{
		id: 'sysNoticeUser',
		name: t('message.list.messageInbox'),
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: t('message.list.seq'), width: 60, fixed: 'left' },

			{ field: 'sysNotice.title', title: t('message.list.title'), minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'sysNotice.content', title: t('message.list.content'), minWidth: 180, showOverflow: 'tooltip', slots: { default: (scope: any) => removeHtml(scope.row.sysNotice.content) } },

			{ field: 'sysNotice.type', title: t('message.list.type'), minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_type' } },
			{ field: 'sysNotice.createTime', title: t('message.list.createTime'), minWidth: 150, showOverflow: 'tooltip' },

			{ field: 'readStatus', title: t('message.list.readStatus'), minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_readStatus' } },
			{ field: 'sysNotice.publicUserName', title: t('message.list.publisher'), minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'sysNotice.publicTime', title: t('message.list.publishTime'), minWidth: 150, showOverflow: 'tooltip' },

			{ field: 'buttons', title: t('message.list.operation'), fixed: 'right', width: 100, showOverflow: true, slots: { default: 'row_buttons' } },
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
	return getAPI(SysNoticeApi).apiSysNoticePageReceivedPost(params);
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

// 查看详情
const handleView = async (row: any) => {
	state.content = row.sysNotice.content;
	state.isShowDialog = true;
	row.readStatus = 1;
	// mittBus.emit('noticeRead', row.sysNotice.id);
	await getAPI(SysNoticeApi).apiSysNoticeSetReadPost({ id: row.sysNotice.id });
};

// 表格事件
const gridEvents: VxeGridListeners<SysNoticeUser> = {
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
</script>

<style lang="scss" scoped>
:deep(.el-dialog__body) {
	min-height: 600px;
}
</style>
