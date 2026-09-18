<template>
	<div class="sys-grantMenu-container">
		<el-drawer v-model="state.isVisible" size="55%">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Menu /> </el-icon>
					<span>{{ state.drawerTitle }}</span>
				</div>
			</template>
			<div>
				<NoticeBar leftIcon="iconfont icon-tongzhi2" :text="$t('message.list.noAuthForMenu')" :scrollable="true" />
			</div>
			<div v-loading="state.loading" style="padding: 10px">
				<el-tree
					ref="treeRef"
					:data="state.menuData"
					node-key="id"
					show-checkbox
					:props="{ children: 'children', label: 'title', class: treeNodeClass }"
					icon="ele-Menu"
					highlight-current
					default-expand-all
				/>
			</div>
			<template #footer>
				<div style="margin-bottom: 20px; margin-right: 20px">
					<el-button icon="ele-CircleCloseFilled" @click="cancel">{{ $t('message.list.cancelButtonText') }}</el-button>
					<el-button type="primary" icon="ele-CircleCheckFilled" @click="submit">{{ $t('message.list.confirmButtonText') }}</el-button>
				</div>
			</template>
		</el-drawer>
	</div>
</template>

<script lang="ts" setup>
import { reactive, onMounted, ref } from 'vue';
import type { ElTree } from 'element-plus';
import NoticeBar from '/@/components/noticeBar/index.vue';
import { useI18n } from 'vue-i18n';

import { getAPI } from '/@/utils/axios-utils';
import { SysMenuApi, SysRoleApi } from '/@/api-services/system/api';
import { SysMenu } from '/@/api-services/system/models';

const { t } = useI18n();
const treeRef = ref<InstanceType<typeof ElTree>>();
const state = reactive({
	loading: false,
	isVisible: false,
	drawerTitle: '',
	menuIdList: [] as any,
	roleId: 0,
	menuData: [] as Array<SysMenu>, // 菜单数据
});

// 页面初始化
onMounted(async () => {
	state.loading = true;
	var res = await getAPI(SysMenuApi).apiSysMenuListGet();
	state.menuData = res.data.result ?? [];
	recurenceMenuData(state.menuData);
	state.loading = false;
});

const recurenceMenuData = (menuData: Array<SysMenu>) => {
	menuData.forEach((item) => {
		if (item.children) {
			recurenceMenuData(item.children);
		}
	});
};

// 打开页面
const openDrawer = async (row: any) => {
	state.roleId = row.id;
	state.drawerTitle = t('message.list.grantRoleMenuWithName', { name: row.name });

	state.loading = true;
	treeRef.value?.setCheckedKeys([]); // 清空选中值
	if (row.id != undefined) {
		var res = await getAPI(SysRoleApi).apiSysRoleOwnMenuListGet(row.id);
		setTimeout(() => {
			treeRef.value?.setCheckedKeys(res.data.result ?? []);
		}, 100);
	}
	state.loading = false;
	state.isVisible = true;
};

// 取消
const cancel = () => {
	state.isVisible = false;
};

// 提交
const submit = async () => {
	state.menuIdList = treeRef.value?.getCheckedKeys(true) as Array<number>; //.concat(treeRef.value?.getHalfCheckedKeys());
	await getAPI(SysRoleApi).apiSysRoleGrantMenuPost({ id: state.roleId, menuIdList: state.menuIdList });
	cancel();
};

// 叶子节点同行显示样式
const treeNodeClass = (node: SysMenu) => {
	let addClass = true; // 添加叶子节点同行显示样式
	for (var key in node.children) {
		// 如果存在子节点非叶子节点，不添加样式
		if (node.children[key as any].children?.length ?? 0 > 0) {
			addClass = false;
			break;
		}
	}
	return addClass ? 'penultimate-node' : '';
};

// 导出对象
defineExpose({ openDrawer });
</script>

<style lang="scss" scoped>
// .menu-data-tree {
// 	width: 100%;
// 	border: 1px solid var(--el-border-color);
// 	border-radius: var(--el-input-border-radius, var(--el-border-radius-base));
// 	padding: 5px;
// }

:deep(.penultimate-node) {
	.el-tree-node__children {
		padding-left: 35px;
		white-space: pre-wrap;
		line-height: 100%;

		.el-tree-node {
			display: inline-block;
		}

		.el-tree-node__content {
			padding-left: 5px !important;
			padding-right: 5px;

			// .el-tree-node__expand-icon {
			// 	display: none;
			// }
		}
	}
}
</style>
