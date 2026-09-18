<template>
	<div class="sys-dictType-container">
		<el-dialog v-model="state.visible" draggable :close-on-click-modal="false" width="700px">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
					<span> {{ props.title }} </span>
				</div>
			</template>
			<el-form :model="state.ruleForm" ref="formRef" label-width="auto">
				<el-row :gutter="10">
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="字典名称" prop="name" :rules="[{ required: true, message: '字典名称不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.name" placeholder="字典名称" clearable />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="字典编码" prop="code" :rules="[{ required: true, message: '字典编码不能为空', trigger: 'blur' }]">
							<el-input v-model="state.ruleForm.code" placeholder="字典编码" clearable />
						</el-form-item>
					</el-col>
					<!-- ★ 「租户字典」表单项已删除（原为 el-col + g-sys-dict 绑定 ruleForm.isTenant）。
					     该字段在本项目里已被**彻底移除**：后端实体 SysDictType.IsTenant 已注释掉、
					     SysDictTypeService 里两处引用也注释掉、DTO 无此字段、活库 sysdicttype 表
					     **没有 istenant 列**、生成的 API 客户端模型里也没有。
					     但它仍以 **required** 形式渲染给超管：填了也到不了后端（静默丢弃），
					     等于逼操作者填一个不起作用的必填项 —— 属于「界面承诺了不存在的功能」。
					     若将来恢复多租户字典，请连同后端实体/服务/表结构一起恢复，不要只加回这一块。 -->
					<el-col :xs="12" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="状态">
							<g-sys-dict v-model="state.ruleForm.status" code="StatusEnum" render-as="radio" />
						</el-form-item>
					</el-col>
					<el-col :xs="12" :sm="12" :md="12" :lg="12" :xl="12" class="mb20">
						<el-form-item label="排序">
							<el-input-number v-model="state.ruleForm.orderNo" placeholder="排序" class="w100" />
						</el-form-item>
					</el-col>
					<el-col :xs="24" :sm="24" :md="24" :lg="24" :xl="24" class="mb20">
						<el-form-item label="备注">
							<el-input v-model="state.ruleForm.remark" placeholder="请输入备注内容" clearable type="textarea" />
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

<script lang="ts" setup name="sysEditDictType">
import { reactive, ref } from 'vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysDictTypeApi } from '/@/api-services/system/api';
import { UpdateDictTypeInput } from '/@/api-services/system/models';

const props = defineProps({
	title: String,
});
const emits = defineEmits(['handleQuery', 'handleUpdate']);
const formRef = ref();
const state = reactive({
	visible: false,
	ruleForm: {} as UpdateDictTypeInput,
});

// 打开弹窗
const openDialog = (row: any) => {
	state.ruleForm = JSON.parse(JSON.stringify(row));
	state.visible = true;
	formRef.value?.resetFields();
};

// 关闭弹窗
const closeDialog = () => {
	emits('handleQuery');
	state.visible = false;
};

// 取消
const cancel = () => {
	state.visible = false;
};

// 提交
const submit = () => {
	formRef.value.validate(async (valid: boolean) => {
		if (!valid) return;
		if (state.ruleForm.id != undefined && state.ruleForm.id > 0) {
			await getAPI(SysDictTypeApi).apiSysDictTypeUpdatePost(state.ruleForm);
		} else {
			await getAPI(SysDictTypeApi).apiSysDictTypeAddPost(state.ruleForm);
		}
		emits('handleUpdate');
		closeDialog();
	});
};

// 导出对象
defineExpose({ openDialog });
</script>
