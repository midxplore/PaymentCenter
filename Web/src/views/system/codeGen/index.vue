<template>
	<div class="sys-codeGen-container">
		<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
			<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
				<el-row :gutter="10">
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="业务名" prop="busName">
							<el-input placeholder="业务名" clearable @keyup.enter="handleQuery" v-model="state.queryParams.busName" @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
					<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
						<el-form-item label="数据库表名" prop="tableName">
							<el-input placeholder="数据库表名" clearable @keyup.enter="handleQuery" v-model="state.queryParams.tableName" @keyup.enter.native="handleQuery(true)" />
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>

			<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

			<el-row>
				<el-col>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" :loading="options.loading"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
					</el-button-group>
				</el-col>
			</el-row>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
				<template #toolbar_buttons>
					<el-button type="primary" icon="ele-Plus" @click="handleAdd"> 新增 </el-button>
				</template>
				<template #toolbar_tools> </template>
				<template #empty>
					<el-empty :image-size="200" />
				</template>
				<template #row_entityName="{ row }">
					{{ row.tableList?.[0].entityName }}
				</template>
				<template #row_generateMethod="{ row }">
					<g-sys-dict code="CodeGenMethodEnum" v-model="row.generateMethod" />
				</template>
				<template #row_scene="{ row }">
					<g-sys-dict code="CodeGenSceneEnum" v-model="row.scene" />
				</template>
				<template #row_record="{ row }">
					<ModifyRecord :data="row" />
				</template>
				<template #row_buttons="{ row }">
					<el-button icon="ele-Position" text type="primary" @click="handleGenerate(row)" title="生成" v-reclick="1000">生成</el-button>
					<el-button icon="ele-Camera" text type="primary" @click="handlePreview(row)" title="预览" v-reclick="1000">预览</el-button>
					<el-button icon="ele-Edit" text type="primary" @click="handleEdit(row)" title="编辑" v-reclick="1000">编辑</el-button>
					<el-button icon="ele-Delete" text type="danger" @click="handleDelete(row)" title="删除" v-reclick="1000">删除</el-button>
					<el-button icon="ele-CopyDocument" text type="primary" @click="handleCopy(row)" title="复制" v-reclick="1000">复制</el-button>
				</template>
			</vxe-grid>
		</el-card>

		<EditCodeGenDialog ref="EditCodeGenRef" @handleQuery="handleQuery" />
		<PreviewDialog :title="state.title" ref="previewRef" />
	</div>
</template>

<!-- 代码生成 -->
<script lang="ts" setup name="sysCodeGen">
import { onMounted, reactive, ref, defineAsyncComponent } from 'vue';
import { ElMessageBox, ElMessage, ElNotification } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { downloadByUrl } from '/@/utils/download';
import { Local } from '/@/utils/storage';

import EditCodeGenDialog from './component/editCodeGenDialog.vue';
import { useCodeGenStore } from './component/codeGenStore';
import ModifyRecord from '/@/components/table/modifyRecord.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysCodeGenApi } from '/@/api-services/system/api';
import { SysCodeGen, PageCodeGenInput } from '/@/api-services/system/models';

const PreviewDialog = defineAsyncComponent(() => import('./component/previewDialog.vue'));
const store = useCodeGenStore();
const xGrid = ref<VxeGridInstance>();
const previewRef = ref<InstanceType<typeof PreviewDialog>>();
const EditCodeGenRef = ref<InstanceType<typeof EditCodeGenDialog>>();
const state = reactive({
	dbData: [] as any,
	configId: '',
	tableName: '',
	queryParams: {
		name: undefined,
		code: undefined,
		tableName: undefined,
		busName: undefined,
	},
	localPageParam: {
		pageSize: 20 as number,
		defaultSort: { field: 'id', order: 'asc', descStr: 'desc' },
	},
	visible: false,
	title: '',
	applicationNamespaces: [] as Array<string>,
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysCodeGen';
// 表格参数配置
const options = useVxeTable<SysCodeGen>(
	{
		id: 'sysCodeGen',
		name: '代码生成',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'busName', title: '业务名', align: 'left', minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'scene', title: '模板场景', align: 'left', minWidth: 100, showOverflow: 'tooltip', slots: { default: 'row_scene' } },
			{ field: 'entityName', title: '主表实体', align: 'left', minWidth: 120, showOverflow: 'tooltip', slots: { default: 'row_entityName' } },
			{ field: 'nameSpace', title: '命名空间', align: 'left', showOverflow: 'tooltip' },
			{ field: 'generateMethod', title: '生成方式', align: 'left', showOverflow: 'tooltip', slots: { default: 'row_generateMethod' } },
			{ field: 'authorName', title: '作者姓名', align: 'left', showOverflow: 'tooltip' },
			{ field: 'record', title: '修改记录', width: 100, showOverflow: 'tooltip', slots: { default: 'row_record' } },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 300, showOverflow: true, slots: { default: 'row_buttons' } },
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
		toolbarConfig: { export: false },
	}
);

// 页面初始化
onMounted(async () => {
	state.localPageParam = Local.get(localPageParamKey) || state.localPageParam;
	store.init();
});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageCodeGenInput;
	return getAPI(SysCodeGenApi).apiSysCodeGenPagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.busName = undefined;
	state.queryParams.tableName = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 打开新增页面
const handleAdd = () => {
    EditCodeGenRef.value?.openDialog({
        authorName: 'Admin.NET',
        generateMethod: 200,
        printType: 1,
        menuIcon: 'ele-Menu',
        pagePath: 'main',
        configObj: {},
        nameSpace: state.applicationNamespaces[0],
        generateMenu: false,
        isApiService: false,
        scene: 1000,
    });
};

// 打开编辑页面
const handleEdit = (row: any) => {
	EditCodeGenRef.value?.openDialog(row);
};

// 打开复制页面
const handleCopy = async (row: any) => {
	const data = await store.getDetail(row.id);
	if (data) {
		data.moduleName = undefined;
		data.tableList?.forEach((e) => {
			e.columnList?.forEach((c) => delete c.id);
			delete e.id;
		});
		delete data.id;
	}
	EditCodeGenRef.value?.openDialog(data);
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(`确定删除吗?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysCodeGenApi).apiSysCodeGenDeletePost({ id: row.id });
			await handleQuery();
			ElMessage.success('操作成功');
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<SysCodeGen> = {
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

// 开始生成代码
const handleGenerate = (row: any) => {
	ElMessageBox.confirm(`确定要生成【${row.busName}】吗?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			const res = await getAPI(SysCodeGenApi).apiSysCodeGenGeneratePost(row);
			if (res.data.result != null && res.data.result.url != null) downloadByUrl({ url: res.data.result.url });
			await handleQuery();

			ElNotification({
				title: '提示',
				message: '生成成功，请重启项目以加载最新代码',
				type: 'success',
				position: 'bottom-right',
			});
		})
		.catch(() => {});
};

// 预览代码
const handlePreview = (row: any) => {
	state.title = '预览代码';
	previewRef.value?.openDialog(row);
};
</script>
