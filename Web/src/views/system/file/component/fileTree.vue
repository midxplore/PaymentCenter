<template>
	<el-card class="box-card" shadow="hover" body-style="height:100%;overflow:auto;padding:5px;width:100%;">
		<template #header>
			<div class="card-header">
				<div class="tree-h-flex">
					<div class="tree-h-left">
						<el-input :prefix-icon="Search" v-model.lazy="filterText" clearable placeholder="文件路径" />
					</div>
					<div class="tree-h-right">
						<el-dropdown @command="handleCommand">
							<el-button style="margin-left: 8px; width: 34px">
								<el-icon class="el-icon--center">
									<more-filled />
								</el-icon>
							</el-button>
							<template #dropdown>
								<el-dropdown-menu>
									<el-dropdown-item command="expandAll">全部展开</el-dropdown-item>
									<el-dropdown-item command="collapseAll">全部折叠</el-dropdown-item>
									<el-dropdown-item command="rootNode">根节点</el-dropdown-item>
									<el-dropdown-item command="refresh">刷新</el-dropdown-item>
								</el-dropdown-menu>
							</template>
						</el-dropdown>
					</div>
					<el-checkbox v-if="!props.checkStrictly && state.isShowCheckbox" v-model="state.strictly" label="联动" style="margin-left: 8px" border />
				</div>
			</div>
		</template>
		<div style="margin-bottom: 45px" v-loading="state.loading">
			<el-scrollbar>
				<el-tree
					ref="treeRef"
					class="filter-tree"
					:data="state.folderData"
					node-key="id"
					:props="{ children: 'children', label: 'name' }"
					:filter-node-method="filterNode"
					@node-click="nodeClick"
					:show-checkbox="state.isShowCheckbox"
					default-expand-all
					highlight-current
					:expand-on-click-node="false"
					:check-strictly="!state.strictly"
				>
					<template #default="{ node }">
						<el-icon v-if="node.level < 4" size="16" style="margin-right: 3px; display: inline; vertical-align: middle"><ele-FolderOpened /></el-icon>
						<el-icon v-else size="16" style="margin-right: 3px; display: inline; vertical-align: middle"><ele-Folder /></el-icon>
						{{ node.label }}
					</template>
				</el-tree>
			</el-scrollbar>
		</div>
	</el-card>
</template>

<script lang="ts" setup>
import { onMounted, reactive, ref, watch } from 'vue';
import type { ElTree } from 'element-plus';
import { Search, MoreFilled } from '@element-plus/icons-vue';
import { TreeKey } from 'element-plus/es/components/tree/src/tree.type';

import { getAPI } from '/@/utils/axios-utils';
import { SysFileApi } from '/@/api-services/system/api';
import { TreeNode } from '/@/api-services/system/models';

const props = defineProps({
	checkStrictly: { type: Boolean, default: true },
});

const filterText = ref('');
const treeRef = ref<InstanceType<typeof ElTree>>();
const state = reactive({
	loading: false,
	folderData: [] as Array<TreeNode>,
	isShowCheckbox: false,
	strictly: false,
});

// 页面初始化
onMounted(async () => {
	await fetchTreeData();
});

// 查询监听
watch(filterText, (val) => {
	treeRef.value!.filter(val);
});

// 获取树数据
const fetchTreeData = async (showLoading: boolean = true) => {
	if (showLoading) state.loading = true;
	var res = await getAPI(SysFileApi).apiSysFileFolderGet();
	state.folderData = res.data.result ?? [];
	if (showLoading) state.loading = false;
	return res.data.result ?? [];
};

// 获取已经选择
const getCheckedKeys = () => {
	return treeRef.value!.getCheckedKeys();
};

// 节点过滤
const filterNode = (value: string, data: any) => {
	if (!value) return true;
	return data.name.includes(value);
};

// 节点操作
const handleCommand = async (command: string | number | object) => {
	if ('expandAll' == command) {
		for (let i = 0; i < treeRef.value!.store._getAllNodes().length; i++) {
			treeRef.value!.store._getAllNodes()[i].expanded = true;
		}
	} else if ('collapseAll' == command) {
		for (let i = 0; i < treeRef.value!.store._getAllNodes().length; i++) {
			treeRef.value!.store._getAllNodes()[i].expanded = false;
		}
	} else if ('refresh' == command) {
		fetchTreeData();
	} else if ('rootNode' == command) {
		treeRef.value?.setCurrentKey();
		emits('node-click', { id: 0, name: '' });
	}
};

// 与父组件的交互逻辑
const emits = defineEmits(['node-click']);
const nodeClick = (node: any, data: any) => {
	var path = node.name;
	if (data.level == 4) {
		// 当点击日节点时，获取对应的月和年
		var monthNode = treeRef.value!.getNode(node.pid);
		var yearNode = treeRef.value!.getNode(monthNode.data.pid);
		path = `${yearNode.data.name}/${monthNode.data.name}/${node.name}`;
	} else if (data.level == 3) {
		// 当点击月节点时，获取对应的年
		var yearNode = treeRef.value!.getNode(node.pid);
		path = `${yearNode.data.name}/${node.name}`;
	}
	emits('node-click', { id: node.id, name: node.name, path: path });
};

// 设置当前选中节点
const setCurrentKey = (key?: TreeKey | undefined, shouldAutoExpandParent?: boolean | undefined) => {
	treeRef.value?.setCurrentKey(key, shouldAutoExpandParent);
};

// 导出对象
defineExpose({ fetchTreeData, getCheckedKeys, setCurrentKey });
</script>

<style lang="scss" scoped>
.box-card {
	flex: 1;
	> :deep(.el-card__header) {
		padding: 5px;
	}
}
.tree-h-flex {
	display: flex;
}

.tree-h-left {
	flex: 1;
	width: 100%;
}

.tree-h-right {
	width: 42px;
	min-width: 42px;
}
</style>
