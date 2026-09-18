<template>
	<el-row :gutter="10">
		<el-col :span="10">
			<div class="transfer-panel">
				<p class="transfer-panel__header">
					<el-checkbox v-model="state.leftAllChecked" :indeterminate="leftIndeterminate" :validate-event="false" @change="handleLeftAllChecked"> {{ props.leftTitle }} </el-checkbox>
					<span>{{ state.leftChecked.length }}/{{ props.leftData.length }}</span>
				</p>
				<div class="transfer-panel__body">
					<el-input class="transfer-panel__filter" v-model="state.leftKeyword" placeholder="搜索" prefix-icon="ele-Search" clearable :validate-event="false" />
					<el-checkbox-group v-show="true" v-model="state.leftChecked" :validate-event="false" class="transfer-panel__list">
						<el-checkbox
							v-for="(item, index) in leftFilterData"
							:key="index"
							:value="item[props.options.value]"
							:label="item[props.options.label]"
							:disabled="item[props.options.disabled]"
							:validate-event="false"
							class="transfer-panel__item"
							@dblclick="dbClickToRight(item)"
						>
						</el-checkbox>
					</el-checkbox-group>
				</div>
			</div>
		</el-col>
		<el-col :span="4" class="transfer-buttons">
			<div class="transfer-buttons__item">
				<el-button type="primary" style="" icon="ele-ArrowRight" @click="toRight"></el-button>
			</div>
			<div class="transfer-buttons__item">
				<el-button type="primary" style="" icon="ele-ArrowLeft" @click="toLeft"></el-button>
			</div>
			<div class="transfer-buttons__item">
				<el-button type="primary" style="" icon="ele-DArrowRight" @click="allToRight"></el-button>
			</div>
			<div class="transfer-buttons__item">
				<el-button type="primary" style="" icon="ele-DArrowLeft" @click="allToLeft"></el-button>
			</div>
		</el-col>
		<el-col :span="10">
			<div class="transfer-panel">
				<p class="transfer-panel__header">
					<el-checkbox v-model="state.rightAllChecked" :indeterminate="rightIndeterminate" :validate-event="false" @change="handleRightAllChecked"> {{ props.rightTitle }} </el-checkbox>
					<span>{{ state.rightChecked.length }}/{{ props.rightData.length }}</span>
				</p>
				<div class="transfer-panel__body">
					<el-input class="transfer-panel__filter" v-model="state.rightKeyword" placeholder="搜索" prefix-icon="ele-Search" clearable :validate-event="false" />
					<el-checkbox-group v-show="true" v-model="state.rightChecked" :validate-event="false" class="transfer-panel__list">
						<el-checkbox
							v-for="(item, index) in rightFilterData"
							:key="index"
							:value="item[props.options.value]"
							:label="item[props.options.label]"
							:disabled="item[props.options.disabled]"
							:validate-event="false"
							class="transfer-panel__item"
							@dblclick="dbClickToLeft(item)"
						>
						</el-checkbox>
					</el-checkbox-group>
				</div>
			</div>
		</el-col>
	</el-row>
</template>

<script lang="ts" setup name="transfer">
import { watch, reactive, computed } from 'vue';

const props = defineProps({
	leftTitle: String,
	rightTitle: String,
	options: {
		type: Object,
		default: () => ({
			value: 'id',
			label: 'name',
			disabled: 'disabled',
		}),
	},
	leftData: { type: Array, default: () => [] }, // 左边全部数据
	rightData: { type: Array, default: () => [] }, // 右边全部数据
});

const emits = defineEmits(['left', 'right', 'allLeft', 'allRight', 'update:leftData', 'update:rightData']);

const state = reactive({
	leftAllChecked: false, // 左边是否全选
	leftKeyword: '', // 左边搜索关键词
	leftChecked: [] as any, // 左边选中数据
	rightAllChecked: false, // 右边是否全选
	rightKeyword: '', // 右边搜索关键词
	rightChecked: [] as any, // 右边选中数据
});

// 过滤左边数据
const leftFilterData: any = computed(() => {
	let result = props.leftData.filter((e: any) => e[props.options.label].toLowerCase().includes(state.leftKeyword.toLowerCase()));
	if (state.leftChecked.length > 0) {
		for (let i = state.leftChecked.length - 1; i >= 0; i--) {
			const index = result.findIndex((e: any) => e[props.options.value] == state.leftChecked[i]);
			// eslint-disable-next-line vue/no-side-effects-in-computed-properties
			if (index == -1) state.leftChecked.splice(i, 1);
		}
	}
	return result;
});

// 左边数据全选
const handleLeftAllChecked = (value: any) => {
	state.leftChecked = value ? leftFilterData.value.filter((e: any) => e[props.options.disabled] == false).map((e: any) => e[props.options.value]) : [];
};

// 左边数据半选状态
const leftIndeterminate = computed(() => {
	const checkedLength = state.leftChecked.length;
	const result = checkedLength > 0 && checkedLength < leftFilterData.value.filter((e: any) => e[props.options.disabled] == false).length;
	return result;
});

watch(
	() => state.leftChecked,
	(val: any[]) => {
		state.leftAllChecked = val.length > 0 && val.length == leftFilterData.value.filter((e: any) => e[props.options.disabled] == false).length;
	}
);

// 右边过滤数据
const rightFilterData: any = computed(() => {
	let result = props.rightData.filter((e: any) => e[props.options.label].toLowerCase().includes(state.rightKeyword.toLowerCase()));
	if (state.rightChecked.length > 0) {
		for (let i = state.rightChecked.length - 1; i >= 0; i--) {
			const index = result.findIndex((e: any) => e[props.options.value] == state.rightChecked[i]);
			// eslint-disable-next-line vue/no-side-effects-in-computed-properties
			if (index == -1) state.rightChecked.splice(i, 1);
		}
	}
	return result;
});

// 右边数据全选
const handleRightAllChecked = (value: any) => {
	state.rightChecked = value ? rightFilterData.value.filter((e: any) => e[props.options.disabled] == false).map((e: any) => e[props.options.value]) : [];
};

// 右边数据半选状态
const rightIndeterminate = computed(() => {
	const checkedLength = state.rightChecked.length;
	const result = checkedLength > 0 && checkedLength < rightFilterData.value.filter((e: any) => e[props.options.disabled] == false).length;
	return result;
});

watch(
	() => state.rightChecked,
	(val: any[]) => {
		state.rightAllChecked = val.length > 0 && val.length == rightFilterData.value.filter((e: any) => e[props.options.disabled] == false).length;
	}
);

// 双击左边的变右边
const dbClickToRight = (item: any) => {
	if (item[props.options.value] && item[props.options.disabled] === false) {
		//取交集
		let adds = props.leftData.filter((e: any) => item[props.options.value] == e[props.options.value]);
		//取差集
		let cuts = props.leftData.filter((e: any) => item[props.options.value] != e[props.options.value]);
		emits('update:leftData', cuts);
		emits('update:rightData', props.rightData.concat(adds));
		emits('right');
		state.leftChecked = state.leftChecked.filter((e: any) => item[props.options.value] != e);
	}
};

// 左边中选中的变右边
const toRight = () => {
	if (state.leftChecked?.length > 0) {
		//取交集
		let adds = props.leftData.filter((e: any) => state.leftChecked.some((x: any) => x == e[props.options.value]));
		//取差集
		let cuts = props.leftData.filter((e: any) => state.leftChecked.every((x: any) => x != e[props.options.value]));
		emits('update:leftData', cuts);
		emits('update:rightData', props.rightData.concat(adds));
		emits('right');
		state.leftChecked = [];
	}
};

// 全部左边的变右边
const allToRight = () => {
	if (leftFilterData.value?.length > 0) {
		let temp = leftFilterData.value.filter((e: any) => e[props.options.disabled] == false);
		//取交集
		let adds = props.leftData.filter((e: any) => temp.some((x: any) => x[props.options.value] == e[props.options.value]));
		//取差集
		let cuts = props.leftData.filter((e: any) => temp.every((x: any) => x[props.options.value] != e[props.options.value]));
		emits('update:leftData', cuts);
		emits('update:rightData', props.rightData.concat(adds));
		emits('allRight');
		state.leftChecked = [];
	}
};

// 双击右边的变左边
const dbClickToLeft = (item: any) => {
	if (item[props.options.value] && item[props.options.disabled] === false) {
		//取交集
		let adds = props.rightData.filter((e: any) => item[props.options.value] == e[props.options.value]);
		//取差集
		let cuts = props.rightData.filter((e: any) => item[props.options.value] != e[props.options.value]);
		emits('update:leftData', props.leftData.concat(adds));
		emits('update:rightData', cuts);
		emits('left');
		state.rightChecked = state.rightChecked.filter((e: any) => item[props.options.value] != e);
	}
};

// 右边中选中的变左边
const toLeft = () => {
	if (state.rightChecked?.length > 0) {
		//取交集
		let adds = props.rightData.filter((e: any) => state.rightChecked.some((x: any) => x == e[props.options.value]));
		//取差集
		let cuts = props.rightData.filter((e: any) => state.rightChecked.every((x: any) => x != e[props.options.value]));
		emits('update:leftData', props.leftData.concat(adds));
		emits('update:rightData', cuts);
		emits('left');
		state.rightChecked = [];
	}
};

// 全部右边的变左边
const allToLeft = () => {
	if (rightFilterData.value?.length > 0) {
		let temp = rightFilterData.value.filter((e: any) => e[props.options.disabled] == false);
		//取交集
		let adds = props.rightData.filter((e: any) => temp.some((x: any) => x[props.options.value] == e[props.options.value]));
		//取差集
		let cuts = props.rightData.filter((e: any) => temp.every((x: any) => x[props.options.value] != e[props.options.value]));
		emits('update:leftData', props.leftData.concat(adds));
		emits('update:rightData', cuts);
		emits('allLeft');
		state.rightChecked = [];
	}
};
</script>

<style lang="scss" scoped>
.transfer-panel {
	overflow: hidden;
	display: inline-block;
	text-align: left;
	vertical-align: middle;
	width: 100%;
	max-height: 100%;
	box-sizing: border-box;
	position: relative;
	box-shadow: 0 0 0 1px var(--el-border-color, var(--el-border-color)) inset;
	&__header {
		width: 100%;
		display: flex;
		align-items: center;
		justify-content: space-between;
		flex-wrap: nowrap;
		// background: #f5f7fa;
		padding: 3px 6px;
		box-shadow: 0 0 0 1px var(--el-border-color, var(--el-border-color)) inset;
	}
	&__body {
		height: 400px;
		.transfer-panel__filter {
			padding: 6px;
		}
		.transfer-panel__list {
			overflow: auto;
			height: calc(100% - 36px);
			padding-top: 10px;
			.transfer-panel__item {
				display: block !important;
				padding-left: 6px;
			}
		}
	}
}
.transfer-buttons {
	display: flex;
	flex-direction: column;
	justify-content: center;
	align-items: center;
	text-align: center;
	&__item {
		padding-top: 10px;
		width: 100%;
	}
}
</style>
