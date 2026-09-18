<template>
	<div class="sys-dict-container" v-loading="optionsDictType.loading">
		<el-row :gutter="8" style="width: 100%; height: 100%; flex: 1">
			<el-col :span="12" :xs="24" class="full-height">
				<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
					<el-form :model="state.queryParamsDictType" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
						<el-row :gutter="10">
							<el-col class="mb5" :xs="24" :sm="24" :md="24" :lg="12" :xl="12">
								<el-form-item label="字典名称" prop="name">
									<el-input v-model="state.queryParamsDictType.name" placeholder="字典名称" clearable @keyup.enter.native="handleQuery(true)" />
								</el-form-item>
							</el-col>
							<el-col class="mb5" :xs="24" :sm="24" :md="24" :lg="12" :xl="12">
								<el-form-item label="字典编码" prop="code">
									<el-input v-model="state.queryParamsDictType.code" placeholder="字典编码" clearable @keyup.enter.native="handleQuery(true)" />
								</el-form-item>
							</el-col>
						</el-row>
					</el-form>
					<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />
					<el-row>
						<el-col>
							<el-button-group>
								<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysDictType/page'" :loading="optionsDictType.loading"> 查询 </el-button>
								<el-button icon="ele-Refresh" @click="resetQuery" :loading="optionsDictType.loading"> 重置 </el-button>
							</el-button-group>
						</el-col>
					</el-row>
				</el-card>

				<el-card class="full-table" shadow="hover" style="margin-top: 5px">
					<vxe-grid ref="xGridDictType" class="xGrid-style" v-bind="optionsDictType" v-on="gridEventsDictType" @cell-click="handleDictData">
						<template #toolbar_buttons>
							<el-icon size="16" style="margin-right: 3px; margin-top: 2px; display: inline; vertical-align: middle"><ele-Collection /></el-icon>字典
							<el-button type="primary" style="margin-left: 20px" icon="ele-Plus" @click="handleAdd" v-auth="'sysDictType/add'"> 新增 </el-button>
						</template>
						<template #toolbar_tools> </template>
						<template #empty>
							<el-empty :image-size="200" />
						</template>
						<!-- <template #row_isTenant="{ row }">
							<g-sys-dict v-model="row.isTenant" code="YesNoEnum" />
						</template> -->
						<template #row_sysFlag="{ row }">
							<g-sys-dict v-model="row.sysFlag" code="YesNoEnum" />
						</template>
						<template #row_isEnum="{ row }">
							<g-sys-dict v-model="row.isEnum" code="YesNoEnum" />
						</template>
						<template #row_status="{ row }">
							<g-sys-dict v-model="row.status" code="StatusEnum" />
						</template>
						<template #row_record="{ row }">
							<ModifyRecord :data="row" />
						</template>
						<template #row_buttons="{ row }">
							<el-tooltip content="编辑" placement="top">
								<el-button icon="ele-Edit" text type="primary" v-auth="'sysDictType/update'" @click="handleEdit(row)" :disabled="row.sysFlag === 1"> </el-button>
							</el-tooltip>
							<el-tooltip content="删除" placement="top">
								<el-button icon="ele-Delete" text type="danger" v-auth="'sysDictType/delete'" @click="handleDeleteDictType(row)" :disabled="row.sysFlag === 1"> </el-button>
							</el-tooltip>
						</template>
					</vxe-grid>
				</el-card>
			</el-col>

			<el-col :span="12" :xs="24" class="full-height">
				<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
					<el-form :model="state.queryParamsDictData" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
						<el-row :gutter="10">
							<el-col class="mb5" :xs="24" :sm="8" :md="8" :lg="8" :xl="8">
								<el-form-item label="文本" prop="code">
									<el-input v-model="state.queryParamsDictData.label" placeholder="文本" clearable @keyup.enter.native="handleQueryDictData(true)" />
								</el-form-item>
							</el-col>
							<el-col class="mb5" :xs="24" :sm="8" :md="8" :lg="8" :xl="8">
								<el-form-item label="字典值" prop="value">
									<el-input v-model="state.queryParamsDictData.value" placeholder="字典值" clearable @keyup.enter.native="handleQueryDictData(true)" />
								</el-form-item>
							</el-col>
							<el-col class="mb5" :xs="24" :sm="8" :md="8" :lg="8" :xl="8">
								<el-form-item label="编码" prop="code">
									<el-input v-model="state.queryParamsDictData.code" placeholder="编码" clearable @keyup.enter.native="handleQueryDictData(true)" />
								</el-form-item>
							</el-col>
						</el-row>
					</el-form>
					<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />
					<el-row>
						<el-col>
							<el-button-group>
								<el-button type="primary" icon="ele-Search" @click="handleQueryDictData" v-auth="'sysDictType/page'" :loading="optionsDictData.loading"> 查询 </el-button>
								<el-button icon="ele-Refresh" @click="resetQueryDictData" :loading="optionsDictData.loading"> 重置 </el-button>
							</el-button-group>
						</el-col>
					</el-row>
				</el-card>

				<el-card class="full-table" shadow="hover" style="margin-top: 5px">
					<vxe-grid ref="xGridDictData" class="xGrid-style" v-bind="optionsDictData" v-on="gridEventsDictData">
						<template #toolbar_buttons>
							<el-icon size="16" style="margin-right: 3px; margin-top: 2px; display: inline; vertical-align: middle"><ele-Collection /></el-icon>字典值
							<el-button type="primary" style="margin-left: 20px" icon="ele-Plus" @click="handleAddDictData" v-auth="'sysDictType/add'"> 新增 </el-button>
						</template>
						<template #toolbar_tools> </template>
						<template #empty>
							<el-empty :image-size="200" />
						</template>
						<template #row_label="{ row }">
							<el-tag :type="row.tagType" :style="row.styleSetting" :class="row.classSetting">{{ row.label }}</el-tag>
						</template>
						<template #row_extData="{ row }">
							<el-tag type="warning" v-if="row.extData == null || row.extData == ''">空</el-tag>
							<el-tag type="success" v-else>有值</el-tag>
						</template>
						<template #row_status="{ row }">
							<el-tag v-if="row.status === 1" type="success">启用</el-tag>
							<el-tag v-else type="danger">禁用</el-tag>
						</template>
						<template #row_record="{ row }">
							<ModifyRecord :data="row" />
						</template>
						<template #row_buttons="{ row }">
							<el-tooltip content="编辑" placement="top">
								<el-button icon="ele-Edit" text type="primary" v-auth="'sysDictType/update'" @click="handleEditDictData(row)" :disabled="state.currentDictTypeRow.sysFlag === 1"> </el-button>
							</el-tooltip>
							<el-tooltip content="删除" placement="top">
								<el-button icon="ele-Delete" text type="danger" v-auth="'sysDictType/delete'" @click="handleDeleteDictData(row)" :disabled="state.currentDictTypeRow.sysFlag === 1"> </el-button>
							</el-tooltip>
							<el-tooltip content="复制">
								<el-button icon="ele-CopyDocument" text type="primary" v-auth="'sysDictType/add'" @click="openCopyDictData(row)"> </el-button>
							</el-tooltip>
						</template>
					</vxe-grid>
				</el-card>
			</el-col>
		</el-row>

		<EditDictType ref="editRefDictType" :title="state.title" @handleQuery="handleQuery" @handleUpdate="updateDictSession" />
		<EditDcitData ref="editRefDictData" :title="state.title" @handleQuery="handleQueryDictData" @handleUpdate="updateDictSession" />
	</div>
</template>

<!-- 字典管理 -->
<script lang="ts" setup name="sysDict">
import { onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { useUserInfo } from '/@/stores/userInfo';
import { auth } from '/@/utils/authFunction';

import EditDictType from '/@/views/system/dict/component/editDictType.vue';
import EditDcitData from '/@/views/system/dict/component/editDictData.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysDictTypeApi, SysDictDataApi } from '/@/api-services/system/api';
import { SysDictType, PageDictTypeInput, SysDictData, PageDictDataInput, UpdateDictDataInput } from '/@/api-services/system/models';

const xGridDictType = ref<VxeGridInstance>();
const xGridDictData = ref<VxeGridInstance>();
const editRefDictType = ref<InstanceType<typeof EditDictType>>();
const editRefDictData = ref<InstanceType<typeof EditDcitData>>();
const state = reactive({
	queryParamsDictType: {
		name: undefined,
		code: undefined,
	},
	queryParamsDictData: {
		dictTypeId: undefined,
		value: undefined,
		code: undefined,
		label: undefined,
	},
	localPageParamDictType: {
		pageSize: 50 as number,
		defaultSort: { field: 'orderNo', order: 'asc', descStr: 'desc' },
	},
	localPageParamDictData: {
		pageSize: 50 as number,
		defaultSort: { field: 'orderNo', order: 'asc', descStr: 'desc' },
	},
	title: '',
	currentDictTypeRow: {} as SysDictType, // 当前字典类型行数据
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysDictType';
// 表格参数配置-字典类型
const optionsDictType = useVxeTable<SysDictType>(
	{
		id: 'sysDictType',
		name: '字典信息',
		columns: [
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'name', title: '字典名称', minWidth: 140, align: 'left', headerAlign: 'center', showOverflow: 'tooltip' },
			{ field: 'code', title: '字典编码', minWidth: 140, align: 'left', headerAlign: 'center', showOverflow: 'tooltip' },
			// { field: 'isTenant', title: '租户隔离', width: 70, showOverflow: 'tooltip', visible: userInfo.accountType === AccountTypeEnum.NUMBER_999, slots: { default: 'row_isTenant' } },
			{ field: 'sysFlag', title: '是否内置', width: 70, showOverflow: 'tooltip', slots: { default: 'row_sysFlag' } },
			{ field: 'isEnum', title: '是否枚举', width: 70, showOverflow: 'tooltip', slots: { default: 'row_isEnum' } },
			{ field: 'orderNo', title: '排序', width: 70, showOverflow: 'tooltip' },
			{ field: 'status', title: '状态', width: 70, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'record', title: '修改记录', width: 90, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 100, showOverflow: true, slots: { default: 'row_buttons' } },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: ({ page, sort }) => handleQueryApi(page, sort) } },
		// 排序配置
		sortConfig: { defaultSort: Local.get(localPageParamKey)?.defaultSort || state.localPageParamDictType.defaultSort },
		// 分页配置
		pagerConfig: { pageSize: Local.get(localPageParamKey)?.pageSize || state.localPageParamDictType.pageSize },
		// 工具栏配置
		toolbarConfig: { export: true },
		// 行配置
		rowConfig: { isCurrent: true, isHover: true },
		// 多选配置
		checkboxConfig: { range: true, highlight: false },
	}
);

// 页面初始化
onMounted(() => {
	state.localPageParamDictType = Local.get(localPageParamKey) || state.localPageParamDictType;
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParamsDictType, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageDictTypeInput;
	return getAPI(SysDictTypeApi).apiSysDictTypePagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	reset ? await xGridDictType.value?.commitProxy('reload') : await xGridDictType.value?.commitProxy('query');
};

// 重置操作
const resetQuery = async () => {
	state.queryParamsDictType.code = undefined;
	state.queryParamsDictType.name = undefined;
	await xGridDictType.value?.commitProxy('reload');
};

// 打开新增页面
const handleAdd = () => {
	state.title = '添加字典';
	editRefDictType.value?.openDialog({ status: 1, orderNo: 100 });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = '编辑字典';
	editRefDictType.value?.openDialog(row);
};

// 点击字典类型，加载其字典值数据
const handleDictData = async (scope: any) => {
	state.currentDictTypeRow = scope.row;
	state.queryParamsDictData.dictTypeId = scope.row.id;
	await handleQueryDictData();
};

// 删除字典
const handleDeleteDictType = (row: any) => {
	ElMessageBox.confirm(`确定删除字典：【${row.name}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysDictTypeApi).apiSysDictTypeDeletePost({ id: row.id });
			await handleQuery();
			await updateDictSession();
			xGridDictData.value?.loadData([]);
			ElMessage.success('删除成功');
		})
		.catch(() => {});
};

// 表格事件
const gridEventsDictType: VxeGridListeners<SysDictType> = {
	// 只对 pager-config 配置时有效，分页发生改变时会触发该事件
	async pageChange({ pageSize }) {
		state.localPageParamDictType.pageSize = pageSize;
		// Local.set(localPageParamKey, state.localPageParam);
	},
	// 当排序条件发生变化时会触发该事件
	async sortChange({ field, order }) {
		state.localPageParamDictType.defaultSort = { field: field, order: order!, descStr: 'desc' };
		// Local.set(localPageParamKey, state.localPageParam);
	},
};

// 表格参数配置-字典值
const optionsDictData = useVxeTable<SysDictData>(
	{
		id: 'sysDictData',
		name: '字典值信息',
		columns: [
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'label', title: '显示文本', minWidth: 140, align: 'left', headerAlign: 'center', showOverflow: 'tooltip', slots: { default: 'row_label' } },
			{ field: 'value', title: '字典值', minWidth: 140, align: 'left', headerAlign: 'center', showOverflow: 'tooltip' },
			{ field: 'code', title: '编码', minWidth: 150, align: 'left', headerAlign: 'center', showOverflow: 'tooltip' },
			{ field: 'extData', title: '拓展数据', showOverflow: 'tooltip', slots: { default: 'row_extData' } },
			{ field: 'orderNo', title: '排序', width: 80, showOverflow: 'tooltip' },
			{ field: 'status', title: '状态', width: 80, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'record', title: '修改记录', width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 120, showOverflow: true, slots: { default: 'row_buttons' } },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: ({ page, sort }) => handleQueryDictDataApi(page, sort) } },
		// // 排序配置
		// sortConfig: { defaultSort: Local.get(localPageParamKey)?.defaultSort || state.localPageParam.defaultSort },
		// // 分页配置
		// pagerConfig: { pageSize: Local.get(localPageParamKey)?.pageSize || state.localPageParam.pageSize },
		// 工具栏配置
		toolbarConfig: { export: true },
		// 等待
		loading: false,
		// 行配置
		rowConfig: { isCurrent: true, isHover: true },
		// 多选配置
		checkboxConfig: { range: true, highlight: false },
	}
);

// 查询api
const handleQueryDictDataApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	if (state.queryParamsDictData.dictTypeId == undefined) return;
	const params = Object.assign(state.queryParamsDictData, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageDictDataInput;
	return getAPI(SysDictDataApi).apiSysDictDataPagePost(params);
};

// 查询操作
const handleQueryDictData = async (reset = false) => {
	reset ? await xGridDictData.value?.commitProxy('reload') : await xGridDictData.value?.commitProxy('query');
};

// 重置操作
const resetQueryDictData = async () => {
	state.queryParamsDictData.value = undefined;
	state.queryParamsDictData.code = undefined;
	state.queryParamsDictData.label = undefined;
	await xGridDictData.value?.commitProxy('reload');
};

// 添加字典值
const handleAddDictData = () => {
	if (!state.queryParamsDictData.dictTypeId) {
		ElMessage.warning('请选择字典');
		return;
	}
	state.title = '添加字典值';
	editRefDictData.value?.openDialog({ status: 1, orderNo: 100, dictTypeId: state.queryParamsDictData.dictTypeId });
};

// 打开编辑页面
const handleEditDictData = (row: any) => {
	state.title = '编辑字典值';
	editRefDictData.value?.openDialog(row);
};

// 打开复制字典值页面
const openCopyDictData = (row: any) => {
	state.title = '复制字典值';
	var copyRow = JSON.parse(JSON.stringify(row)) as UpdateDictDataInput;
	copyRow.id = 0;
	editRefDictData.value?.openDialog(copyRow);
};

// 删除字典值
const handleDeleteDictData = (row: any) => {
	ElMessageBox.confirm(`确定删除字典值：【${row.value}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysDictDataApi).apiSysDictDataDeletePost({ id: row.id });
			await handleQueryDictData();
			await updateDictSession();
			ElMessage.success('删除成功');
		})
		.catch(() => {});
};

// 表格事件
const gridEventsDictData: VxeGridListeners<SysDictData> = {
	// 只对 pager-config 配置时有效，分页发生改变时会触发该事件
	async pageChange({ pageSize }) {
		state.localPageParamDictData.pageSize = pageSize;
		// Local.set(localPageParamKey, state.localPageParam);
	},
	// 当排序条件发生变化时会触发该事件
	async sortChange({ field, order }) {
		state.localPageParamDictData.defaultSort = { field: field, order: order!, descStr: 'desc' };
		// Local.set(localPageParamKey, state.localPageParam);
	},
	// 双击行事件
	async cellDblclick({ row }) {
		if (auth('sysDictType/update') && state.currentDictTypeRow.sysFlag !== 1) await handleEditDictData(row);
	},
};

// 更新前端字典缓存
const updateDictSession = async () => {
	// if (Session.get('dictList')) {
	// 	const dictList = await useUserInfo().getAllDictList();
	// 	Session.set('dictList', dictList);
	// }
	await useUserInfo().setDictList();
};
</script>

<style lang="scss" scoped>
.full-height {
	display: flex;
	flex-direction: column;
	height: 100%;
}
</style>
