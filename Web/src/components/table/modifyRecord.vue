<template>
	<el-popover placement="bottom" width="300" trigger="hover">
		<template #reference>
			<el-text type="primary" class="cursor-default">
				<el-icon><ele-InfoFilled /></el-icon>{{ t('message.list.detail') }}
			</el-text>
		</template>
		<el-descriptions direction="vertical" :column="2" border>
			<el-descriptions-item width="140" v-if="'createUserName' in props.data">
				<template #label>
					<el-text truncated class="one-line">
						<el-icon><ele-UserFilled /></el-icon>{{ t('message.list.creator') }}
					</el-text>
				</template>
				<el-tag>{{ props.data.createUserName ?? t('message.list.none') }}</el-tag>
			</el-descriptions-item>
			<el-descriptions-item v-if="'createTime' in props.data">
				<template #label>
					<el-text truncated class="one-line">
						<el-icon><ele-Calendar /></el-icon>{{ t('message.list.createTime') }}
					</el-text>
				</template>
				<el-tag>{{ props.data.createTime ?? t('message.list.none') }}</el-tag>
			</el-descriptions-item>
			<el-descriptions-item v-if="'updateUserName' in props.data">
				<template #label>
					<el-text truncated class="one-line">
						<el-icon><ele-UserFilled /></el-icon>{{ t('message.list.modifier') }}
					</el-text>
				</template>
				<el-tag>{{ props.data.updateUserName ?? t('message.list.none') }}</el-tag>
			</el-descriptions-item>
			<el-descriptions-item v-if="'updateTime' in props.data">
				<template #label>
					<el-text truncated class="one-line">
						<el-icon><ele-Calendar /></el-icon>{{ t('message.list.modifyTime') }}
					</el-text>
				</template>
				<el-tag>{{ props.data.updateTime ?? t('message.list.none') }}</el-tag>
			</el-descriptions-item>

			<el-descriptions-item v-if="'createOrgName' in props.data">
				<template #label>
					<el-text truncated class="one-line">
						<el-icon><ele-OfficeBuilding /></el-icon>{{ t('message.list.organizationName') }}
					</el-text>
				</template>
				<el-tag>{{ props.data.createOrgName ?? t('message.list.none') }}</el-tag>
			</el-descriptions-item>
			<el-descriptions-item v-if="'tenantId' in props.data">
				<template #label>
					<el-text truncated class="one-line">
						<el-icon><ele-School /></el-icon>{{ t('message.list.tenantName') }}
					</el-text>
				</template>
				<el-tag>{{ props.data.tenantId ?? t('message.list.none') }}</el-tag>
			</el-descriptions-item>

			<el-descriptions-item v-if="'remark' in props.data">
				<template #label>
					<el-text truncated class="one-line">
						<el-icon><ele-Tickets /></el-icon>{{ t('message.list.remark') }}
					</el-text>
				</template>
				{{ props.data.remark ?? t('message.list.none') }}
			</el-descriptions-item>
		</el-descriptions>
	</el-popover>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
const props = defineProps<{
	data: ModifyRecord;
}>();
</script>

<style scoped lang="scss">
.one-line {
	width: 120px;
	display: inline-block;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}
</style>
