<template>
	<div class="sysSerial-container">
		<el-dialog v-model="state.showDialog" :width="800" draggable :close-on-click-modal="false">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span> {{ props.title }} </span>
				</div>
			</template>
			<el-form :model="state.ruleForm" ref="ruleFormRef" label-width="auto" :rules="rules">
				<el-row :gutter="35">
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="序列号" prop="seq">
							<el-input-number v-model="state.ruleForm.seq" placeholder="请输入序列号" clearable :disabled="true" class="w100" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="使用分类" prop="type">
							<el-select v-model="state.ruleForm.type" placeholder="分类">
								<el-option v-for="item in state.typeData" :key="item.value" :label="item.text" :value="item.value" />
							</el-select>
						</el-form-item>
					</el-col>

					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="表达式" prop="formater">
							<el-input v-model="state.ruleForm.formater" placeholder="表达式样例：R{yyyy}{MM}{dd}{HH}{mm}{ss}{SEQ}" clearable>
								<template #append>
									<el-dropdown placement="bottom" @command="(val: any) => (state.ruleForm.formater += val)">
										<el-button icon="ele-Guide"> 插槽 </el-button>
										<template #dropdown>
											<el-dropdown-menu>
												<el-dropdown-item v-for="key in state.slotList" :key="key" :command="key">{{ key }}</el-dropdown-item>
											</el-dropdown-menu>
										</template>
									</el-dropdown>
								</template>
							</el-input>
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="最小值" prop="min">
							<el-input-number v-model="state.ruleForm.min" placeholder="请输入最小值" clearable class="w100" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="最大值" prop="max">
							<el-input-number v-model="state.ruleForm.max" placeholder="请输入最大值" clearable class="w100" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="重置间隔" prop="resetInterval">
							<g-sys-dict v-model="state.ruleForm.resetInterval" :code="'ResetIntervalEnum'" render-as="select" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="排序">
							<el-input-number v-model="state.ruleForm.orderNo" placeholder="请输入排序" clearable class="w100" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="备注">
							<el-input v-model="state.ruleForm.remark" placeholder="请输入备注" clearable type="textarea" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20" v-if="state.previewSeqNo">
						<el-form-item label="预览">
							{{ state.previewSeqNo }}
						</el-form-item>
					</el-col>
				</el-row>
			</el-form>
			<template #footer>
				<span class="dialog-footer">
					<el-button type="warning" icon="ele-View" @click="preview" v-reclick="500">预览</el-button>
					<el-button icon="ele-CircleCloseFilled" @click="() => (state.showDialog = false)">{{ $t('message.list.cancelButtonText') }}</el-button>
					<el-button type="primary" v-reclick="1000" icon="ele-CircleCheckFilled" @click="submit">{{ $t('message.list.confirmButtonText') }}</el-button>
				</span>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="sysSerial">
import { ref, reactive, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import type { FormRules } from 'element-plus';

import { getAPI } from '/@/utils/axios-utils';
import { SysSerialApi } from '/@/api-services/system/api';
import { UpdateSerialInput } from '/@/api-services/system/models';

const props = defineProps({
	title: String,
});
const emit = defineEmits(['handleQuery']);
const ruleFormRef = ref();
const state = reactive({
	title: '',
	loading: false,
	showDialog: false,
	previewSeqNo: '',
	typeData: [] as any[],
	slotList: [] as any[],
	ruleForm: {} as UpdateSerialInput,
});

// 自行添加其他规则
const rules = ref<FormRules>({
	type: [{ required: true, message: '请选择使用分类！', trigger: 'change' }],
	formater: [{ required: true, message: '请选择格式化！', trigger: 'blur' }],
	resetInterval: [{ required: true, message: '请选择重置方式！', trigger: 'blur' }],
	min: [{ required: true, message: '请输入最小值！', trigger: 'blur' }],
	max: [{ required: true, message: '请输入最大值！', trigger: 'blur' }],
});

// 页面加载时
onMounted(async () => {
	state.slotList = await getAPI(SysSerialApi)
		.apiSysSerialSlotListGet()
		.then(({ data }) => data.result ?? []);
});

// 打开弹窗
const openDialog = async (row: any, typeData: any[]) => {
	state.ruleForm = JSON.parse(JSON.stringify(row));
	state.ruleForm.formater ??= '';
	state.previewSeqNo = '';
	state.typeData = typeData;
	state.showDialog = true;
	ruleFormRef.value?.resetFields();
};

// 关闭弹窗
const closeDialog = () => {
	emit('handleQuery');
	state.showDialog = false;
};

// 预览
const preview = () => {
	if (state.ruleForm.formater == '') return;

	getAPI(SysSerialApi)
		.apiSysSerialPreviewPost(state.ruleForm)
		.then((res) => {
			if (res.data.result) {
				ElMessage.success('获取成功');
				state.previewSeqNo = res.data.result;
				state.ruleForm.seq ??= 0;
				state.ruleForm.seq += 1;
			} else {
				ElMessage.error('预览失败');
			}
		});
};

// 提交
const submit = async () => {
	ruleFormRef.value.validate(async (valid: boolean) => {
		if (!valid) return;

		let values = JSON.parse(JSON.stringify(state.ruleForm));
		if (state.ruleForm.id) {
			await getAPI(SysSerialApi).apiSysSerialUpdatePost(values);
		} else {
			await getAPI(SysSerialApi).apiSysSerialAddPost(values);
		}
		closeDialog();
	});
};

// 将属性或者函数暴露给父组件
defineExpose({ openDialog });
</script>
