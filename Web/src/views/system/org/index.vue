<template>
	<div class="sys-org-container">
		<splitpanes class="default-theme">
			<pane size="15" style="display: flex">
				<OrgTree ref="orgTreeRef" @node-click="handleNodeChange" />
			</pane>
			<pane size="85" style="display: flex; flex-direction: column">
				<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
					<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
						<el-row :gutter="10">
							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item :label="$t('message.list.organizationName')">
									<el-input v-model="state.queryParams.name" :placeholder="$t('message.list.organizationName')" clearable @keyup.enter.native="handleQuery" />
								</el-form-item>
							</el-col>
							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item :label="$t('message.list.organizationCode')">
									<el-input v-model="state.queryParams.code" :placeholder="$t('message.list.organizationCode')" clearable @keyup.enter.native="handleQuery" />
								</el-form-item>
							</el-col>
							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item :label="$t('message.list.organizationType')">
									<el-select v-model="state.queryParams.type" filterable clearable class="w100" @clear="state.queryParams.type = undefined">
										<el-option v-for="item in state.orgTypeList" :key="item.id" :label="item.label" :value="item.value" />
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

				<el-card class="full-table" shadow="hover" style="margin-top: 5px; flex: 1">
					<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
						<template #toolbar_buttons>
							<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'sysOrg/add'"> {{ $t('message.list.add') }} </el-button>
							<el-button-group style="padding-left: 12px">
								<el-button icon="ele-Expand" @click="handleExpand"> {{ $t('message.list.allExpand') }} </el-button>
								<el-button icon="ele-Fold" @click="handleFold"> {{ $t('message.list.allFold') }} </el-button>
							</el-button-group>
						</template>
						<template #toolbar_tools> </template>
						<template #empty>
							<el-empty :image-size="200" />
						</template>
						<template #row_status="{ row }">
							<el-tag v-if="row.status === 1" type="success">{{ $t('message.list.enable') }}</el-tag>
							<el-tag v-else type="danger">{{ $t('message.list.disable') }}</el-tag>
						</template>
						<template #row_record="{ row }">
							<ModifyRecord :data="row" />
						</template>
						<template #row_buttons="{ row }">
							<el-button icon="ele-Edit" text type="primary" @click="handleEdit(row)" v-auth="'sysOrg/update'"> {{ $t('message.list.edit') }} </el-button>
							<el-button icon="ele-Delete" text type="danger" @click="handleDelete(row)" v-auth="'sysOrg/delete'"> {{ $t('message.list.delete') }} </el-button>
							<el-button icon="ele-CopyDocument" text type="primary" @click="openCopyOrg(row)" v-auth="'sysOrg/add'"> {{ $t('message.list.copy') }} </el-button>
						</template>
					</vxe-grid>
				</el-card>
			</pane>
		</splitpanes>

		<EditOrg ref="editOrgRef" :title="state.title" :orgData="state.treeData" @reload="handleQuery" />
	</div>
</template>

<!-- 组织架构 -->
<script lang="ts" setup name="sysOrg">
import { onMounted, reactive, ref, nextTick } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { VxeGridInstance, VxeGridListeners } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Splitpanes, Pane } from 'splitpanes';
import 'splitpanes/dist/splitpanes.css';
import { useI18n } from 'vue-i18n';
import { auth } from '/@/utils/authFunction';

import OrgTree from '/@/views/system/org/component/orgTree.vue';
import EditOrg from '/@/views/system/org/component/editOrg.vue';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysDictDataApi, SysOrgApi } from '/@/api-services/system/api';
import { SysOrg, UpdateOrgInput } from '/@/api-services/system/models';

const xGrid = ref<VxeGridInstance>();
const editOrgRef = ref<InstanceType<typeof EditOrg>>();
const orgTreeRef = ref<InstanceType<typeof OrgTree>>();
const state = reactive({
	treeData: [] as Array<SysOrg>, // 机构树所有数据
	queryParams: {
		id: 0,
		name: undefined,
		code: undefined,
		type: undefined,
	},
	title: '',
	orgTypeList: [] as any,
});

const i18n = useI18n();

// 表格参数配置
const options = useVxeTable<SysOrg>(
	{
		id: 'sysOrg',
		name: i18n.t('message.list.orgStructure'),
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: i18n.t('message.list.seq'), width: 60, fixed: 'left' },
			{ field: 'name', title: i18n.t('message.list.organizationName'), minWidth: 200, showOverflow: 'tooltip', treeNode: true, align: 'left', headerAlign: 'center' },
			{ field: 'code', title: i18n.t('message.list.organizationCode'), minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'level', title: i18n.t('message.list.level'), minWidth: 70, showOverflow: 'tooltip' },
			{
				field: 'type',
				title: i18n.t('message.list.organizationType'),
				minWidth: 80,
				formatter: ({ cellValue }: any) => state.orgTypeList.find((u: any) => u.value == cellValue)?.label,
				showOverflow: 'tooltip',
			},
			{ field: 'orderNo', title: i18n.t('message.list.orderNo'), width: 80, showOverflow: 'tooltip' },
			{ field: 'status', title: i18n.t('message.list.status'), width: 80, showOverflow: 'tooltip', slots: { default: 'row_status' } },
			{ field: 'record', title: i18n.t('message.list.record'), width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: i18n.t('message.list.operation'), fixed: 'right', width: 200, showOverflow: true, slots: { default: 'row_buttons' } },
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
		treeConfig: { expandAll: false },
	}
);

// 页面初始化
onMounted(async () => {
	let resDicData = await getAPI(SysDictDataApi).apiSysDictDataDataListCodeGet('org_type');
	state.orgTypeList = resDicData.data.result;
	// 展开表格所有数据，数据量大时请勿开启
	nextTick(async () => {
		if (state.treeData?.length < 100) await xGrid.value?.setAllTreeExpand(true);
	});
});

// 查询api
const handleQueryApi = async () => {
	const params = Object.assign(state.queryParams);
	return getAPI(SysOrgApi).apiSysOrgListGet(params.id, params.name, params.code, params.type);
};

// 查询操作
const handleQuery = async (updateTree: boolean = false) => {
	options.loading = true;
	var res = await handleQueryApi();
	xGrid.value?.loadData(res.data.result ?? []);
	options.loading = false;
	// 是否更新左侧机构列表树
	if (updateTree == true) {
		const data = await orgTreeRef.value?.fetchTreeData();
		state.treeData = data ?? [];
	}
	// 若无选择节点并且查询条件为空时，更新编辑页面机构列表树
	if (state.queryParams.id == 0 && state.queryParams.name == undefined && state.queryParams.code == undefined && state.queryParams.type == undefined && updateTree == false)
		state.treeData = res.data.result ?? [];
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.id = 0;
	state.queryParams.name = undefined;
	state.queryParams.code = undefined;
	state.queryParams.type = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 打开新增页面
const handleAdd = () => {
	state.title = i18n.t('message.list.addOrg');
	editOrgRef.value?.openDialog({ status: 1, orderNo: 100 });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	state.title = i18n.t('message.list.editOrg');
	editOrgRef.value?.openDialog(row);
};

// 打开复制页面
const openCopyOrg = (row: any) => {
	state.title = i18n.t('message.list.copyOrg');
	var copyRow = JSON.parse(JSON.stringify(row)) as UpdateOrgInput;
	copyRow.id = 0;
	copyRow.name = '';
	editOrgRef.value?.openDialog(copyRow);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(i18n.t('message.list.confirmDeleteOrg', { name: row.name }), i18n.t('message.list.hint'), {
		confirmButtonText: i18n.t('message.list.confirmButtonText'),
		cancelButtonText: i18n.t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			getAPI(SysOrgApi)
				.apiSysOrgDeletePost({ id: row.id })
				.then(async (res: any) => {
					if (res.data.code == 200) {
						ElMessage.success(i18n.t('message.list.successDelete'));
						await handleQuery(true);
					}
				});
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<SysOrg> = {
	// 只对 proxy-config.ajax.query 配置时有效，当手动点击查询时会触发该事件
	async proxyQuery() {
		state.treeData = xGrid.value?.getTableData().tableData ?? [];
	},
	// 双击行事件
	async cellDblclick({ row }) {
		if (auth('sysOrg/update')) await handleEdit(row);
	},
};

// 树组件点击
const handleNodeChange = async (node: any) => {
	state.queryParams.id = node.id;
	state.queryParams.name = undefined;
	state.queryParams.code = undefined;
	state.queryParams.type = undefined;
	orgTreeRef?.value?.setCurrentKey();
	await handleQuery();
};

// 全部展开
const handleExpand = () => {
	xGrid.value?.setAllTreeExpand(true);
};

// 全部折叠
const handleFold = () => {
	xGrid.value?.clearTreeExpand();
};
</script>
