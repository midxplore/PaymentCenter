<template>
	<el-dialog v-model="visibleDialog" style="width: 70vw" append-to-body draggable :close-on-click-modal="false">
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Edit /> </el-icon>
				<span> 选择账号 </span>
			</div>
		</template>
		<el-row :gutter="10">
			<el-col :span="6">
				<p class="tip">机构</p>
				<OrgTree ref="orgTreeRef" @node-click="handleNodeChange" class="boxHeight" />
			</el-col>
			<el-col :span="12">
				<p class="tip">账号列表</p>
				<div style="margin-top: 10px">
					<el-form :model="state.queryParams" ref="queryRef" :inline="true">
						<el-form-item label="关键字">
							<el-input v-model="state.queryParams.keyword" clearable style="width: 150px" @keyup.enter="handleQuery" />
						</el-form-item>
						<el-form-item>
							<el-button-group>
								<el-button type="primary" icon="ele-Search" @click="handleQuery"> 查询 </el-button>
								<el-button icon="ele-Refresh" @click="resetQuery"> 重置 </el-button>
							</el-button-group>

							<el-button type="primary" icon="ele-DArrowRight" plain style="margin-left: 10px" @click="handleSelectAllUser"> 全选 </el-button>
						</el-form-item>
					</el-form>
					<el-card class="full-table" shadow="hover">
						<el-table ref="refTable" :data="userList" style="height: calc(70vh - 96px)">
							<el-table-column label="操作" width="60" align="center">
								<template #default="scope">
									<el-button link type="primary" @click="handleSelectUser(scope.row)">
										<el-icon><ele-Plus /></el-icon>
									</el-button>
								</template>
							</el-table-column>
							<el-table-column label="账号名称" prop="account" align="center" :show-overflow-tooltip="true" />
							<el-table-column label="真实姓名" prop="realName" align="center" :show-overflow-tooltip="true" />
							<el-table-column label="联系方式" prop="phone" align="center" :show-overflow-tooltip="true" />
							<el-table-column label="岗位" prop="posName" align="center" :show-overflow-tooltip="true" />
							<el-table-column label="工号" prop="jobNum" align="center" :show-overflow-tooltip="true" />
							<el-table-column label="所属机构" prop="orgName" align="center" :show-overflow-tooltip="true" />
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
							style="padding-bottom: 8px"
						/>
					</el-card>
				</div>
			</el-col>
			<el-col :span="6">
				<p class="tip">已选中列表</p>
				<el-table ref="selectedTable" :data="checkedUsersList" border class="boxHeight">
					<el-table-column label="操作" width="60" align="center" class-name="small-padding fixed-width">
						<template #default="scope">
							<div @click="handleRemove(scope.row)" style="border: 1px dashed red">
								<el-icon color="red" style="transform: translateY(2px)"><ele-Minus /></el-icon>
							</div>
						</template>
					</el-table-column>
					<el-table-column label="账号名称" prop="account" :show-overflow-tooltip="true" />
					<el-table-column label="真实姓名" prop="realName" :show-overflow-tooltip="true" />
					<el-table-column label="工号" prop="jobNum" align="center" :show-overflow-tooltip="true" />
				</el-table>
			</el-col>
		</el-row>
		<template #footer>
			<div class="dialog-footer">
				<el-button icon="ele-CircleCloseFilled" @click="closeDialog">取 消</el-button>
				<el-button type="primary" icon="ele-CircleCheckFilled" @click="saveDialog">确 定</el-button>
			</div>
		</template>
	</el-dialog>
</template>

<script lang="ts" setup>
import { ref, reactive, watch, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';

import OrgTree from '/@/views/system/org/component/orgTree.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysUserApi } from '/@/api-services/system/api';

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
					id: item.id,
					account: item.account,
					realName: item.realName,
				};
			});
		}
	}
);

// 页面初始化
onMounted(async () => {
	await getUserList();
});

// 重置操作
const resetQuery = () => {
	state.queryParams = {};
	handleQuery();
};

// 树组件点击
const handleNodeChange = async (node: any) => {
	state.queryParams.orgId = node.id;
	await handleQuery();
};

// 获取账号数据
const getUserList = async () => {
	let res = await getAPI(SysUserApi).apiSysUserPagePost(state.queryParams);
	userList.value = res.data.result?.items ?? [];
	state.queryParams.total = res.data.result?.total ?? 0;
};

// 搜索按钮操作
async function handleQuery() {
	state.queryParams.pageNum = 1;
	await getUserList();
}

// 选择授权账号操作
function handleSelectUser(row: { id: string }) {
	if (!row || row.id == '') return;

	if (!checkedUsersList.value.some((c: { id: any }) => c.id == row.id)) {
		checkedUsersList.value.push(row);
	}
}

// 移除选中账号操作
function handleRemove(row: { id: any }) {
	if (checkedUsersList.value.some((c: { id: any }) => c.id == row.id)) {
		checkedUsersList.value = checkedUsersList.value.filter((item: { id: any }) => item.id != row.id);
	} else {
		ElMessage.error('账号已被移除');
	}
}

// 全选
function handleSelectAllUser() {
	if (userList == null || userList.value.length < 1) return;

	userList.value.forEach((item: any) => {
		if (!checkedUsersList.value.some((c: { id: any }) => c.id == item.id)) {
			checkedUsersList.value.push(item);
		}
	});
}

// 确认/保存
let saveDialog = () => {
	let checkedList = [...checkedUsersList.value].map((item) => ({
		type: 1,
		id: item.id,
		account: item.account,
		realName: item.realName,
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
	height: calc(100% - 40px);
	overflow-y: scroll;
	overflow-x: hidden;
}
</style>
