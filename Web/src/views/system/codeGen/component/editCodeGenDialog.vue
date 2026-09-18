<template>
	<div class="sys-editCodeGen-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" fullscreen>
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span> {{ state.title }} </span>
				</div>
			</template>
			<el-form :model="store.ruleForm" ref="ruleFormRef" label-width="auto" v-loading="store.loading">
				<el-tabs v-model="state.activeTab" class="demo-tabs">
					<el-tab-pane label="代码生成">
						<el-row :gutter="10">
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="业务名" prop="busName" :rules="[{ required: true, message: '业务名不能为空', trigger: 'blur' }]">
									<el-input v-model="store.ruleForm.busName" placeholder="请输入" class="w100" clearable />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb5">
								<el-form-item label="场景类型" prop="scene" :rules="[{ required: true, message: '场景类型不能为空', trigger: 'blur' }]">
									<g-sys-dict v-model="store.ruleForm.scene" code="CodeGenSceneEnum" render-as="select" :disabled="!!store.ruleForm.id" :on-item-filter="(e: any) => e.value > 0" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="作者姓名" prop="authorName" class="flex w100">
									<el-input v-model="store.ruleForm.authorName" clearable placeholder="请输入姓名" class="w100" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="作者邮箱" prop="authorName" class="flex w100">
									<el-input v-model="store.ruleForm.email" clearable placeholder="请输入邮箱" class="w100" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="命名空间" prop="nameSpace" :rules="[{ required: true, message: '请选择命名空间', trigger: 'blur' }]">
									<el-select v-model="store.ruleForm.nameSpace" filterable clearable class="w100" placeholder="命名空间">
										<el-option v-for="(item, index) in store.namespaceList ?? []" :key="index" :label="item" :value="item" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="前端目录" prop="pagePath" :rules="[{ required: true, message: '前端目录不能为空', trigger: 'blur' }]">
									<template v-slot:label>
										<div>
											前端目录
											<el-tooltip raw-content content="指的是前端相对目录：src/views/main" placement="top">
												<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"><ele-QuestionFilled /></el-icon>
											</el-tooltip>
										</div>
									</template>
									<el-input v-model="store.ruleForm.pagePath" clearable placeholder="请输入" class="w100" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="生成方式" prop="generateMethod" :rules="[{ required: true, message: '生成方式不能为空', trigger: 'blur' }]">
									<g-sys-dict v-model="store.ruleForm.generateMethod" code="CodeGenMethodEnum" render-as="select" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="支持打印" prop="printType" :rules="[{ required: true, message: '支持打印不能为空', trigger: 'blur' }]">
									<g-sys-dict v-model="store.ruleForm.printType" code="CodeGenPrintTypeEnum" render-as="select" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="接口模式" prop="isApiService" :rules="[{ required: true, message: '接口模式不能为空', trigger: 'blur' }]">
									<template v-slot:label>
										<div>
											接口模式
											<el-tooltip
												raw-content
												content="接口服务模式是指根据swagger自动生成前端接口请求文件(需要手动双击批处理生成，目录api_build/build.bat)，推荐此模式。传统模式则是指手动根据swagger编写接口请求并进行模型定义。"
												placement="top"
											>
												<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"><ele-QuestionFilled /></el-icon>
											</el-tooltip>
										</div>
									</template>
									<el-radio-group v-model="store.ruleForm.isApiService">
										<el-radio :value="true">接口服务</el-radio>
										<el-radio :value="false">传统模式</el-radio>
									</el-radio-group>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="生成菜单" prop="generateMenu" :rules="[{ required: true, message: '生成菜单不能为空', trigger: 'blur' }]">
									<el-radio-group v-model="store.ruleForm.generateMenu">
										<el-radio :value="true">是</el-radio>
										<el-radio :value="false">否</el-radio>
									</el-radio-group>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20" v-if="store.ruleForm.generateMenu">
								<el-row>
									<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12">
										<el-form-item label="父级菜单" prop="menuPid">
											<el-cascader
												:options="store.menuList"
												:props="cascaderProps"
												placeholder="请选择上级菜单"
												:disabled="!store.ruleForm.generateMenu"
												filterable
												clearable
												class="w100"
												v-model="store.ruleForm.menuPid"
											>
												<template #default="{ node, data }">
													<span>{{ data.title }}</span>
													<span v-if="!node.isLeaf"> ({{ data.children.length }}) </span>
												</template>
											</el-cascader>
										</el-form-item>
									</el-col>
									<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12">
										<el-form-item label="菜单图标" prop="menuIcon" :rules="[{ required: true, message: '菜单图标不能为空', trigger: 'blur' }]">
											<IconSelector v-model="store.ruleForm.menuIcon as any" :size="other.globalComponentSize()" placeholder="菜单图标" type="all" />
										</el-form-item>
									</el-col>
								</el-row>
							</el-col>
							<el-col v-if="<number>store.ruleForm.scene >= 2000" :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
								<el-form-item label="布局方向" prop="isHorizontal" :rules="[{ required: true, message: '水平布局不能为空', trigger: 'blur' }]">
									<el-radio-group v-model="store.ruleForm.isHorizontal">
										<el-radio :value="false">水平</el-radio>
										<el-radio :value="true">垂直</el-radio>
									</el-radio-group>
								</el-form-item>
							</el-col>
						</el-row>
					</el-tab-pane>
					<el-tab-pane v-if="store.showTree()" label="树组件配置">
						<el-row>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item label="库定位器" prop="treeConfig.configId" :rules="[{ required: true, message: '库定位器不能为空', trigger: 'blur' }]">
									<el-select clearable v-model="store.ruleForm.treeConfig!.configId" placeholder="库名" filterable @change="(val: any) => dbChanged(val)" class="w100">
										<el-option v-for="item in store.databaseList ?? []" :key="item.configId" :label="item.configId" :value="item.configId" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item label="数据库表" prop="treeConfig.tableName" :rules="[{ required: true, message: '数据库表不能为空', trigger: 'blur' }]">
									<el-select v-model="store.ruleForm.treeConfig!.tableName" filterable clearable @change="(val: any) => tableChanged(store.ruleForm.treeConfig!.configId, val, 'tree')" class="w100">
										<el-option v-for="item in state.tableData" :key="item.entityName" :label="item.tableName + ' [' + item.tableComment + ']'" :value="item" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item label="显示字段" prop="treeConfig.displayPropertyNames" :rules="[{ required: true, message: '显示字段不能为空', trigger: 'blur' }]">
									<el-select v-model="store.ruleForm.treeConfig!.displayPropertyNames" filterable class="w100">
										<el-option v-for="item in state.columnList" :key="item.propertyName" :label="item.columnName + ' [' + item.columnComment + ']'" :value="item.propertyName" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item label="值字段" prop="treeConfig.linkPropertyName" :rules="[{ required: true, message: '值字段不能为空', trigger: 'blur' }]">
									<el-select v-model="store.ruleForm.treeConfig!.linkPropertyName" filterable class="w100" @change="(val: any) => changeTreePropertyName(val, 'link')">
										<el-option v-for="item in state.columnList" :key="item.propertyName" :label="item.columnName + ' [' + item.columnComment + ']'" :value="item" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item label="父级字段" prop="treeConfig.parentPropertyName" :rules="[{ required: true, message: '父级字段不能为空', trigger: 'blur' }]">
									<el-select v-model="store.ruleForm.treeConfig!.parentPropertyName" filterable class="w100" @change="(val: any) => changeTreePropertyName(val, 'parent')">
										<el-option v-for="item in state.columnList" :key="item.propertyName" :label="item.columnName + ' [' + item.columnComment + ']'" :value="item" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item label="查询字段" prop="treeConfig.searchPropertyName" :rules="[{ required: true, message: '查询字段不能为空', trigger: 'blur' }]">
									<el-select v-model="store.ruleForm.treeConfig!.searchPropertyName" filterable class="w100" @change="(val: any) => changeTreePropertyName(val, 'search')">
										<el-option v-for="item in state.columnList" :key="item.propertyName" :label="item.columnName + ' [' + item.columnComment + ']'" :value="item" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item label="树标题" prop="treeConfig.treeTitle" :rules="[{ required: true, message: '树标题不能为空', trigger: 'blur' }]">
									<el-input v-model="store.ruleForm.treeConfig!.treeTitle" clearable placeholder="请输入" class="w100" />
								</el-form-item>
							</el-col>
							<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
								<el-form-item label="是否多选">
									<el-radio-group v-model="store.ruleForm.treeConfig!.multiple" filterable>
										<el-radio :value="true">是</el-radio>
										<el-radio :value="false">否</el-radio>
									</el-radio-group>
								</el-form-item>
							</el-col>
						</el-row>
					</el-tab-pane>
					<el-tab-pane v-if="store.showMaster()" label="主表配置">
						<TableConfig :index="0" />
					</el-tab-pane>
					<el-tab-pane v-if="store.showSlave()" label="从表配置">
						<TableConfig :index="1" />
					</el-tab-pane>
				</el-tabs>
			</el-form>
			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="cancel" :disabled="state.loading">取 消</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit" :disabled="state.loading" v-reclick="1000">确 定</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<!-- 代码生成配置 -->
<script lang="ts" setup name="sysEditCodeGen">
import { defineAsyncComponent, nextTick, reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';
import other from '/@/utils/other';
import IconSelector from '/@/components/iconSelector/index.vue';

import { useCodeGenStore } from './codeGenStore';

const TableConfig = defineAsyncComponent(() => import('./tableConfig.vue'));
const emits = defineEmits(['handleQuery']);
const store = useCodeGenStore();
const ruleFormRef = ref();
const state = reactive({
	title: '编辑代码生成',
	isShowDialog: false,
	tableData: [] as any,
	columnList: [] as any,
	loading: false,
	activeTab: '0',
});

// 级联选择器配置选项
const cascaderProps = { checkStrictly: true, emitPath: false, value: 'id', label: 'title' };

// db改变
const dbChanged = async (configId: string) => {
	if (!configId) return;
	state.tableData = await store.tableListBy(configId);
};

// table改变
const tableChanged = async (configId: string, item: any, type: string) => {
	if (typeof item === `string`) item = state.tableData.find((i: any) => i.entityName == item || i.tableName == item);
	if (type == 'tree') {
		store.ruleForm.treeConfig ??= {} as any;
		store.ruleForm.treeConfig!.tableComment = item?.tableComment ?? undefined;
		store.ruleForm.treeConfig!.entityName = item?.entityName ?? undefined;
		if (store.ruleForm.treeConfig!.tableName != item?.tableName) {
			store.ruleForm.treeConfig!.tableName = item?.tableName ?? undefined;
			store.ruleForm.treeConfig!.displayPropertyNames = undefined as any;
			store.ruleForm.treeConfig!.parentPropertyName = undefined;
			store.ruleForm.treeConfig!.searchPropertyName = undefined;
			store.ruleForm.treeConfig!.linkPropertyName = undefined as any;
		}
	}
	// else if (type == 'tableRelationship') {
	// 	store.ruleForm.configObj.entityName = item?.entityName ?? undefined;
	// 	store.ruleForm.configObj.tableName = item?.tableName ?? undefined;
	// 	store.ruleForm.configObj.propertyName1 = undefined;
	// 	store.ruleForm.configObj.propertyName2 = undefined;
	// }
	state.columnList = (await store.columnListBy(configId, item?.tableName)) ?? [];
};

// 属性改变
const changeTreePropertyName = (item: any, type: string) => {
	if (type == 'display') {
		store.ruleForm.treeConfig!.displayPropertyNames = item.propertyName;
		//store.ruleForm!.treeConfig!.displayPropertyType = item.netType;
	} else if (type == 'link') {
		store.ruleForm.treeConfig!.linkPropertyName = item.propertyName;
		store.ruleForm!.treeConfig!.linkPropertyType = item.netType;
	} else if (type == 'search') {
		store.ruleForm.treeConfig!.searchPropertyName = item.propertyName;
		store.ruleForm!.treeConfig!.searchPropertyType = item.netType;
	} else if (type == 'parent') {
		store.ruleForm.treeConfig!.parentPropertyName = item.propertyName;
		store.ruleForm!.treeConfig!.parentPropertyType = item.netType;
	}
};

// 打开弹窗
const openDialog = async (row: any) => {
	store.enabledWatch = !row?.id;

	let scene = 1000 as any;
	try {
		state.loading = true;
		state.activeTab = '0';
		ruleFormRef.value?.resetFields();
		state.title = (row.id ? '编辑' : '新增') + '代码生成';
		store.ruleForm = row?.id ? await store.getDetail(row.id) : JSON.parse(JSON.stringify(row));
		scene = store.ruleForm.scene;
		store.ruleForm.scene = undefined;
		store.ruleForm.nameSpace ??= store.namespaceList?.[0] as any;
		state.isShowDialog = true;
	} finally {
		await nextTick(() => {
			store.ruleForm.scene = scene;
		});
		state.loading = false;
	}
	store.enabledWatch = true;
};

// 关闭弹窗
const closeDialog = () => {
	emits('handleQuery');
	state.isShowDialog = false;
};

// 取消
const cancel = () => {
	state.isShowDialog = false;
};

// 提交
const submit = () => {
	store.ruleForm.busName ??= store.ruleForm.tableList?.[0]?.busName;
	const data = Object.assign(store.ruleForm, {
		moduleName: store.ruleForm.moduleName ?? store.ruleForm.tableList?.[0]?.moduleName,
	});
	ruleFormRef.value.validate(async (isValid: boolean, fields?: any) => {
		if (isValid) {
			data.treeConfig = <number>data.scene % 100 == 10 ? data.treeConfig : undefined;
			await store.save(data);
			closeDialog();
		} else {
			ElMessage({
				message: `表单有${Object.keys(fields).length}处验证失败，请修改后再提交`,
				type: 'error',
			});
		}
	});
};

// 导出对象
defineExpose({ openDialog });
</script>

<style lang="scss" scoped>
:deep(.el-dialog__body) {
	min-height: 450px;
}
:deep(.el-overlay) {
	z-index: 999 !important;
}
</style>
