<template>
	<div class="sys-file-container">
		<splitpanes class="default-theme">
			<pane size="15" style="display: flex">
				<FileTree ref="fileTreeRef" @node-click="handleNodeChange" />
			</pane>

			<pane size="80" style="display: flex; flex-direction: column">
				<el-card shadow="hover" :body-style="{ padding: '5px 5px 0 5px', display: 'flex', width: '100%', height: '100%', alignItems: 'start' }">
					<el-form :model="state.queryParams" ref="queryForm" :show-message="false" :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
						<el-row :gutter="10">
							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item label="文件名称" prop="fileName">
									<el-input v-model="state.queryParams.fileName" placeholder="文件名称" clearable @keyup.enter.native="handleQuery(true)" />
								</el-form-item>
							</el-col>
							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item label="开始时间" prop="name">
									<el-date-picker v-model="state.queryParams.startTime" type="datetime" placeholder="开始时间" format="YYYY-MM-DD HH:mm:ss" value-format="YYYY-MM-DD HH:mm:ss" class="w100" />
								</el-form-item>
							</el-col>
							<el-col class="mb5" :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item label="结束时间" prop="code">
									<el-date-picker v-model="state.queryParams.endTime" type="datetime" placeholder="结束时间" format="YYYY-MM-DD HH:mm:ss" value-format="YYYY-MM-DD HH:mm:ss" class="w100" />
								</el-form-item>
							</el-col>
						</el-row>
					</el-form>

					<el-divider style="height: calc(100% - 5px); margin: 0 10px" direction="vertical" />

					<el-row>
						<el-col>
							<el-button-group>
								<el-button type="primary" icon="ele-Search" @click="handleQuery(true)" v-auth="'sysFile/page'" :loading="options.loading"> 查询 </el-button>
								<el-button icon="ele-Refresh" @click="resetQuery" :loading="options.loading"> 重置 </el-button>
							</el-button-group>
						</el-col>
					</el-row>
				</el-card>

				<el-card class="full-table" shadow="hover" style="margin-top: 5px">
					<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options" v-on="gridEvents">
						<template #toolbar_buttons>
							<el-button type="primary" icon="ele-UploadFilled" @click="showUpload" v-auth="'sysFile/uploadFile'"> 上传 </el-button>
						</template>
						<template #toolbar_tools> </template>
						<template #empty>
							<el-empty :image-size="200" />
						</template>
						<template #row_suffix="{ row }">
							<!-- <el-tag round>{{ row.suffix }}</el-tag> -->
							<el-tag v-if="row.suffix === '.png' || row.suffix === '.jpg' || row.suffix === '.jpeg' || row.suffix === '.gif' || row.suffix === '.bmp'" type="success">{{ row.suffix }}</el-tag>
							<el-tag v-else type="danger">{{ row.suffix }}</el-tag>
						</template>
						<template #row_isPublic="{ row }">
							<el-tag v-if="row.isPublic === true" type="success">是</el-tag>
							<el-tag v-else type="danger">否</el-tag>
						</template>
						<template #row_url="{ row }">
							<el-image
								style="width: 60px; height: 60px"
								:src="fetchFileUrl(row)"
								alt="无法预览"
								lazy
								hide-on-click-modal
								:preview-src-list="[fetchFileUrl(row)]"
								:initial-index="0"
								fit="scale-down"
								preview-teleported
							></el-image>
						</template>
						<template #row_buttons="{ row }">
							<el-tooltip content="编辑" placement="top">
								<el-button icon="ele-Edit" size="small" text type="primary" @click="handleEdit(row)" v-auth="'sysFile/update'" />
							</el-tooltip>
							<el-tooltip content="删除" placement="top">
								<el-button icon="ele-Delete" size="small" text type="danger" @click="handleDelete(row)" v-auth="'sysFile/delete'" />
							</el-tooltip>
							<el-tooltip content="预览" placement="top">
								<el-button icon="ele-View" size="small" text type="primary" @click="showPreviewDialog(row)" v-auth="'sysFile/preview'" />
							</el-tooltip>
							<el-tooltip content="下载" placement="top">
								<el-button icon="ele-Download" size="small" text type="primary" @click="handleDownload(row)" v-auth="'sysFile/downloadFile'" />
							</el-tooltip>
						</template>
					</vxe-grid>
				</el-card>

				<el-dialog v-model="state.visible" :lock-scroll="false" draggable overflow destroy-on-close width="400px">
					<template #header>
						<div style="color: #fff">
							<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-UploadFilled /> </el-icon>
							<span> 上传文件 </span>
						</div>
					</template>
					<div v-loading="state.loading">
						<el-select v-model="state.fileType" placeholder="请选择文件类型" style="margin-bottom: 10px">
							<el-option label="相关文件" value="相关文件" />
							<el-option label="归档文件" value="归档文件" />
						</el-select>

						<el-upload
							ref="uploadRef"
							drag
							:auto-upload="false"
							:file-list="state.fileList"
							action
							:on-change="handleChange"
							:on-remove="handleChange"
							accept=".jpg,.png,.bmp,.gif,.txt,.xml,.pdf,.xlsx,.docx"
						>
							<el-icon class="el-icon--upload">
								<ele-UploadFilled />
							</el-icon>
							<div class="el-upload__text">将文件拖到此处，或<em>点击上传</em></div>
							<template #tip>
								<div class="el-upload__tip">请上传大小不超过 10MB 的文件</div>
							</template>
						</el-upload>
					</div>
					<template #footer>
						<span class="dialog-footer">
							<el-button icon="ele-CircleCloseFilled" @click="state.visible = false">取消</el-button>
							<el-button icon="ele-CircleCheckFilled" type="primary" @click="handleUpload" :loading="state.loading">确定</el-button>
						</span>
					</template>
				</el-dialog>
			</pane>
		</splitpanes>

		<el-drawer :title="state.fileName" v-model="state.docxVisible" size="70%" destroy-on-close>
			<vue-office-docx v-loading="state.loading" :src="state.docxUrl" style="height: calc(100vh - 37px)" @rendered="handleRendered" @error="handleError" />
		</el-drawer>
		<el-drawer :title="state.fileName" v-model="state.xlsxVisible" size="70%" destroy-on-close>
			<vue-office-excel v-loading="state.loading" :src="state.excelUrl" style="height: calc(100vh - 37px)" @rendered="handleRendered" @error="handleError" />
		</el-drawer>
		<el-drawer :title="state.fileName" v-model="state.pdfVisible" size="70%" destroy-on-close>
			<vue-office-pdf v-loading="state.loading" :src="state.pdfUrl" style="height: calc(100vh - 37px)" @rendered="handleRendered" @error="handleError" />
		</el-drawer>
		<el-image-viewer v-if="state.showViewer" :url-list="state.previewList" :hideOnClickModal="true" @close="state.showViewer = false"></el-image-viewer>
		<EditFile ref="editRef" title="编辑文件" @handleQuery="handleQuery" />
	</div>
</template>

<!-- 文件管理 -->
<script lang="ts" setup name="sysFile">
import { onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage, UploadInstance } from 'element-plus';
import { VxeGridInstance, VxeGridListeners, VxeGridPropTypes } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { Local } from '/@/utils/storage';
import { downloadByUrl } from '/@/utils/download';
import { auth } from '/@/utils/authFunction';

import { Splitpanes, Pane } from 'splitpanes';
import 'splitpanes/dist/splitpanes.css';

import VueOfficeDocx from '@vue-office/docx';
import VueOfficeExcel from '@vue-office/excel';
import VueOfficePdf from '@vue-office/pdf';
import '@vue-office/docx/lib/index.css';
import '@vue-office/excel/lib/index.css';

import FileTree from './component/fileTree.vue';
import EditFile from './component/editFile.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysFileApi } from '/@/api-services/system/api';
import { SysFile, PageFileInput } from '/@/api-services/system/models';

const xGrid = ref<VxeGridInstance>();
// const baseUrl = window.__env__.VITE_API_URL;
const uploadRef = ref<UploadInstance>();
const editRef = ref<InstanceType<typeof EditFile>>();
const fileTreeRef = ref<InstanceType<typeof FileTree>>();

const state = reactive({
	loading: false,
	queryParams: {
		fileName: undefined,
		filePath: undefined,
		startTime: undefined,
		endTime: undefined,
	},
	localPageParam: {
		pageSize: 50 as number,
		defaultSort: { field: 'orderNo', order: 'asc', descStr: 'desc' },
	},
	visible: false,
	diaglogEditFile: false,
	fileList: [] as any,
	docxVisible: false,
	xlsxVisible: false,
	pdfVisible: false,
	showViewer: false,
	docxUrl: '',
	excelUrl: '',
	pdfUrl: '',
	fileName: '',
	fileType: '相关文件',
	previewList: [] as string[],
});

// 本地存储参数
const localPageParamKey = 'localPageParam:sysFile';
// 表格参数配置
const options = useVxeTable<SysFile>(
	{
		id: 'sysFile',
		name: '文件信息',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'fileName', title: '名称', minWidth: 220, headerAlign: 'center', align: 'left', showOverflow: 'tooltip' },
			{ field: 'url', title: '预览', minWidth: 80, slots: { default: 'row_url' } },
			{ field: 'sizeKb', title: '大小(KB)', minWidth: 80, showOverflow: 'tooltip' },
			{ field: 'suffix', title: '后缀', minWidth: 80, showOverflow: 'tooltip', slots: { default: 'row_suffix' } },
			{ field: 'contentType', title: 'MIME类型', minWidth: 220, headerAlign: 'center', align: 'left', showOverflow: 'tooltip' },
			{ field: 'id', title: '存储标识', minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'bucketName', title: '存储位置', minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'isPublic', title: '是否公开', minWidth: 80, showOverflow: 'tooltip', slots: { default: 'row_isPublic' } },
			{ field: 'createTime', title: '创建时间', minWidth: 150, showOverflow: 'tooltip' },
			{ field: 'fileType', title: '类别', minWidth: 140, showOverflow: 'tooltip' },
			{ field: 'fileAlias', title: '别名', minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'dataId', title: '业务数据ID', minWidth: 120, showOverflow: 'tooltip' },
			// { field: 'userName', title: '上传者', minWidth: 150, showOverflow: 'tooltip', sortable: true },
			{ field: 'remark', title: '备注', minWidth: 200, showOverflow: 'tooltip' },
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
		// 行设置
		cellConfig: { height: 80 },
	}
);

// 页面初始化
onMounted(() => {});

// 查询api
const handleQueryApi = async (page: VxeGridPropTypes.ProxyAjaxQueryPageParams, sort: VxeGridPropTypes.ProxyAjaxQuerySortCheckedParams) => {
	const params = Object.assign(state.queryParams, { page: page.currentPage, pageSize: page.pageSize, field: sort.field, order: sort.order, descStr: 'desc' }) as PageFileInput;
	return getAPI(SysFileApi).apiSysFilePagePost(params);
};

// 查询操作
const handleQuery = async (reset = false) => {
	options.loading = true;
	reset ? await xGrid.value?.commitProxy('reload') : await xGrid.value?.commitProxy('query');
	options.loading = false;
};

// 重置操作
const resetQuery = async () => {
	state.queryParams.fileName = undefined;
	state.queryParams.filePath = undefined;
	state.queryParams.startTime = undefined;
	state.queryParams.endTime = undefined;
	await xGrid.value?.commitProxy('reload');
};

// 打开上传页面
const showUpload = () => {
	state.fileList = [];
	state.visible = true;
};

// 通过onChanne方法获得文件列表
const handleChange = (file: any, fileList: []) => {
	state.fileList = fileList;
};

// 上传
const handleUpload = async () => {
	if (state.fileList.length < 1) return;
	// // 单文件上传
	// await getAPI(SysFileApi).apiSysFileUploadFilePostForm(state.fileList[0].raw, state.fileType);
	// 多文件上传
	var files = state.fileList.map((file: any) => file.raw);
	state.loading = true;
	await getAPI(SysFileApi)
		.apiSysFileUploadFilesPostForm(files, state.fileType)
		.finally(() => (state.loading = false));
	handleQuery();
	ElMessage.success('上传成功');
	state.visible = false;
};

// 下载
const handleDownload = async (row: any) => {
	// var res = await getAPI(SysFileApi).apiSysFileDownloadFilePost({ id: row.id });
	// downloadStreamFile(res, row.fileName);

	var fileUrl = fetchFileUrl(row);
	downloadByUrl({ url: fileUrl, fileName: row.fileName });
};

// 删除
const handleDelete = (row: any) => {
	ElMessageBox.confirm(`确定删除文件：【${row.fileName}】?`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await getAPI(SysFileApi).apiSysFileDeletePost({ id: row.id });
			handleQuery();
			ElMessage.success('删除成功');
		})
		.catch(() => {});
};

// 表格事件
const gridEvents: VxeGridListeners<SysFile> = {
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
		if (auth('sysFile/update')) await handleEdit(row);
	},
};

// 树组件点击
const handleNodeChange = async (node: any) => {
	state.queryParams.fileName = undefined;
	state.queryParams.filePath = node.path;
	state.queryParams.startTime = undefined;
	state.queryParams.endTime = undefined;
	await handleQuery();
};

// 打开Pdf预览页面
const showPreviewDialog = async (row: any) => {
	state.loading = true;
	if (row.suffix == '.pdf') {
		state.fileName = `【${row.fileName}${row.suffix}】`;
		state.pdfUrl = fetchFileUrl(row);
		state.pdfVisible = true;
	} else if (row.suffix == '.docx') {
		state.fileName = `【${row.fileName}${row.suffix}】`;
		state.docxUrl = fetchFileUrl(row);
		state.docxVisible = true;
	} else if (row.suffix == '.xlsx') {
		state.fileName = `【${row.fileName}${row.suffix}】`;
		state.excelUrl = fetchFileUrl(row);
		state.xlsxVisible = true;
	} else if (['.jpg', '.png', '.jpeg', '.bmp'].findIndex((e) => e == row.suffix) > -1) {
		state.previewList = [fetchFileUrl(row)];
		state.showViewer = true;
	} else {
		ElMessage.error('此文件格式不支持预览');
	}
	state.loading = false;
};

// 获取文件地址
const fetchFileUrl = (row: SysFile): string => {
	if (row.bucketName == 'Local') {
		return `/${row.filePath}/${row.id}${row.suffix}`;
	} else {
		return row.url!;
	}
};

// 打开编辑页面
const handleEdit = (row: any) => {
	editRef.value?.openDialog(row);
};

// 文件渲染完成
const handleRendered = () => {
	state.loading = false;
};

// 文件渲染失败
const handleError = () => {
	ElMessage.error('预览失败');
	state.loading = false;
	state.docxVisible = false;
	state.xlsxVisible = false;
	state.pdfVisible = false;
};
</script>
