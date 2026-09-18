<template>
	<div class="sys-linkTableConfig-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="700px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span v-if="state.type == 'tree'"> 树选择配置 </span>
					<span v-if="state.type == 'fk'"> 外键配置 </span>
				</div>
			</template>
			<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="库定位器" prop="configId" :rules="[{ required: true, message: '库定位器不能为空', trigger: 'blur' }]">
							<el-select clearable v-model="state.ruleForm.configId" placeholder="库名" filterable @change="dbChanged()" class="w100">
								<el-option v-for="item in store.databaseList" :key="item.configId" :label="item.configId" :value="item.configId" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="数据库表" prop="tableName" :rules="[{ required: true, message: '数据库表不能为空', trigger: 'blur' }]">
							<el-select v-model="state.ruleForm.tableName" @change="tableChanged" filterable clearable class="w100">
								<el-option v-for="item in state.tableData" :key="item.entityName" :label="item.tableName + ' [' + item.tableComment + ']'" :value="item" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="显示字段" prop="displayPropertyNames" :rules="[{ required: true, message: '显示字段不能为空', trigger: 'blur' }]">
							<el-select v-model="state.ruleForm.displayPropertyNames" @change="(val: any) => changePropertyName(val, 'display')" filterable class="w100">
								<el-option v-for="item in state.columnList" :key="item.propertyName" :label="item.columnName + ' [' + item.columnComment + ']'" :value="item.propertyName" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="值字段" prop="linkPropertyName" :rules="[{ required: true, message: '值字段不能为空', trigger: 'blur' }]">
							<el-select v-model="state.ruleForm.linkPropertyName" @change="(val: any) => changePropertyName(val, 'link')" filterable class="w100">
								<el-option v-for="item in state.columnList" :key="item.propertyName" :label="item.columnName + ' [' + item.columnComment + ']'" :value="item" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col v-if="state.type == 'tree'" :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="父级字段" prop="parentPropertyName">
							<el-select v-model="state.ruleForm.parentPropertyName" @change="(val: any) => changePropertyName(val, 'parent')" filterable clearable class="w100">
								<el-option v-for="item in state.columnList" :key="item.propertyName" :label="item.columnName + ' [' + item.columnComment + ']'" :value="item" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="查询字段" prop="searchPropertyName">
							<el-select v-model="state.ruleForm.searchPropertyName" @change="(val: any) => changePropertyName(val, 'search')" filterable class="w100">
								<el-option v-for="item in state.columnList" :key="item.propertyName" :label="item.columnName + ' [' + item.columnComment + ']'" :value="item" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20" v-if="rowData.netType.startsWith('string')">
						<el-form-item label="是否多选" prop="multiple" :rules="[{ required: true, message: '是否多选不能为空', trigger: 'blur' }]">
							<el-radio-group v-model="state.ruleForm.multiple" filterable>
								<el-radio :value="true">是</el-radio>
								<el-radio :value="false">否</el-radio>
							</el-radio-group>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="快捷设置">
							<el-button v-if="state.type == 'tree'" type="primary" icon="ele-Search" plain @click="handelQuickConfig('sysOrg')">组织机构选择器</el-button>
							<el-button v-else type="primary" icon="ele-Search" plain @click="handelQuickConfig('sysUser')">系统账号选择器</el-button>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="cancel">取 消</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit">确 定</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<!-- 外键配置 -->
<script lang="ts" setup name="sysLinkTableConfig">
import { reactive, ref } from 'vue';
import { useCodeGenStore } from './codeGenStore';

let rowData = {} as any;
const store = useCodeGenStore();
const emits = defineEmits(['submitData']);
const ruleFormRef = ref();
const state = reactive({
	type: 'fk' as 'fk' | 'tree',
	isShowDialog: false,
	ruleForm: {} as any,
	dbData: [] as any,
	tableData: [] as any,
	columnList: [] as any,
});

const changePropertyName = (item: any, type: string) => {
	state.ruleForm[type + 'PropertyName'] = item.propertyName;
	state.ruleForm[type + 'PropertyType'] = item.netType;
};

const dbChanged = async () => {
	state.tableData = await store.tableListBy(state.ruleForm.configId);
};

const tableChanged = async (item: any) => {
	state.ruleForm.displayPropertyNames = undefined;
	state.ruleForm.parentPropertyName = undefined;
	state.ruleForm.searchPropertyName = undefined;
	state.ruleForm.linkPropertyName = undefined;
	state.ruleForm.tableComment = item.tableComment;
	state.ruleForm.entityName = item.entityName;
	state.ruleForm.tableName = item.tableName;
	state.columnList = await store.columnListBy(state.ruleForm.configId, state.ruleForm.tableName);
};

const handelQuickConfig = (type: string) => {
	if (store.quickConfigMap && store.quickConfigMap[type]) {
		state.ruleForm = Object.assign({}, store.quickConfigMap[type]);
	}
};

// 打开弹窗
const openDialog = async (row: any, type: 'fk' | 'tree') => {
	rowData = row;
	state.type = type;
	state.isShowDialog = true;
	state.ruleForm.multiple ??= false;
	state.ruleForm.useTable ??= false;
	state.ruleForm = Object.assign({}, row.config);
	if (state.ruleForm.configId) {
		dbChanged().then(async () => {
			if (state.ruleForm.tableName) {
				state.columnList = await store.columnListBy(state.ruleForm.configId, state.ruleForm.tableName);
			}
		});
	}
};

// 关闭弹窗
const closeDialog = () => {
	if (!rowData.netType.startsWith('string')) state.ruleForm.multiple = false;
	rowData.config = Object.assign({}, state.ruleForm);
	emits('submitData', rowData);
	cancel();
};

// 取消
const cancel = () => {
	state.isShowDialog = false;
	ruleFormRef.value?.resetFields();
	state.ruleForm = {};
};

// 提交
const submit = () => {
	ruleFormRef.value.validate(async (valid: boolean) => {
		if (!valid) return;
		closeDialog();
	});
};

// 导出对象
defineExpose({ openDialog });
</script>
