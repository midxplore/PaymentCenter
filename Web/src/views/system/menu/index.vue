<template>
	<div class="sys-menu-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="菜单名称" prop="title">
							<el-input v-model="state.queryParams.title" placeholder="菜单名称" clearable @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="类型" prop="type">
							<el-select v-model="state.queryParams.type" placeholder="类型" clearable @clear="state.queryParams.type = undefined">
								<el-option label="目录" :value="1" />
								<el-option label="菜单" :value="2" />
								<el-option label="按钮" :value="3" />
							</el-select>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysMenu/list'" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'sysMenu/add'"> 新增 </el-button>
					<el-button-group style="padding-left: 12px">
						<el-button icon="ele-Expand" @click="handleExpand"> 全部展开 </el-button>
						<el-button icon="ele-Fold" @click="handleFold"> 全部折叠 </el-button>
					</el-button-group>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_title="{ row }">
					<SvgIcon :name="row.icon" />
					<span class="ml10">{{ $t(row.title) }}</span>
				</template>
				<template #row_type="{ row }">
					<g-sys-dict v-model="row.type" code="MenuTypeEnum" />
				</template>
				<template #row_status="{ row }">
					<el-switch v-model="row.status" :active-value="1" :inactive-value="2" size="small" @change="changeStatus(row)" />
				</template>
				<template #row_record="{ row }">
					<ModifyRecord :data="row" />
				</template>
				<template #row_buttons="{ row }">
					<el-button icon="ele-Edit" text type="primary" v-auth="'sysMenu/update'" @click="handleEdit(row)"> {{ $t('message.list.edit') }} </el-button>
					<el-button icon="ele-Delete" text type="danger" v-auth="'sysMenu/delete'" @click="handleDelete(row)"> {{ $t('message.list.delete') }} </el-button>
					<el-button icon="ele-CopyDocument" text type="primary" v-auth="'sysMenu/add'" @click="openCopyMenu(row)"> {{ $t('message.list.copy') }} </el-button>
				</template>
			</vxe-grid>
		</el-card>

		<EditMenu ref="editMenuRef" :title="state.title" :menuData="state.menuData" @handleQuery="handleQuery(false)" />
	</div>
</template>

<!-- 菜单管理 -->
<script lang="ts" setup name="sysMenu">
import { reactive, ref } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridListeners } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import SvgIcon from '/@/components/svgIcon/index.vue';
import { useI18n } from 'vue-i18n';
import { auth } from '/@/utils/authFunction';

import EditMenu from '/@/views/system/menu/component/editMenu.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysMenuApi } from '/@/api-services/system/api';
import { SysMenu, UpdateMenuInput } from '/@/api-services/system/models';

const i18n = useI18n();
const xGrid = ref<VxeGridInstance>();
const editMenuRef = ref<InstanceType<typeof EditMenu>>();
const state = reactive({
	menuData: [] as Array<SysMenu>,
	queryParams: {
		title: undefined,
		type: undefined,
	},
	title: '',
});

// 表格参数配置
const options = useVxeTable<SysMenu>(
	{
		id: 'sysMenu',
		name: i18n.t('message.list.menuInfo'),
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 80, fixed: 'left' },
			{ field: 'title', title: '菜单名称', minWidth: 180, showOverflow: 'tooltip', treeNode: true, align: 'left', headerAlign: 'center', slots: { default: 'row_title' } },
			{ field: 'type', title: '菜单类型', minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_type' } },
			{ field: 'path', title: '路由路径', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'component', title: '组件路径', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'permission', title: '权限标识', minWidth: 160, showOverflow: 'tooltip' },
			{ field: 'orderNo', title: '排序', width: 80, showOverflow: 'tooltip' },
			{ field: 'status', title: '状态', width: 100, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'record', title: '修改记录', width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 200, showOverflow: true, slots: { default: 'row_buttons' } },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		stripe: false,
		// 多选配置
		checkboxConfig: { range: false },
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: () => handleQueryApi() } },
		// 分页配置
		pagerConfig: { enabled: false },
		// 工具栏配置
		toolbarConfig: { export: true },
		// 树形配置
		treeConfig: { expandAll: false, reserve: true },
		// 行配置信息
		rowConfig: {
			keyField: 'id',
		},
	}
);

// 查询api
const handleQueryApi = async () => {
	const params = Object.assign(state.queryParams);
	return getAPI(SysMenuApi).apiSysMenuListGet(params.title, params.type);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	state.menuData = xGrid.value?.getTableData().tableData ?? [];
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.title = undefined;
	state.queryParams.type = undefined;
	// 调用vxe-grid的commitProxy(reload)方法，触发表格重新加载数据
	await xGrid.value?.commitProxy('reload');
};

// 打开新增页面
const handleAdd = () => {
	state.title = i18n.t('message.list.addMenu');
	editMenuRef.value?.openDialog({ type: 2, isHide: false, isKeepAlive: true, isAffix: false, isIframe: false, status: 1, orderNo: 100 });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = i18n.t('message.list.editMenu');
	editMenuRef.value?.openDialog(row);
};

// 打开复制页面
const openCopyMenu = (row: any) => {
	state.title = i18n.t('message.list.copyMenu');
	var copyRow = JSON.parse(JSON.stringify(row)) as UpdateMenuInput;
	copyRow.id = 0;
	copyRow.title = '';
	editMenuRef.value?.openDialog(copyRow);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(i18n.t('message.list.confirmDeleteMenu', { title: row.title }), i18n.t('message.list.hint'), {
		confirmButtonText: i18n.t('message.list.confirmButtonText'),
		cancelButtonText: i18n.t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysMenuApi).apiSysMenuDeletePost({ id: row.id });
			ElMessage.success(i18n.t('message.list.successDelete'));
			await handleQuery(false);
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<SysMenu> = {
	// 只对 proxy-config.ajax.query 配置时有效，当手动点击查询时会触发该事件
	async proxyQuery() {
		state.menuData = xGrid.value?.getTableData().tableData ?? [];
	},
	// 双击行事件
	async cellDblclick({ row }) {
		if (auth('sysMenu/update')) await handleEdit(row);
	},
};

// 全部展开
const handleExpand = () => {
	xGrid.value?.setAllTreeExpand(true);
};

// 全部折叠
const handleFold = () => {
	xGrid.value?.clearTreeExpand();
};

// 修改状态
const changeStatus = async (row: any) => {
	await getAPI(SysMenuApi)
		.apiSysMenuSetStatusPost({ id: row.id, status: row.status })
		.then(() => {
			ElMessage.success(i18n.t('message.list.operationSuccessful'));
		})
		.catch(() => {
			row.status = row.status == 1 ? 2 : 1;
		});
};
</script>
