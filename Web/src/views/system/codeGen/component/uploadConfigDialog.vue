<template>
	<div class="sys-codeGenUpload-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" width="700px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span> 上传配置 </span>
				</div>
			</template>
			<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="链接预览" prop="useDownload" :rules="[{ required: true, message: '链接下载不能为空', trigger: 'blur' }]">
							<el-radio-group v-model="state.ruleForm.useDownload" filterable>
								<el-radio :value="true">是</el-radio>
								<el-radio :value="false">否</el-radio>
							</el-radio-group>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20" v-if="state.ruleForm.useDownload">
						<el-form-item label="链接文本" prop="downloadText" :rules="[{ required: true, message: '链接文本不能为空', trigger: 'blur' }]">
							<vxe-input v-model="state.ruleForm.downloadText" placeholder="请输入" class="w100" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="图片文件" prop="isImage" :rules="[{ required: true, message: '图片文件不能为空', trigger: 'blur' }]">
							<el-radio-group v-model="state.ruleForm.isImage" filterable>
								<el-radio :value="true">是</el-radio>
								<el-radio :value="false">否</el-radio>
							</el-radio-group>
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<template #footer>
				<span class="dialog-footer">
					<el-button icon="ele-CircleCloseFilled" @click="cancel">取 消</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit" v-reclick="1000">确 定</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="sysPreviewCode">
import { onMounted, reactive, ref } from 'vue';

let rowData = {} as any;
const emits = defineEmits(['submitData']);
const ruleFormRef = ref();
const state = reactive({
	isShowDialog: false,
	ruleForm: {} as any,
});

onMounted(async () => {});

// 打开弹窗
const openDialog = async (row: any) => {
	rowData = row;
	state.ruleForm = Object.assign({}, row.config);
	state.ruleForm.downloadText ??= '查看文件';
	state.ruleForm.useDownload ??= false;
	state.ruleForm.isImage ??= false;
	state.isShowDialog = true;
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

<style scoped></style>
