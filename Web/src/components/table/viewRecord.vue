<template>
	<el-dialog v-model="state.dialogVisible" draggable :close-on-click-modal="false">
		<template #header>
			<div style="color: #fff">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-View /> </el-icon>
				<span>{{ props.title }}</span>
			</div>
		</template>
		<el-descriptions :column="descColumn" border>
			<el-descriptions-item v-for="(item, key) in state.data" :key="key" :label="key" :width="state.descriptionsWidth">
				<template #label>
					<el-text v-if="String(key) === t('message.list.creator')">
						<el-icon><ele-UserFilled /></el-icon>
						{{ key }}
					</el-text>
					<el-text v-else-if="String(key) === t('message.list.createTime')">
						<el-icon><ele-Calendar /></el-icon>
						{{ key }}
					</el-text>
					<el-text v-else-if="String(key) === t('message.list.modifier')">
						<el-icon><ele-UserFilled /></el-icon>
						{{ key }}
					</el-text>
					<el-text v-else-if="String(key) === t('message.list.modifyTime')">
						<el-icon><ele-Calendar /></el-icon>
						{{ key }}
					</el-text>
					<el-text v-else-if="String(key) === t('message.list.remark')">
						<el-icon><ele-Tickets /></el-icon>
						{{ key }}
					</el-text>
					<el-text v-else>
						{{ key }}
					</el-text>
				</template>
				<template v-if="item === false || item === true">
					<span>{{ item === false ? '否' : '是' }}</span>
				</template>
				<span v-else>{{ item }}</span>
			</el-descriptions-item>
		</el-descriptions>
	</el-dialog>
</template>

<script lang="ts" setup>
import { computed, reactive, onMounted, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
const props = defineProps({
	title: String,
});

const state = reactive({
	dialogVisible: false,
	data: {} as any,
	windowWidth: window.innerWidth,
	descriptionsWidth: '',
});

const handleResize = () => {
	state.windowWidth = window.innerWidth;
};

onMounted(() => {
	window.addEventListener('resize', handleResize);
});

onUnmounted(() => {
	window.removeEventListener('resize', handleResize);
});

// 打开弹框
const openDialog = (row: any, columns: any) => {
	state.data = {};
	state.dialogVisible = true;

	// 先处理列数据
	const columnsData = columns.reduce((acc: any, item: any) => {
		if (item.field !== 'seq' && item.field !== 'record' && item.field !== 'buttons') {
			acc[item.title] = row[item.field];
		}
		return acc;
	}, {});

	// 后合并固定字段（后者覆盖前者）
	const fixedFields = {
		创建者: row.createUserName,
		创建时间: row.createTime,
		修改者: row.updateUserName,
		修改时间: row.updateTime,
		...(row.remark && { 备注: row.remark }),
	};

	state.data = { ...columnsData, ...fixedFields };
};

// 计算列宽和个数
const descColumn = computed(() => {
	if (state.windowWidth < 800) {
		state.descriptionsWidth = '50%';
		return 1;
	}
	if (state.windowWidth < 1000) {
		state.descriptionsWidth = '25%';
		return 2;
	}
	state.descriptionsWidth = '10%';
	return 3;
});

defineExpose({
	openDialog,
});
</script>
