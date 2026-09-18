<template>
	<div class="sys-codeGenUpload-container">
		<el-dialog v-model="state.isShowDialog" draggable :close-on-click-modal="false" :width="400" :max-height="'100px'">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span> 时间数据配置 </span>
				</div>
			</template>
			<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="时间格式" prop="format" :rules="[{ required: true, message: '时间格式不能为空', trigger: 'blur' }]">
							<el-select v-model="state.ruleForm.format" filterable clearable class="w100">
								<el-option label="YYYY-mm-dd HH:MM:SS" value="datetime" />
								<el-option label="YYYY-dd-MM" value="date" />
								<el-option label="HH:MM:SS" value="time" />
							</el-select>
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
	state.isShowDialog = true;
	state.ruleForm = Object.assign({}, row.config);
};

// 关闭弹窗
const closeDialog = () => {
	rowData.config = Object.assign({}, state.ruleForm);
	emits('submitData', rowData);
	cancel();
};

// 取消
const cancel = () => {
	ruleFormRef.value?.resetFields();
	state.isShowDialog = false;
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
