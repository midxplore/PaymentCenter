<template>
	<div class="sys-dbTable-container">
		<vxe-modal v-model="state.visible" title="增加表" show-footer show-confirm-button show-cancel-button fullscreen show-zoom resize width="100vw" height="100vh" @close="cancel">
			<template #default>
				<el-divider content-position="left" style="margin: 10px 0 18px 0">数据表信息</el-divider>
				<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto">
					<el-row>
						<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
							<el-form-item label="表名称" prop="tableName" :rules="[{ required: true, message: '名称不能为空', trigger: 'blur' }]">
								<el-input v-model.lazy.trim="state.ruleForm.tableName" placeholder="表名称" clearable />
							</el-form-item>
						</el-col>
						<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
							<el-form-item label="描述" prop="description" :rules="[{ required: true, message: '描述不能为空', trigger: 'blur' }]">
								<el-input v-model.lazy.trim="state.ruleForm.description" placeholder="描述" clearable type="textarea" />
							</el-form-item>
						</el-col>
					</el-row>
				</el-form>
				<el-divider content-position="left" style="margin: 10px 0 18px 0">数据列信息</el-divider>
				<div style="height: calc(100vh - 300px)">
					<vxe-grid ref="xGrid" class="xGrid-table-style" v-bind="options">
						<template #toolbar_buttons>
							<el-button icon="ele-Plus" @click="addPrimaryColumn">新增主键字段</el-button>
							<el-button icon="ele-Plus" @click="addColumn">新增普通字段</el-button>
							<el-button icon="ele-Plus" @click="addTenantColumn">新增租户字段</el-button>
							<el-button icon="ele-Plus" @click="addBaseColumn">新增基础字段</el-button>
							<el-button icon="ele-Delete" type="danger" @click="deleteSelected">删除选中</el-button>
						</template>
						<template #toolbar_tools> </template>
						<template #empty>
							<el-empty :image-size="200" />
						</template>
						<template #drag_default="{}">
							<span class="drag-btn">
								<i class="fa fa-arrows"></i>
							</span>
						</template>
						<template #isPrimarykey="{ row }">
							<vxe-switch readonly v-model="row.isPrimarykey" open-label="是" close-label="否" :openValue="1" :closeValue="0"></vxe-switch>
						</template>
						<template #isIdentity="{ row }">
							<vxe-switch readonly v-model="row.isIdentity" open-label="是" close-label="否" :openValue="1" :closeValue="0"></vxe-switch>
						</template>
						<template #isNullable="{ row }">
							<vxe-switch readonly v-model="row.isNullable" open-label="是" close-label="否" :openValue="1" :closeValue="0"></vxe-switch>
						</template>
						<template #operate="{ row }">
							<vxe-button mode="text" status="error" icon="vxe-icon-delete" @click="deleteRow(row)" />
						</template>
					</vxe-grid>
				</div>
			</template>
			<template #footer>
				<vxe-button icon="vxe-icon-error-circle-fill" @click="cancel">取 消</vxe-button>
				<vxe-button status="primary" icon="vxe-icon-success-circle-fill" @click="submit">确 定</vxe-button>
			</template>
		</vxe-modal>
	</div>
</template>

<script lang="tsx" setup name="sysAddTable">
import { nextTick, reactive, ref } from 'vue';
import { ElMessage, dayjs } from 'element-plus';
import { VxeGridInstance, VxeGridProps } from 'vxe-table';
import Sortable from 'sortablejs';
import { dataTypeList } from '../database';

import { getAPI } from '/@/utils/axios-utils';
import { SysDatabaseApi } from '/@/api-services/system/api';
import { UpdateDbTableInput } from '/@/api-services/system/models';

const xGrid = ref<VxeGridInstance>();
let initTime: any;
const emits = defineEmits(['addTableSubmitted']);
const ruleFormRef = ref();
const state = reactive({
	visible: false,
	ruleForm: {} as UpdateDbTableInput,
	sortable: undefined as any,
});

// 表格参数配置
const options = reactive<VxeGridProps>({
	id: 'sysAddTable',
	height: 'auto',
	autoResize: true,
	size: 'mini',
	loading: false,
	align: 'center', // 自动监听父元素的变化去重新计算表格（对于父元素可能存在动态变化、显示隐藏的容器中、列宽异常等场景中的可能会用到）
	data: [] as Array<any>,
	rowConfig: { useKey: true },
	mouseConfig: {
		selected: true,
	},
	keyboardConfig: {
		isArrow: true,
		isDel: true,
		isEnter: true,
		isTab: true,
		isEdit: true,
	},
	seqConfig: { seqMethod: ({ row }) => row.orderNo },
	columns: [
		{
			field: 'drag',
			width: 80,
			slots: {
				default: 'drag_default',
			},
		},
		{ field: 'seq', type: 'seq', title: '序号', width: 60 },
		{ field: 'checkbox', type: 'checkbox', width: 40 },
		{
			field: 'dbColumnName',
			title: '字段名',
			minWidth: 160,
			showOverflow: 'tooltip',
			editRender: { name: '$input', props: { clearable: true, placeholder: '请输入字段名' } },
		},
		{
			field: 'columnDescription',
			title: '描述',
			minWidth: 220,
			showOverflow: 'tooltip',
			editRender: { name: '$input', props: { clearable: true, placeholder: '请输入描述' } },
		},
		{
			field: 'isPrimarykey',
			title: '主键',
			minWidth: 100,
			editRender: { name: '$switch', props: { openValue: 1, closeValue: 0, openLabel: '是', closeLabel: '否', immediate: true } },
			slots: {
				default: 'isPrimarykey',
			},
		},
		{
			field: 'isIdentity',
			title: '自增',
			minWidth: 100,
			editRender: { name: '$switch', props: { openValue: 1, closeValue: 0, openLabel: '是', closeLabel: '否' } },
			slots: {
				default: 'isIdentity',
			},
		},
		{
			field: 'dataType',
			title: '类型',
			minWidth: 100,
			editRender: {
				name: '$select',
				options: dataTypeList,
				optionProps: { value: 'value', label: 'value' },
				props: { optionProps: { value: 'value', label: 'value' }, placeholder: '请选择类型', popupClassName: 'zIndex2023' },
			},
		},
		{
			field: 'isNullable',
			title: '可空',
			minWidth: 100,
			editRender: { name: '$switch', props: { openValue: 1, closeValue: 0, openLabel: '是', closeLabel: '否' } },
			slots: {
				default: 'isNullable',
			},
		},
		{
			field: 'length',
			title: '长度',
			minWidth: 100,
			showOverflow: 'tooltip',
			editRender: { name: '$input', props: { type: 'integer', clearable: true, placeholder: '请输入长度' } },
		},
		{
			field: 'decimalDigits',
			title: '小数位',
			minWidth: 100,
			showOverflow: 'tooltip',
			editRender: { name: '$input', props: { type: 'integer', clearable: true, placeholder: '请输入小数位' } },
		},
		{
			field: 'defaultValue',
			title: '默认值',
			minWidth: 100,
			showOverflow: 'tooltip',
			editRender: { name: '$input', props: { clearable: true, placeholder: '请输入默认值' } },
		},
		{
			field: '操作',
			title: '操作',
			width: 80,
			showOverflow: true,
			fixed: 'right',
			slots: {
				default: 'operate',
			},
		},
	],
	editRules: {
		dbColumnName: [{ required: true, message: '字段名必填' }],
		dataType: [{ required: true, message: '类型必填' }],
	},
	toolbarConfig: {
		size: 'small',
		slots: { buttons: 'toolbar_buttons', tools: 'toolbar_tools' },
		refresh: false,
		export: true,
		print: true,
		zoom: true,
		custom: false,
	},
	checkboxConfig: { range: true },
	sortConfig: {
		trigger: 'cell',
		remote: false,
	},
	editConfig: { trigger: 'click', mode: 'row', showStatus: true },
	exportConfig: {
		remote: false, // 设置使用服务端导出
		filename: `数据列信息导出_${dayjs().format('YYMMDDHHmmss')}`,
		exportMethod: ({ options }) => handleExport(options), // 服务器导出方法
	},
	printConfig: { sheetName: '' },
	proxyConfig: {
		props: {
			list: 'data.result.items', // 不分页时
			// result: 'data.result.items', // 分页时
			total: 'data.result.total',
			message: 'data.message',
		},
		ajax: {
			query: () => Promise.resolve(), // 不加会报错
		},
	},
});

// 打开弹窗
const openDialog = (row: any) => {
	state.ruleForm = row;
	state.visible = true;
	ruleFormRef.value?.resetFields();
	nextTick(() => {
		rowDrop();
	});
};

const rowDrop = () => {
	const el = document.querySelector('.xGrid-table-style .vxe-table--body tbody') as HTMLElement;
	state.sortable = Sortable.create(el, {
		animation: 300,
		handle: '.drag-btn',
		onEnd: (sortableEvent: any) => {
			const fullData = xGrid.value?.getTableData().fullData;
			const newIndex = sortableEvent.newIndex as number;
			const oldIndex = sortableEvent.oldIndex as number;
			// 往前移动
			if (oldIndex > newIndex) {
				const moveRow = fullData?.find((e) => e.orderNo == oldIndex + 1);
				for (let i = oldIndex; i > newIndex; i--) {
					const row = fullData?.find((e) => e.orderNo == i);
					row.orderNo += 1;
				}
				moveRow.orderNo = newIndex + 1;
			} else {
				// 往后移动
				const moveRow = fullData?.find((e) => e.orderNo == oldIndex + 1);
				for (let i = oldIndex; i < newIndex; i++) {
					const row = fullData?.find((e) => e.orderNo == i + 2);
					row.orderNo -= 1;
				}
				moveRow.orderNo = newIndex + 1;
			}
			xGrid.value?.updateData();
		},
	});
};

// 关闭弹窗
const closeDialog = () => {
	emits('addTableSubmitted', state.ruleForm.tableName ?? '');
	cancel();
};

// 取消
const cancel = () => {
	xGrid.value?.loadData([]);
	state.visible = false;
	clearTimeout(initTime);
	if (state.sortable) {
		state.sortable.destroy();
	}
};

// 导出日志(服务端导出)
const handleExport = async (opts: any) => {
	console.log(opts);
	options.loading = true;
	// var res = await getAPI(SysLogExApi).apiSysLogExExportPost(state.queryParams as any, { responseType: 'blob' });
	options.loading = false;
	// VXETable.saveFile({ filename: getFileName(res.headers), type: 'xlsx', content: res.data as any });
	return Promise.resolve();
};

// 提交
const submit = () => {
	ruleFormRef.value.validate(async (valid: boolean) => {
		if (!valid) return;
		const tableData = xGrid.value?.getTableData().fullData ?? [];
		if (tableData.length === 0) {
			ElMessage({ type: 'error', message: `请添加列!` });
			return;
		}
		const rowValid = await xGrid.value?.validate(true);
		if (rowValid) {
			ElMessage.error('校验不通过');
			return;
		}
		const params: any = {
			dbColumnInfoList: tableData,
			...state.ruleForm,
		};
		await getAPI(SysDatabaseApi).apiSysDatabaseAddTablePost(params);
		closeDialog();
	});
};

// 增加主键列
const addPrimaryColumn = async () => {
	const fullData = xGrid.value?.getTableData().fullData;
	const colIndex = (fullData?.length ?? 0) + 1;
	const temp = await xGrid.value?.insertAt(
		{
			columnDescription: '主键ID',
			dataType: 'bigint',
			dbColumnName: 'Id',
			decimalDigits: 0,
			isIdentity: 0,
			isNullable: 0,
			isPrimarykey: 1,
			length: 0,
			orderNo: colIndex,
			editable: true,
			isNew: true,
		},
		-1
	);
	if (temp && temp.row) await xGrid.value?.setEditCell(temp?.row, 'dbColumnName');
};

// 增加普通列
const addColumn = async () => {
	const fullData = xGrid.value?.getTableData().fullData;
	const colIndex = (fullData?.length ?? 0) + 1;
	const temp = await xGrid.value?.insertAt(
		{
			columnDescription: '',
			dataType: 'varchar',
			dbColumnName: '',
			decimalDigits: 0,
			isIdentity: 0,
			isNullable: 1,
			isPrimarykey: 0,
			length: 64,
			orderNo: colIndex,
			editable: true,
			isNew: true,
		},
		-1
	);
	if (temp && temp.row) await xGrid.value?.setEditCell(temp?.row, 'dbColumnName');
};

// 增加租户列
const addTenantColumn = async () => {
	const fullData = xGrid.value?.getTableData().fullData;
	const colIndex = (fullData?.length ?? 0) + 1;
	const temp = await xGrid.value?.insertAt(
		{
			columnDescription: '租户Id',
			dataType: 'bigint',
			dbColumnName: 'TenantId',
			decimalDigits: 0,
			isIdentity: 0,
			isNullable: 1,
			isPrimarykey: 0,
			length: 0,
			orderNo: colIndex,
			editable: true,
			isNew: true,
		},
		-1
	);
	if (temp && temp.row) await xGrid.value?.setEditCell(temp?.row, 'dbColumnName');
};

// 增加通用基础列
const addBaseColumn = async () => {
	const fileds = [
		{ dataType: 'varchar', name: 'Code', desc: '编码', length: 64 },
		{ dataType: 'varchar', name: 'Name', desc: '名称', length: 64 },
		{ dataType: 'varchar', name: 'Description', desc: '描述', length: 500 },
		{ dataType: 'datetime', name: 'CreateTime', desc: '创建时间' },
		{ dataType: 'bigint', name: 'CreateUserId', desc: '创建人Id' },
		{ dataType: 'varchar', name: 'CreateUserName', desc: '创建人', length: 64 },
		{ dataType: 'datetime', name: 'UpdateTime', desc: '修改时间' },
		{ dataType: 'bigint', name: 'UpdateUserId', desc: '修改人Id' },
		{ dataType: 'varchar', name: 'UpdateUserName', desc: '修改人', length: 64 },
		{ dataType: 'bigint', name: 'CreateOrgId', desc: '创建者机构Id' },
		{ dataType: 'varchar', name: 'CreateOrgName', desc: '创建者机构名称', length: 64 },
		{ dataType: 'bit', name: 'IsDelete', desc: '软删除', isNullable: 0 },
		{ dataType: 'datetime', name: 'DeleteTime', desc: '软删除时间' },
	];
	let temp = {} as any;
	const fullData = xGrid.value?.getTableData().fullData;
	let colIndex = (fullData?.length ?? 0) + 1;
	for (let i = 0; i < fileds.length; i++) {
		temp = await xGrid.value?.insertAt(
			{
				columnDescription: fileds[i].desc,
				dataType: fileds[i].dataType,
				dbColumnName: fileds[i].name,
				decimalDigits: 0,
				isIdentity: 0,
				isNullable: fileds[i].isNullable === 0 ? 0 : 1,
				isPrimarykey: 0,
				length: (fileds[i] as any).length || 0,
				orderNo: colIndex,
				editable: true,
				isNew: true,
			},
			-1
		);
		colIndex++;
	}
	if (temp && temp.row) await xGrid.value?.setEditCell(temp?.row, 'dbColumnName');
};

// 删除行
const deleteRow = (row: any) => {
	const fullData = xGrid.value?.getTableData().fullData;
	fullData?.filter((e: any) => e.orderNo > row.orderNo)?.forEach((e: any) => (e.orderNo = e.orderNo - 1));
	xGrid.value?.remove(row);
};

// 删除选中行
const deleteSelected = () => {
	const selected = xGrid.value?.getCheckboxRecords();
	const fullData = xGrid.value?.getTableData().fullData;
	// 更新序号
	for (let i = 0; i < selected!.length; i++) {
		fullData?.filter((e: any) => e.orderNo > selected![i].orderNo)?.forEach((e: any) => (e.orderNo = e.orderNo - 1));
	}
	// 移除选中
	xGrid.value?.removeCheckboxRow();
};

// 导出对象
defineExpose({ openDialog });
</script>

<style lang="scss" scoped>
.sys-dbTable-container :deep(.el-dialog__body) {
}

.xGrid-table-style .drag-btn {
	cursor: move;
	font-size: 20px;
}

.xGrid-table-style .vxe-body--row.sortable-ghost,
.xGrid-table-style .vxe-body--row.sortable-chosen {
	background-color: #e40000 !important;
}
</style>
