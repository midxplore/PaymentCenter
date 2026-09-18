<template>
	<div>
		<el-card class="full-table" shadow="hover" style="margin-top: 5px">
			<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options">
				<template #toolbar_buttons>
					<el-form :inlineMessage="true" label-width="auto" style="flex: 1 1 0%">
						<el-row :gutter="10">
							<el-col :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-form-item label="库名" prop="configId">
									<el-select v-model="state.configId" placeholder="库名" filterable>
										<el-option v-for="item in state.dbData" :key="item.configId" :label="`${item.dbName}(${item.configId})`" :value="item.configId" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="8" :lg="6" :xl="6">
								<el-button type="primary" icon="ele-Plus" @click="handleAdd" v-auth="'dbBackup/add'" :loading="state.loading"> 新增 </el-button>
							</el-col>
						</el-row>
					</el-form>
				</template>
				<template #row_createTime="{ row }">
					<el-tooltip :content="row.createTime" placement="top">
						<span>{{ formatPast(row.createTime) }}</span>
					</el-tooltip>
				</template>
				<template #row_size="{ row }">
					<span>{{ (row.size / 1024 / 1024).toFixed(2) }} MB</span>
				</template>
				<template #row_buttons="{ row }">
					<el-button icon="ele-Delete" text type="danger" v-auth="'dbBackup/delete'" @click="handleDelete(row)"> 删除 </el-button>
					<el-button icon="ele-Files" text type="primary" @click="handleCompress(row)" v-if="!row.fileName.endsWith('.zip')"> 压缩 </el-button>
					<el-button icon="ele-Download" text type="primary" @click="handleDownload(row)"> 下载 </el-button>
				</template>
			</vxe-grid>
		</el-card>
	</div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { VxeGridInstance } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { formatPast } from '/@/utils/formatTime';

import { getAPI } from '/@/utils/axios-utils';
import { SysDatabaseApi, SysDbBackupApi } from '/@/api-services/system/api';

const xGrid = ref<VxeGridInstance>();

const state = reactive({
	loading: false,
	dbData: [] as any,
	configId: '',
});

// 表格参数配置
const options = useVxeTable(
	{
		id: 'dbBackup',
		name: '备份信息',
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ field: 'seq', type: 'seq', title: '序号', width: 60, fixed: 'left' },
			{ field: 'fileName', title: '文件名称', minWidth: 200, showOverflow: 'tooltip' },
			{ field: 'size', title: '文件大小', width: 150, showOverflow: 'tooltip', slots: { default: 'row_size' } },
			{ field: 'createTime', title: '备份时间', width: 200, showOverflow: 'tooltip', slots: { default: 'row_createTime' } },
			{ field: 'buttons', title: '操作', fixed: 'right', width: 200, showOverflow: true, slots: { default: 'row_buttons' } },
		],
	},
	{
		// 代理配置
		proxyConfig: { autoLoad: true, ajax: { query: () => handleQueryApi() } },
		// 分页配置
		pagerConfig: { enabled: false },
		// 工具栏配置
		toolbarConfig: { enabled: true },
	}
);

// 页面初始化
onMounted(async () => {
	options.loading = true;
	let res = await getAPI(SysDatabaseApi).apiSysDatabaseListGet();
	state.dbData = res.data.result;

	options.loading = false;
});

// 查询api
const handleQueryApi = async () => {
	return await getAPI(SysDbBackupApi).apiSysDbBackupListGet();
};

// 新增备份
const handleAdd = async () => {
	try {
		if (!state.configId) {
			ElMessage.warning('请选择库名');
			return;
		}

		await ElMessageBox.confirm('确定要新增备份吗？', '提示', {
			confirmButtonText: '确定',
			cancelButtonText: '取消',
			type: 'warning',
		});

		state.loading = true;
		await getAPI(SysDbBackupApi).apiSysDbBackupAddPost(state.configId, { timeout: 0 /**永不超时 */ });
		await xGrid.value?.commitProxy('reload');
	} finally {
		state.loading = false;
	}
};

// 删除备份
const handleDelete = async (row: any) => {
	try {
		await ElMessageBox.confirm(`确定要删除备份：【${row.fileName}】吗？`, '提示', {
			confirmButtonText: '确定',
			cancelButtonText: '取消',
			type: 'warning',
		});

		state.loading = true;
		await getAPI(SysDbBackupApi).apiSysDbBackupDeletePost(row.fileName);
		await xGrid.value?.commitProxy('reload');
	} finally {
		state.loading = false;
	}
};

// 下载备份
const handleDownload = (row: any) => {
	const baseUrl = import.meta.env.VITE_API_URL;
	let url = `${baseUrl}/api/sysDbBackup/download/${row.fileName}`;

	const tempLink = document.createElement('a');
	tempLink.style.display = 'none';
	tempLink.href = url;
	tempLink.setAttribute('download', row.fileName);
	if (typeof tempLink.download === 'undefined') {
		tempLink.setAttribute('target', '_blank');
	}
	document.body.appendChild(tempLink);
	tempLink.click();
	document.body.removeChild(tempLink);
};

// 压缩备份
const handleCompress = async (row: any) => {
	try {
		await ElMessageBox.confirm(`确定要压缩备份：【${row.fileName}】吗？`, '提示', {
			confirmButtonText: '确定',
			cancelButtonText: '取消',
			type: 'info',
		});

		state.loading = true;
		await getAPI(SysDbBackupApi).apiSysDbBackupCompressPost(row.fileName, { timeout: 0 /**永不超时 */ });
		await xGrid.value?.commitProxy('reload');
	} finally {
		state.loading = false;
	}
};
</script>

<style scoped></style>
