<template>
	<div class="sys-onlineUser-container">
		<el-drawer v-model="state.isVisible" size="40%">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-UserFilled /> </el-icon>
					<span> {{ $t('message.list.onlineUserList') }} </span>
				</div>
			</template>

			<el-card class="full-table" shadow="hover" style="margin-top: 5px">
				<vxe-grid ref="xGrid" class="xGrid-style" v-bind="options">
					<template #toolbar_buttons> </template>
					<template #toolbar_tools> </template>
					<template #empty>
						<el-empty :image-size="200" />
					</template>
					<template #row_buttons="{ row }">
						<el-tooltip :content="$t('message.list.sendMessage')" placement="top">
							<el-button icon="ele-Position" text type="primary" @click="openSendMessage(row)"> </el-button>
						</el-tooltip>
						<el-tooltip :content="$t('message.list.forceOffline')" placement="top">
							<el-button icon="ele-CircleCloseFilled" text type="danger" v-auth="'sysOnlineUser/forceOffline'" @click="forceOffline(row)"> </el-button>
						</el-tooltip>
					</template>
				</vxe-grid>
			</el-card>
		</el-drawer>

		<SendMessage ref="sendMessageRef" :title="$t('message.list.sendMessage')" />
	</div>
</template>

<!-- 在线用户 -->
<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';
import { ElMessage, ElMessageBox, ElNotification } from 'element-plus';
import { VxeGridInstance } from 'vxe-table';
import { useVxeTable } from '/@/hooks/useVxeTableOptionsHook';
import { useThemeConfig } from '/@/stores/themeConfig';
import { storeToRefs } from 'pinia';
import { useI18n } from 'vue-i18n';
import { signalR } from './signalR';

import SendMessage from '/@/views/system/onlineUser/component/sendMessage.vue';

import { getAPI, clearAccessTokens } from '/@/utils/axios-utils';
import { SysAuthApi, SysOnlineUserApi } from '/@/api-services/system/api';
import { OnlineUser } from '/@/api-services/system/models';

const storesThemeConfig = useThemeConfig();
const { themeConfig } = storeToRefs(storesThemeConfig);
const { t } = useI18n();
const xGrid = ref<VxeGridInstance>();
const sendMessageRef = ref<InstanceType<typeof SendMessage>>();
const state = reactive({
	isVisible: false,
});

// 表格参数配置
const options = useVxeTable<OnlineUser>(
	{
		id: 'sysOnlineUser',
		name: t('message.list.onlineUserList'),
		columns: [
			// { type: 'checkbox', width: 40, fixed: 'left' },
			{ type: 'seq', title: t('message.list.seq'), width: 50, fixed: 'left' },
			{ field: 'userName', title: t('message.list.account'), minWidth: 110, showOverflow: 'tooltip' },
			{ field: 'realName', title: t('message.list.realName'), minWidth: 110, showOverflow: 'tooltip' },
			{ field: 'time', title: t('message.list.loginTime'), minWidth: 120, showOverflow: 'tooltip' },
			{ field: 'ip', title: t('message.list.ipAddress'), minWidth: 100, showOverflow: 'tooltip' },
			{ field: 'browser', title: t('message.list.browser'), minWidth: 160, showOverflow: 'tooltip' },
			// { field: 'connectionId', title: '连接Id', minWidth: 160, showOverflow: 'tooltip', sortable: true },
			{ field: 'buttons', title: t('message.list.operation'), fixed: 'right', width: 100, showOverflow: true, slots: { default: 'row_buttons' } },
		],
	},
	// vxeGrid配置参数(此处可覆写任何参数)，参考vxe-table官方文档
	{
		// 代理配置
		proxyConfig: { enabled: false },
		// 分页配置
		pagerConfig: { enabled: false },
		// 工具栏配置
		toolbarConfig: { enabled: false },
	}
);

// 页面初始化
onMounted(async () => {
	// 监听用户在线列表
	signalR.on('OnlineUserList', async (data: any) => {
		options.data = data.userList;

		// 上下线通知
		if (!themeConfig.value.onlineNotice) return;
		ElNotification({
			title: '提示',
			message: `${data.online ? `【${data.realName}】上线了` : `【${data.realName}】离开了`}`,
			// ★ 原来是 `type: `${data.online ? 'info' : 'error'}`,`。
			//   模板字符串会把 `'info' | 'error'` **拓宽成 string**，于是整个对象字面量
			//   不满足 NotificationOptions（type 要求字面量联合）→ TS 只好去匹配
			//   `ElNotification(options: string | VNode)` 那个重载 → 报出
			//   「'title' does not exist in type 'VNode'」这种**指错方向**的错误。
			//   去掉模板字符串，TS 推断出字面量联合，重载与类型同时正确。
			type: data.online ? 'info' : 'error',
			position: 'bottom-right',
		});
	});

	// 监听用户强制下线
	signalR.on('ForceOffline', async (data: any) => {
		await signalR.stop();

		await getAPI(SysAuthApi).apiSysAuthLogoutPost();
		clearAccessTokens();
	});
});

// 打开页面
const openDrawer = async () => {
	state.isVisible = true;

	// 获取用户在线列表
	var res = await getAPI(SysOnlineUserApi).apiSysOnlineUserOnlineUserListGet();
	options.data = res.data.result ?? [];
};

// 发送消息
const openSendMessage = (row: any) => {
	sendMessageRef.value?.openDialog(row);
};

// 强制下线
const forceOffline = async (row: any) => {
	ElMessageBox.confirm(t('message.list.confirmKickAccount', { account: row.realName }), t('message.list.hint'), {
		confirmButtonText: t('message.list.confirm'),
		cancelButtonText: t('message.list.cancelButtonText'),
		type: 'warning',
	})
		.then(async () => {
			// 强制用户下线（对应后台Hub的方法名称）
			await signalR.send('ForceOffline', { connectionId: row.connectionId }).catch(function (err: any) {
				ElMessage.error(err);
			});
		})
		.catch(() => {});
};

// 导出对象
defineExpose({ openDrawer });
</script>

<style lang="scss" scoped>
:deep(.el-drawer__body) {
	padding: 5px;
	display: flex;
	flex-direction: column;
	height: 100%;
}
.full-table {
	flex: 1;

	:deep(.el-card__body) {
		height: 100%;
		display: flex;
		flex-direction: column;
	}
}
</style>
