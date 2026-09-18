<template>
	<div class="sys-grantTable-container">
		<el-drawer v-model="state.isVisible" size="55%">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Menu /> </el-icon>
					<span>{{ state.drawerTitle }}</span>
				</div>
			</template>
			<div>
				<NoticeBar leftIcon="iconfont icon-tongzhi2" :text="$t('message.list.noAuthForTable')" :scrollable="true" />
			</div>
			<div v-loading="state.loading" style="margin: 10px">
				<el-tree
					ref="treeRef"
					:data="state.roleTableData"
					node-key="id"
					show-checkbox
					:props="{ children: 'columns', label: 'label', class: 'penultimate-node' }"
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
import { SysRoleApi } from '/@/api-services/system/api';
import { RoleTableOutput } from '/@/api-services/system/models';

const treeRef = ref<InstanceType<typeof ElTree>>();
const state = reactive({
	loading: false,
	isVisible: false,
	drawerTitle: '',
	tableColumnList: [] as any,
	roleId: 0,
	roleTableData: [] as Array<RoleTableOutput>, // 表格字段数据
});

const { t } = useI18n();

// 页面初始化
onMounted(async () => {
	state.loading = true;
	var res = await getAPI(SysRoleApi).apiSysRoleAllTableColumnListGet();
	state.roleTableData = res.data.result ?? [];
	state.loading = false;
});

// 打开页面
const openDrawer = async (row: any) => {
	state.roleId = row.id;
	state.drawerTitle = t('message.list.setApiBlacklist', { name: row.name });

	state.loading = true;
	treeRef.value?.setCheckedKeys([]); // 清空选中值
	if (row.id != undefined) {
		var res = await getAPI(SysRoleApi).apiSysRoleRoleTableRoleIdGet(row.id);
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
	state.tableColumnList = treeRef.value?.getCheckedKeys(true) as Array<number>; //.concat(treeRef.value?.getHalfCheckedKeys());
	await getAPI(SysRoleApi).apiSysRoleGrantRoleTablePost({ id: state.roleId, tableColumnList: state.tableColumnList });
	cancel();
};

// 导出对象
defineExpose({ openDrawer });
</script>

<style lang="scss" scoped>
:deep(.penultimate-node) {
	.el-tree-node__children {
		padding-left: 35px;
		white-space: pre-wrap;
		line-height: 100%;

		.el-tree-node {
			display: inline-block;
			min-width: 160px;
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
