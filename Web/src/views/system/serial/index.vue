<template>
	<div class="sys-serial-container">
		<el-card shadow="hover" :body-style="{ padding: '5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="分类" prop="type">
							<el-select v-model="state.queryParams.type" placeholder="分类" clearable @keyup.enter.native="handleQuery(false)">
								<el-option v-for="item in state.typeData" :key="item.value" :label="item.text" :value="item.value" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col class="mb5" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="状态" prop="status">
							<g-sys-dict v-model="state.queryParams.status" :code="'StatusEnum'" render-as="select" clearable @keyup.enter.native="handleQuery(false)" />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />
			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(false)" v-auth="'sysSerial/page'"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'sysSerial/add'"> 新增 </el-button>
				</template>
				<template #toolbar_tools></template>
				<template #empty><el-empty :image-size="200" /></template>
				<template #row_record="{ row }"><ModifyRecord :data="row" /></template>
				<template #row_expy="{ row, $index }">
					{{ commonFun.dateFormatYMDHMS(row, $index, row.expy) }}
				</template>
				<template #row_type="{ row, $index }">
					<el-tag v-for="item in state.typeData" v-show="item.value === row.type">{{ item.text }}</el-tag>
				</template>
				<template #row_seq="{ row }">
					{{ row.seq }}
				</template>
				<template #row_resetInterval="{ row, $index }">
					<g-sys-dict v-model="row.resetInterval" :code="'ResetIntervalEnum'" />
				</template>
				<template #row_status="{ row, $index }">
					<el-switch v-model="row.status" :active-value="1" :inactive-value="2" disabled />
				</template>
				<template #row_buttons="{ row }">
					<el-button icon="ele-Edit" text type="primary" @click="handleEdit(row)" v-auth="'sysSerial/update'">{{ $t('message.list.edit') }}</el-button>
					<el-button icon="ele-Delete" text type="danger" @click="handleDelete(row)" v-auth="'sysSerial/delete'">{{ $t('message.list.delete') }}</el-button>
				</template>
			</vxe-grid>
		</el-card>

		<EditSerial ref="editSerialRef" :title="state.title" @handleQuery="handleQuery" />
	</div>
</template>

<!-- 流水号 -->
<script lang="ts" setup name="sysSerial">
import { onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import commonFunction from '/@/utils/commonFunction';
import { Local } from '/@/utils/storage';
import { useI18n } from 'vue-i18n';
import { auth } from '/@/utils/authFunction';

import ModifyRecord from '/@/components/table/modifyRecord.vue';
import EditSerial from './component/editSerial.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysSerialApi } from '/@/api-services/system/api';
import { PageSerialInput, PageSerialOutput } from '/@/api-services/system/models';

const i18n = useI18n();
const commonFun = commonFunction();
const xGrid = ref<VxeGridInstance>();
const editSerialRef = ref<InstanceType<typeof EditSerial>>();
const state = reactive({
	queryParams: {} as PageSerialInput,
	showAdvanceQueryUI: false,
	localPageParam: {
		pageSize: 20 as number,
		defaultSort: { field: 'id', order: 'asc', descStr: 'desc' },
	},
	typeData: [] as any[],
	visible: false,
	title: '',
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysSerial';
// 表格参数配置
const options = useVxeTable<PageSerialOutput>(
	{
		id: 'sysSerial',
		name: '流水号',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'type', title: '分类', minWidth: 150, showOverflow: 'tooltip', slots: { default: 'row_type' } },
			{ field: 'resetInterval', title: '重置间隔', minWidth: 120, showOverflow: 'tooltip', slots: { default: 'row_resetInterval' } },
			{ field: 'formater', title: '表达式', minWidth: 220, showOverflow: 'tooltip' },
			{ field: 'currentSeq', title: '当前序列号', minWidth: 80, showOverflow: 'tooltip', slots: { default: 'row_seq' } },
			{ field: 'min', title: '最小值', minWidth: 80, showOverflow: 'tooltip' },
			{ field: 'max', title: '最大值', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'expy', title: '有效期', minWidth: 150, showOverflow: 'tooltip', slots: { default: 'row_expy' } },
			{ field: 'orderNo', title: '排序', minWidth: 100, sortable: true, showOverflow: 'tooltip' },
			{ field: 'status', title: '状态', minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'remark', title: '备注', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'record', title: '修改记录', width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 180, showOverflow: true, slots: { default: 'row_buttons' } },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		// 代理配置
		proxyConfig: { autoLoad: false, ajax: { query: ({ page, sort }) => handleQueryApi(page, sort) } },
		// 排序配置
		sortConfig: { defaultSort: Local.get(localPageParamKey)?.defaultSort || state.localPageParam.defaultSort },
		// 分页配置
		pagerConfig: { pageSize: Local.get(localPageParamKey)?.pageSize || state.localPageParam.pageSize },
		// 导入配置
		// importConfig: { remote: true, importMethod: (options: any) => handleImport(options), slots: { top: 'import_sysSerial' } },
		// 工具栏配置
		toolbarConfig: { import: false, export: true },
	}
);

// 页面初始化
onMounted(async () => {
	state.localPageParam = Local.get(localPageParamKey) || state.localPageParam;
	state.typeData = await getAPI(SysSerialApi)
		.apiSysSerialTypeListGet()
		.then((res) => res.data.result ?? []);
	handleQuery(true);
});

// 查询api
const handleQueryApi = (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageSerialInput;
	return getAPI(SysSerialApi).apiSysSerialPagePost(params);
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

// 打开新增页面
const handleAdd = () => {
	state.title = '添加流水号'; // i18n.t('message.list.addPosition');
	editSerialRef.value?.openDialog({ resetInterval: 1, min: 1, max: 99999999, status: 1, orderNo: 100 }, state.typeData);
};

// 打开编辑页面
const handleEdit = async (row: any) => {
	state.title = '编辑流水号'; //i18n.t('message.list.editPosition');
	editSerialRef.value?.openDialog(row, state.typeData);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(i18n.t('message.list.confirmDelete', { name: row.name }), i18n.t('message.list.hint'), {
		confirmButtonText: i18n.t('message.list.confirmButtonText'),
		cancelButtonText: i18n.t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysSerialApi).apiSysSerialDeletePost({ id: row.id });
			handleQuery();
			ElMessage.success(i18n.t('message.list.successDelete'));
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<PageSerialOutput> = {
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
		if (auth('sysSerial/update')) await handleEdit(row);
	},
};
</script>
