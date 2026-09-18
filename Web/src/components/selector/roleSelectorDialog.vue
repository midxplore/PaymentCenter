<template>
	<el-dialog v-model="visibleDialog" style="width: 70vw" append-to-body draggable :close-on-click-modal="false">
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
				<span> 选择角色 </span>
			</div>
		</template>
		<el-row :gutter="10">
			<el-col :span="16">
				<p class="tip">角色列表</p>
				<div style="margin-top: 10px">
					<el-form :model="state.queryParams" ref="queryRef" :inline="true">
						<el-form-item label="角色名称" prop="name">
							<el-input v-model="state.queryParams.name" placeholder="请输入角色名称" clearable style="width: 150px" />
						</el-form-item>
						<el-form-item>
							<el-button-group>
								<el-button type="primary" icon="ele-Search" @click="handleQuery"> 查询 </el-button>
								<el-button icon="ele-Refresh" @click="resetQuery"> 重置 </el-button>
							</el-button-group>
						</el-form-item>
					</el-form>
					<el-card class="full-table" shadow="hover" style="margin-top: 5px">
						<el-table ref="refTable" :data="userList" style="height: calc(60vh - 100px)">
							<el-table-column label="操作" width="60" align="center">
								<template #default="scope">
									<el-button link type="primary" @click="handleSelectUser(scope.row)">
										<el-icon><ele-Plus /></el-icon>
									</el-button>
								</template>
							</el-table-column>
							<el-table-column label="角色名称" prop="name" width="150" align="center" :show-overflow-tooltip="true" />
							<el-table-column label="角色编码" prop="code" width="120" align="center" :show-overflow-tooltip="true" />
							<el-table-column label="数据范围" prop="roleName" align="center">
								<template #default="scope">
									<el-tag v-if="scope.row.dataScope === 1">全部数据</el-tag>
									<el-tag v-else-if="scope.row.dataScope === 2">本部门及以下数据</el-tag>
									<el-tag v-else-if="scope.row.dataScope === 3">本部门数据</el-tag>
									<el-tag v-else-if="scope.row.dataScope === 4">仅本人数据</el-tag>
									<el-tag v-else-if="scope.row.dataScope === 5">自定义数据</el-tag>
								</template>
							</el-table-column>
							<el-table-column label="租户名称" prop="tenantName" />
						</el-table>

						<el-pagination
							:currentPage="state.queryParams.page"
							:page-size="state.queryParams.pageSize"
							:total="state.queryParams.total"
							:page-sizes="[10, 20, 50, 100]"
							background
							@size-change="handleSizeChange"
							@current-change="handleCurrentChange"
							layout="total, sizes, prev, pager, next, jumper"
						/>
					</el-card>
				</div>
			</el-col>
			<el-col :span="8">
				<p class="tip">已选中列表</p>
				<el-table ref="selectedTable" :data="checkedUsersList" border style="height: 60vh">
					<el-table-column label="操作" width="60" align="center" class-name="small-padding fixed-width">
						<template #default="scope">
							<div @click="handleRemove(scope.row)" style="border: 1px dashed red">
								<el-icon color="red" style="transform: translateY(2px)"><ele-Minus /></el-icon>
							</div>
						</template>
					</el-table-column>
					<el-table-column label="角色名称" prop="name" :show-overflow-tooltip="true" />
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

import { getAPI } from '/@/utils/axios-utils';
import { SysRoleApi } from '/@/api-services/system/api';

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
	total: 0,
	queryParams: {
		orgId: -1,
		page: 1,
		pageSize: 50,
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
	await getRoleList();
});

// 重置操作
const resetQuery = () => {
	state.queryParams = {};
	handleQuery();
};

// 获取角色数据
const getRoleList = async () => {
	let res = await getAPI(SysRoleApi).apiSysRolePagePost(state.queryParams);
	userList.value = res.data.result?.items ?? [];
	state.queryParams.total = res.data.result?.total ?? 0;
};

// 搜索按钮操作
async function handleQuery() {
	state.queryParams.pageNum = 1;
	await getRoleList();
}

// 选择授权角色操作
function handleSelectUser(row: { id: string }) {
	if (!row || row.id == '') {
		ElMessage.error('请选择角色');
		return;
	}
	if (checkedUsersList.value.some((c: { id: any }) => c.id == row.id)) {
		ElMessage.error('角色已被选中');
	} else {
		checkedUsersList.value.push(row);
	}
}
// 移除选中角色操作
function handleRemove(row: { id: any }) {
	if (checkedUsersList.value.some((c: { id: any }) => c.id == row.id)) {
		checkedUsersList.value = checkedUsersList.value.filter((item: { id: any }) => item.id != row.id);
	} else {
		ElMessage.error('角色已被移除');
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

// 改变页面容量
const handleSizeChange = (val: number) => {
	state.queryParams.pageSize = val;
	handleQuery();
};

// 改变页码序号
const handleCurrentChange = (val: number) => {
	state.queryParams.page = val;
	handleQuery();
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
