<template>
	<el-dialog v-model="visibleDialog" style="width: 70vw" append-to-body draggable :close-on-click-modal="false">
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
				<span> 选择部门 </span>
			</div>
		</template>
		<el-row :gutter="10">
			<el-col :span="6">
				<p class="tip">部门</p>
				<OrgTree ref="orgTreeRef" @node-click="handleNodeChange" class="boxHeight" />
			</el-col>
			<el-col :span="12">
				<p class="tip">部门列表</p>
				<div style="margin-top: 10px">
					<el-form :model="state.queryParams" ref="queryRef" :inline="true">
						<el-form-item label="部门名称" prop="name">
							<el-input v-model="state.queryParams.name" placeholder="请输入部门名称" clearable style="width: 150px" @keyup.enter="handleQuery" />
						</el-form-item>
						<el-form-item>
							<el-button-group>
								<el-button type="primary" icon="ele-Search" @click="handleQuery"> 查询 </el-button>
								<el-button icon="ele-Refresh" @click="resetQuery"> 重置 </el-button>
							</el-button-group>
						</el-form-item>
					</el-form>
					<el-card class="full-table" shadow="hover" style="margin-top: 5px">
						<el-table ref="refTable" :data="userList" style="height: calc(60vh - 100px)" row-key="id" :tree-props="{ children: 'children' }">
							<el-table-column label="子级" width="100" />
							<el-table-column label="操作" width="80" align="center">
								<template #default="scope">
									<el-button link type="primary" @click="handleSelectUser(scope.row)">
										<el-icon><ele-Plus /></el-icon>
									</el-button>
								</template>
							</el-table-column>
							<el-table-column label="部门名称" prop="name" minWidth="200" :show-overflow-tooltip="true" />
						</el-table>
					</el-card>
				</div>
			</el-col>
			<el-col :span="6">
				<p class="tip">已选中列表</p>
				<el-table ref="selectedTable" :data="checkedUsersList" border style="height: 60vh">
					<el-table-column label="操作" width="60" align="center">
						<template #default="scope">
							<div @click="handleRemove(scope.row)" style="border: 1px dashed red">
								<el-icon color="red" style="transform: translateY(2px)"><ele-Minus /></el-icon>
							</div>
						</template>
					</el-table-column>
					<el-table-column label="部门名称" prop="name" :show-overflow-tooltip="true" />
				</el-table>
			</el-col>
		</el-row>
		<template #footer>
			<div class="dialog-footer">
				<el-button type="primary" @click="saveDialog">确 定</el-button>
				<el-button @click="closeDialog">取 消</el-button>
			</div>
		</template>
	</el-dialog>
</template>

<script lang="ts" setup>
import { ref, reactive, watch, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';

import OrgTree from '/@/views/system/org/component/orgTree.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysOrgApi } from '/@/api-services/system/api';

let emits = defineEmits(['update:visible', 'change']);
const props = defineProps({
	visible: {
		type: Boolean,
		default: false,
	},
	data: {
		type: Array,
		default: () => [],
	},
});

const userList = ref([] as any);
let checkedUsersList = ref([] as any);
const state = reactive({
	queryParams: {
		id: 0,
	} as any,
});

let visibleDialog = computed({
	get() {
		return props.visible;
	},
	set() {
		closeDialog();
	},
});

watch(
	() => props.visible,
	(newVal) => {
		if (newVal) {
			checkedUsersList.value = props.data.map((item: any) => {
				return {
					id: item.targetId,
					name: item.name,
				};
			});
		}
	}
);

// 页面初始化
onMounted(async () => {
	await getOrgList();
});

// 重置操作
const resetQuery = () => {
	state.queryParams = {
		id: 0,
	};
	handleQuery();
};

// 树组件点击
const handleNodeChange = async (node: any) => {
	state.queryParams.id = node.id;
	await handleQuery();
};

// 获取机构数据
const getOrgList = async () => {
	const params = Object.assign(state.queryParams);
	let res = await getAPI(SysOrgApi).apiSysOrgListGet(params.id, params.name, params.code, params.type);
	userList.value = res.data.result ?? [];
};

// 搜索按钮操作
async function handleQuery() {
	state.queryParams.pageNum = 1;
	await getOrgList();
}

// 选择授权部门操作
function handleSelectUser(row: { id: string }) {
	if (!row || row.id == '') {
		ElMessage.error('请选择部门');
		return;
	}
	if (checkedUsersList.value.some((c: { id: any }) => c.id == row.id)) {
		ElMessage.error('部门已被选中');
	} else {
		checkedUsersList.value.push(row);
	}
}
// 移除选中部门操作
function handleRemove(row: { id: any }) {
	if (checkedUsersList.value.some((c: { id: any }) => c.id == row.id)) {
		checkedUsersList.value = checkedUsersList.value.filter((item: { id: any }) => item.id != row.id);
	} else {
		ElMessage.error('部门已被移除');
	}
}

// 确认/保存
let saveDialog = () => {
	let checkedList = [...checkedUsersList.value].map((item) => ({
		type: 1,
		targetId: item.id,
		name: item.name,
	}));
	emits('change', checkedList);
};

// 关闭弹窗
const closeDialog = () => {
	checkedUsersList.value = [];
	emits('update:visible', false);
};
</script>

<style lang="scss" scoped>
.tip {
	padding: 8px 16px;
	background-color: var(--el-color-primary-light-9);
	border-left: 5px solid var(--el-color-primary);
	margin-bottom: 5px;
}

.boxHeight {
	height: 60vh;
	overflow-y: scroll;
	overflow-x: hidden;
}
</style>
