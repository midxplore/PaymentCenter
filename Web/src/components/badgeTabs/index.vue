<script setup lang="ts">
import { PropType, reactive, watch } from 'vue';

const emit = defineEmits(['update:modelValue', 'change']);

type BadgeTabsItem = {
	label: NullableString | NullableNumber; // 标签
	value: NullableString | NullableNumber; // 值
	count?: number; // 数量
	visible?: boolean; // 是否可见
	disabled?: boolean; // 是否禁用
};

/**
 * 组件属性定义
 */
const props = defineProps({
	/**
	 * 绑定值
	 * @type {string|number|boolean}
	 * @required
	 * @example
	 * <BadgeTabs v-model="state.active" :data="[
	 * 			{ label: '履约中', value: 10, count: state.total?.ly ?? 0 },
	 * 			{ label: '解约申请', value: 20, count: state.total?.jysq ?? 0 },
	 * 			{ label: '解约审核', value: 21, count: state.total?.jysh ?? 0, visible: auth('xxx') },
	 * 			{ label: '已解约', value: 22, count: state.total?.yjy ?? 0 },
	 * 			{ label: '已超期', value: 11, count: state.total?.ycq ?? 0 },
	 * 			{ label: '已失效', value: 199, count: state.total?.ysx ?? 0 },
	 * 		]" @@change="handleQuery(true)">
	 * 		<el-table ref="tableRef" :data="state.tableData">
	 * 			 <el-table-column label="序号" width="50"></el-table-column>
	 * 			 <el-table-column label="合同编号"></el-table-column>
	 * 		</el-table>
	 * </BadgeTabs>
	 */
	modelValue: {
		type: [String, Number, Boolean],
		required: true,
	},
	/**
	 * 字典编码，用于从字典中获取数据
	 * @type {string}
	 * @required
	 * @default []
	 * @example []
	 */
	data: {
		type: Array as PropType<BadgeTabsItem[]>,
		required: true,
		default: [],
	},
	/**
	 * badge 最大值
	 * @type {number}
	 * @default 99
	 * @example 99
	 */
	max: {
		type: Number,
		default: 99,
	},
	/**
	 * badge 的偏移量
	 * @type [int, int]
	 * @default [5, -2]
	 * @example [5, -2]
	 */
	offset: {
		type: Array as unknown as PropType<[number, number]>,
		default: [5, -2] as const,
	},
	/**
	 * 隐藏小于等于0的badge
	 * @type {boolean}
	 * @default true
	 * @example true
	 */
	hideZero: {
		type: Boolean,
		default: true,
	},
});

const state = reactive({
	active: props.modelValue,
	data: props.data,
});

watch(
	() => state.active,
	(newValue) => {
		emit('update:modelValue', newValue);
		emit('change', newValue, state.data);
	},
	{ immediate: true }
);
watch(
	() => props.data,
	(newValue) => {
		state.data = newValue ?? [];
	},
	{ immediate: true }
);
</script>

<template>
	<el-tabs v-model="state.active" class="ml5 mt5 container" v-bind="$attrs">
		<el-tab-pane v-for="(item, index) in state.data" :key="index" :label="item.label" :name="item.value" :disabled="item.disabled" v-show="item.visible === undefined || item.visible">
			<template #label v-if="(props.hideZero && item.count) || !props.hideZero">
				<el-badge :value="item.count ?? 0" :max="props.max" :offset="props.offset">{{ item.label }}</el-badge>
			</template>
		</el-tab-pane>
		<slot></slot>
	</el-tabs>
</template>

<style scoped lang="scss">
.container {
	height: 100%;
	display: flex;
	flex-direction: column;
}
</style>
