<template>
	<div class="sys-grantApi-container">
		<el-drawer v-model="state.isVisible" size="40%">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Link /> </el-icon>
					<span>{{ state.drawerTitle }}</span>
				</div>
			</template>
			<div>
				<NoticeBar leftIcon="iconfont icon-tongzhi2" :text="$t('message.list.noAuthForApi')" :scrollable="true" />
			</div>
			<el-card class="tree-card" shadow="hover" style="height: calc(100vh - 110px)" body-style="height:100%; overflow:auto">
				<template #header>
					<div class="card-header">
						<div class="tree-h-flex">
							<div class="tree-h-left">
								<el-input prefix-icon="ele-Search" v-model="filterText" :placeholder="$t('message.list.apiRoute')" />
							</div>
						</div>
					</div>
				</template>

				<el-form-item v-loading="state.loading" style="margin-bottom: 45px">
					<el-row :gutter="24">
						<el-col :span="24" class="mb8">
							<el-tree
								ref="treeRef"
								class="filter-tree"
								:data="state.allApiData"
								:filter-node-method="filterNode"
								node-key="route"
								:props="{ children: 'children', label: 'desc' }"
								show-checkbox
								icon="ele-Menu"
								highlight-current
							>
								<template #default="{ node, data }">
									<span v-if="node.level == 1 || node.level == 2">{{ node.label }}</span>
									<span v-if="node.level == 3">
										<el-icon style="margin-right: 3px; display: inline; vertical-align: middle"><ele-Link /></el-icon>
										{{ node.label }} 【{{ node.key }}】
										<el-tag type="primary" v-if="data.httpMethod === 'GET'">GET</el-tag>
										<el-tag type="success" v-else-if="data.httpMethod === 'POST'">POST</el-tag>
										<el-tag type="warning" v-else-if="data.httpMethod === 'PUT'">PUT</el-tag>
										<el-tag type="danger" v-else-if="data.httpMethod === 'DELETE'">DELETE</el-tag>
										<el-tag type="info" v-else>{{ data.httpMethod }}</el-tag>
									</span>
								</template>
							</el-tree>
						</el-col>
					</el-row>
				</el-form-item>
			</el-card>
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
import { reactive, onMounted, ref, watch } from 'vue';
import type { ElTree } from 'element-plus';
import NoticeBar from '/@/components/noticeBar/index.vue';
import { useI18n } from 'vue-i18n';

import { getAPI } from '/@/utils/axios-utils';
import { SysCommonApi, SysRoleApi } from '/@/api-services/system/api';

const { t } = useI18n();
const filterText = ref('');
const treeRef = ref<InstanceType<typeof ElTree>>();
const state = reactive({
	loading: false,
	isVisible: false,
	allApiData: [] as any,
	ownApiList: [] as any,
	roleId: 0,
	selectRow: [] as any,
	drawerTitle: '',
	selectedTabName: 0,
});

// 页面初始化
onMounted(() => {
	initTreeData();
});

// 节点过滤
watch(filterText, (val) => {
	treeRef.value!.filter(val);
});

// 初始化树
const initTreeData = async () => {
	state.loading = true;
	var res = await getAPI(SysCommonApi).apiSysCommonAllSysApiListGet();
	var tData = res.data.result ?? [];
	treeRef.value?.setCheckedKeys([]); // 清空选中值
	state.allApiData = tData;
	state.loading = false;
};

// 打开页面
const openDrawer = async (row: any) => {
	state.selectedTabName = 0;
	state.roleId = row.id;
	state.drawerTitle = t('message.list.setApiBlacklist', { name: row.name });

	state.loading = true;
	// 获取已有接口资源
	var res1 = await getAPI(SysRoleApi).apiSysRoleRoleApiListGet(state.roleId);
	state.ownApiList = res1.data.result ?? [];

	setTimeout(() => {
		treeRef.value?.setCheckedKeys(state.ownApiList ?? []);
	}, 200);

	state.loading = false;
	state.isVisible = true;
};

// 节点过滤
const filterNode = (value: string, data: any) => {
	if (!value) return true;
	return data.route.includes(value);
};

// 取消
const cancel = () => {
	state.isVisible = false;
};

// 授权角色接口资源
const submit = async () => {
	state.ownApiList = treeRef.value?.getCheckedKeys(true) as Array<string>;
	await getAPI(SysRoleApi).apiSysRoleGrantApiPost({ id: state.roleId, apiList: state.ownApiList });
	cancel();
};

// 导出对象
defineExpose({ openDrawer });
</script>
