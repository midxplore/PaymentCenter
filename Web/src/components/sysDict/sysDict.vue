<!--
  字典组件 - 根据字典编码渲染不同类型的数据展示控件（取值来自后端字典维护）

  功能特性：
  - 支持多种渲染方式：标签、下拉框、单选框、复选框、单选按钮组
  - 支持常量和普通字典
  - 支持多选和单选模式
  - 支持数组多选模式和逗号多选模式
  - 支持自定义显示格式和过滤
-->
<script lang="ts">
/**
 * 支持的渲染类型常量
 * @type {Array}
 * @constant
 */
const RENDER_TYPES = ['tag', 'select', 'radio', 'checkbox', 'checkbox-button', 'radio-button'] as const;
type RenderType = (typeof RENDER_TYPES)[number];

/**
 * 支持的标签类型常量
 * @type {Array}
 * @constant
 */
const TAG_TYPES = ['success', 'warning', 'info', 'primary', 'danger'] as const;
type TagType = (typeof TAG_TYPES)[number];

/**
 * 字典项数据结构
 * @interface
 * @property {string} [tagType] - 标签类型（当renderAs='tag'时生效）
 * @property {string} [styleSetting] - 自定义样式
 * @property {string} [classSetting] - 自定义类名
 * @property {string} [label] - 显示文本
 * @property {string|number} [value] - 值
 */
interface DictItem {
	[key: string]: any;
	tagType?: TagType;
	styleSetting?: string;
	classSetting?: string;
	disabled?: boolean;
	label?: string;
	value?: string | number;
}

/**
 * 互斥选项配置
 * @type {Object}
 * @property {string|number} value - 触发互斥的选项值
 * @property {Array<string|number>} excludes - 被互斥的选项值列表
 */
interface MutexConfig {
	value: string | number;
	excludes: (string | number)[];
}

/**
 * 多选值模式常量
 * @enum {string}
 * @property {string} Array - 数组模式，如['1','2','3']
 * @property {string} Comma - 逗号分隔模式，如'1,2,3'
 */
const MultipleModel = {
	Array: 'array',
	Comma: 'comma',
} as const;

// 多选值模式枚举类型
type MultipleModelType = (typeof MultipleModel)[keyof typeof MultipleModel];

/**
 * 检查是否为合法的渲染类型
 * @function
 * @param {any} value - 待检查的值
 * @returns {value is RenderType} - 是否为合法的渲染类型
 */
function isRenderType(value: any): value is RenderType {
	return RENDER_TYPES.includes(value);
}

/**
 * 检查是否为合法的多选模式
 * @function
 * @param {any} value - 待检查的值
 * @returns {value is MultipleModel} - 是否为合法的多选模式
 */
function isMultipleModel(value: any): value is MultipleModelType {
	return Object.values(MultipleModel).includes(value);
}
</script>

<script setup lang="ts">
import { reactive, watch, computed, PropType } from 'vue';
import { useUserInfo } from '/@/stores/userInfo';

const userStore = useUserInfo();
const emit = defineEmits(['update:modelValue', 'change']);

/**
 * 组件属性定义
 */
const props = defineProps({
	/**
	 * 绑定值，支持多种类型
	 * @type {string|number|boolean|Array|null}
	 * @required
	 * @example
	 * // 单选选择器
	 * <g-sys-dict v-model="selectedValue" code="gender" renderAs="select" />
	 *
	 * // 多选选择器（数组模式）
	 * <g-sys-dict v-model="selectedValues" code="roles" renderAs="select" multiple />
	 *
	 * // 多选选择器（逗号模式）
	 * <g-sys-dict v-model="selectedValues" code="roles" renderAs="select" multiple multiple-model="comma" />
	 */
	modelValue: {
		type: [String, Number, Boolean, Array, null] as PropType<string | number | boolean | any[] | Nullable>,
		default: null,
		required: true,
	},
	/**
	 * 字典编码，用于从字典中获取数据
	 * @type {string}
	 * @required
	 * @example 'gender'
	 */
	code: {
		type: String,
		required: false,
	},
	/**
	 * 直接传入的字典数据源（优先级高于code）
	 * @type {DictItem[]}
	 * @example [{ label: '选项1', value: '1' }, { label: '选项2', value: '2' }]
	 */
	data: {
		type: Array as PropType<DictItem[]>,
		default: () => [],
	},
	/**
	 * 是否为常量字典（true从常量列表获取，false从字典列表获取）
	 * @type {boolean}
	 * @default false
	 * @example true
	 */
	isConst: {
		type: Boolean,
		default: false,
	},
	/**
	 * 字典项中用于显示的字段名
	 * @type {string}
	 * @default 'label'
	 * @example 'name'
	 */
	propLabel: {
		type: String,
		default: 'label',
	},
	/**
	 * 字典项中用于取值的字段名
	 * @type {string}
	 * @default 'value'
	 * @example 'id'
	 */
	propValue: {
		type: String,
		default: 'value',
	},
	/**
	 * 字典项过滤函数
	 * @type {Function}
	 * @param {DictItem} dict - 当前字典项
	 * @returns {boolean} - 是否保留该项
	 * @default (dict) => true
	 * @example
	 * // 只显示启用的字典项
	 * :onItemFilter="(dict) => dict.status === 1"
	 */
	onItemFilter: {
		type: Function as PropType<(dict: DictItem) => boolean>,
		default: () => true,
	},
	/**
	 * 字典项显示内容格式化函数
	 * @type {Function}
	 * @param {DictItem} dict - 当前字典项
	 * @returns {string|undefined|null} - 格式化后的显示内容
	 * @default () => undefined
	 * @example
	 * // 在标签前添加图标
	 * :onItemFormatter="(dict) => `${dict.label} <icon-user />`"
	 */
	onItemFormatter: {
		type: Function as PropType<(dict: DictItem) => string | undefined | null>,
		default: () => undefined,
	},
	/**
	 * 组件渲染方式
	 * @type {'tag'|'select'|'radio'|'checkbox'|'radio-button'}
	 * @default 'tag'
	 * @example 'select'
	 */
	renderAs: {
		type: String as PropType<RenderType>,
		default: 'tag',
		validator: isRenderType,
	},
	/**
	 * 是否多选（仅在renderAs为select/checkbox时有效）
	 * @type {boolean}
	 * @default false
	 * @example true
	 */
	multiple: {
		type: Boolean,
		default: false,
	},
	/**
	 * 多选值模式（仅在multiple为true时有效）
	 * @type {'array'|'comma'}
	 * @default 'array'
	 * @example 'comma'
	 */
	multipleModel: {
		type: String as PropType<MultipleModelType>,
		default: MultipleModel.Array,
		validator: isMultipleModel,
	},
	/**
	 * 互斥配置项（仅在多选模式下有效）
	 * @type {Array<MutexConfig>}
	 * @example
	 * :mutex-configs="[
	 *   { value: 'all', excludes: ['1', '2', '3'] },
	 *   { value: '1', excludes: ['all'] }
	 * ]"
	 */
	mutexConfigs: {
		type: Array as PropType<MutexConfig[]>,
		default: () => [],
	},
	/**
	 * 单选框是否支持取消选择
	 * @type {Boolean}
	 * @example
	 */
	radioCancelable: {
		type: Boolean,
		default: true,
	},
});

/**
 * 组件状态
 * @property {DictItem[]} dictData - 原始字典数据
 * @property {any} value - 当前值
 */
const state = reactive({
	dictData: [] as DictItem[],
	value: props.modelValue,
	conversion: false,
});

/**
 * 格式化后的字典数据（计算属性）
 * @computed
 * @returns {DictItem[]} - 过滤并格式化后的字典数据
 */
const formattedDictData = computed(() => {
	const baseData = state.dictData.filter(props.onItemFilter).map((item) => ({
		...item,
		label: item[props.propLabel],
		value: item[props.propValue],
	}));

	// 如果没有互斥配置或多选模式，直接返回基础数据
	if (!props.multiple || !props.mutexConfigs || props.mutexConfigs.length === 0) {
		return baseData;
	}

	// 处理互斥逻辑，设置禁用状态
	return baseData.map((item) => {
		// 检查当前项是否应该被禁用
		const isDisabled = isItemDisabled(item.value, state.value, props.mutexConfigs);
		return {
			...item,
			disabled: isDisabled || item.disabled, // 保持原有的disabled状态
		};
	});
});

/**
 * 当前选中的字典项（计算属性）
 * @computed
 * @returns {DictItem|DictItem[]|null} - 当前选中的字典项或字典项数组
 */
const currentDictItems = computed(() => {
	// 更严谨的空值判断，0 不算空
	const isEmpty = (val: any) => val === null || val === undefined || (Array.isArray(val) && val.length === 0) || (typeof val === 'string' && val.trim() === '');

	if (isEmpty(state.value)) return null;

	let values: any[] = [];

	if (props.multiple) {
		if (Array.isArray(state.value)) {
			values = state.value;
		} else if (typeof state.value === 'string' && props.renderAs === 'tag') {
			values = state.value.split(',').filter((v) => v !== '');
		} else if (state.value !== undefined && state.value !== null && state.value !== '') {
			values = [state.value];
		}
		console.log('[g-sys-dict] 解析多选值:', state.value, values);
		// 去重并类型兼容
		const uniqueValues = [...new Set(values)];
		return formattedDictData.value.filter((item) => uniqueValues.some((val) => val == item.value));
	}

	// 单选时类型兼容
	return formattedDictData.value.find((item) => item.value == state.value) || null;
});

/**
 * 获取字典数据列表
 * @function
 * @returns {DictItem[]} - 字典数据列表
 * @throws {Error} - 获取数据失败时抛出错误
 */
const getDataList = (): DictItem[] => {
	try {
		// 如果提供了 data 数据源，优先使用
		if (props.data && props.data.length > 0) {
			return props.data.map((item: any) => ({
				...item,
				label: item[props.propLabel] ?? [item.name, item.desc].filter((x) => x).join('-'),
				value: item[props.propValue] ?? item.code,
			}));
		}

		if (!props.code) {
			console.error('[g-sys-dict] code和data不能同时为空');
			return [];
		}

		const source = props.isConst ? userStore.constList : userStore.dictList;
		const data = props.isConst ? (source?.find((x: any) => x.code === props.code)?.data?.result ?? []) : (source[props.code] ?? []);
		data.sort((a: number, b: number) => a - b);

		return data.map((item: any) => ({
			...item,
			label: item[props.propLabel] ?? [item.name, item.desc].filter((x) => x).join('-'),
			value: item[props.propValue] ?? item.code,
		}));
	} catch (error) {
		console.error(`[g-sys-dict] 获取字典[${props.code}]数据失败:`, error);
		return [];
	}
};

/**
 * 处理数字类型的值
 * @function
 * @param {any} value - 待处理的值
 */
const processNumericValues = (value: any) => {
	if (typeof value === 'number' || (Array.isArray(value) && typeof value[0] === 'number')) {
		state.dictData.forEach((item) => {
			if (item.value) {
				item.value = Number(item.value);
			}
		});
	}
};

/**
 * 解析多选值（修复逗号模式问题）
 * @function
 * @param {any} value - 待解析的值
 * @returns {any} - 解析后的值
 */
const parseMultipleValue = (value: any): any => {
	// 处理空值情况
	if (value === null || value === undefined || value === '') {
		return props.multiple ? [] : value;
	}

	// 多选且字符串，自动按逗号分割
	if (props.multiple && typeof value === 'string') {
		if (value.trim().startsWith('[') && value.trim().endsWith(']')) {
			try {
				return JSON.parse(value.trim());
			} catch {
				return [];
			}
		}
		// 只要是多选+字符串，默认逗号分割
		return value
			.split(',')
			.map((v) => v.trim())
			.filter((v) => v !== '');
	}

	// 处理数字
	if (typeof value === 'number' && !state.conversion) {
		try {
			state.dictData.forEach((item) => {
				if (item.value) item.value = Number(item.value);
			});
			state.conversion = true;
		} catch (error) {
			console.warn('[g-sys-dict] 数字转换失败:', error);
		}
	}

	// 其他情况直接返回
	return value;
};

/**
 * 检查选项是否应该被禁用
 * @function
 * @param {string|number} itemValue - 当前选项的值
 * @param {any} currentValue - 当前选中的值
 * @param {MutexConfig[]} mutexConfigs - 互斥配置列表
 * @returns {boolean} - 是否应该禁用
 */
const isItemDisabled = (itemValue: string | number, currentValue: any, mutexConfigs: MutexConfig[]): boolean => {
	// 如果没有配置互斥规则，不禁用任何项
	if (!mutexConfigs || mutexConfigs.length === 0) {
		return false;
	}

	// 获取当前选中的值数组
	const selectedValues = Array.isArray(currentValue) ? currentValue : currentValue ? [currentValue] : [];

	// 检查每个互斥配置
	for (const config of mutexConfigs) {
		// 如果互斥触发项已被选中，且当前项是被互斥项，则禁用
		if (selectedValues.includes(config.value) && config.excludes.includes(itemValue)) {
			return true;
		}

		// 如果当前项是互斥触发项，且有被互斥项被选中，则禁用
		if (itemValue == config.value && config.excludes.some((exclude) => selectedValues.includes(exclude))) {
			return true;
		}
	}

	return false;
};

/**
 * 互斥处理函数
 * @function
 * @param {any} newValue - 新选中的值
 * @param {MutexConfig[]} mutexConfigs - 互斥配置列表
 * @returns {any} - 处理后的值
 */
const handleMutex = (newValue: any, mutexConfigs: MutexConfig[]): any => {
	// 如果没有配置互斥规则，直接返回原值
	if (!mutexConfigs || mutexConfigs.length === 0) return newValue;

	// 如果是单选模式，直接返回
	if (!props.multiple) return newValue;

	// 对于禁用模式，我们只需要确保新值是有效的（即没有违反互斥规则）
	// 实际的禁用逻辑在formattedDictData中处理
	let resultValue = Array.isArray(newValue) ? [...newValue] : newValue ? [newValue] : [];

	// 过滤掉无效的值（可能由于异步更新导致的无效选择）
	const validValues = formattedDictData.value.filter((item) => !item.disabled).map((item) => item.value);

	return resultValue.filter((val) => validValues.includes(val));
};

/**
 * 更新绑定值（修复逗号模式问题）
 * @function
 * @param {any} newValue - 新值
 */
const updateValue = (newValue: any) => {
	// 先解析为数组（逗号模式下传入可能是字符串）
	let processedValue = Array.isArray(newValue) ? newValue : typeof newValue === 'string' && props.multipleModel === MultipleModel.Comma ? newValue.split(',').filter(Boolean) : newValue;

	// 处理互斥逻辑
	if (props.mutexConfigs && props.mutexConfigs.length > 0) {
		processedValue = handleMutex(processedValue, props.mutexConfigs);
	}

	let emitValue = processedValue;
	if (props.multipleModel === MultipleModel.Comma) {
		if (Array.isArray(processedValue)) {
			emitValue = processedValue.length > 0 ? processedValue.sort().join(',') : '';
		} else if (processedValue === null || processedValue === undefined) {
			emitValue = undefined;
		}
	} else {
		if (Array.isArray(processedValue)) {
			emitValue = processedValue.length > 0 ? processedValue.sort() : [];
		} else if (processedValue === null || processedValue === undefined) {
			emitValue = undefined;
		}
	}
	console.log('[g-sys-dict] 更新值:', { newValue, processedValue, emitValue });

	state.value = processedValue;
	emit('update:modelValue', emitValue === '' || emitValue?.length === 0 ? undefined : emitValue);
	emit('change', state.value, currentDictItems, state.dictData);
};

/**
 * 单选框支持取消选择
 * @function
 * @param {any} newValue - 新值
 * @param {any} isDisabled - 是否禁用
 */
const radioClick = (newValue: any, isDisabled: boolean) => {
	if (isDisabled) return;
	if (newValue === state.value && props.radioCancelable) {
		state.value = null;
		newValue = undefined;
	} else {
		state.value = newValue;
	}
	updateValue(newValue);
};

/**
 * 确保标签类型存在
 * @function
 * @param {DictItem} item - 字典项
 * @returns {TagType} - 合法的标签类型
 */
const ensureTagType = (item: DictItem): TagType => {
	return TAG_TYPES.includes(item.tagType as TagType) ? (item.tagType as TagType) : 'primary';
};

/**
 * 计算显示的文本
 * @function
 * @param {DictItem} [dict] - 字典项
 * @returns {string} - 显示文本
 */
const getDisplayText = (dict?: DictItem): string => {
	if (!dict) return String(state.value || '');
	const formattedText = props.onItemFormatter?.(dict);
	return formattedText ?? dict[props.propLabel] ?? '';
};

/**
 * 初始化数据
 * @function
 */
const initData = () => {
	// 验证 code 和 data 不能同时为空
	if (!props.code && (!props.data || props.data.length === 0)) {
		console.error('[g-sys-dict] code和data不能同时为空');
		state.dictData = [];
		state.value = props.multiple ? [] : null;
		return;
	}

	state.dictData = getDataList();
	processNumericValues(props.modelValue);
	const initialValue = parseMultipleValue(props.modelValue);
	if (initialValue !== state.value) {
		state.value = initialValue;
	}
};

/**
 * 校验初始值对应的选项是否存在
 * @function
 */
const validateInitialValue = () => {
	return new Promise((resolve, reject) => {
		if (props.renderAs === 'tag' || !state.value) return resolve(undefined);
		if (Array.isArray(state.value)) {
			const errorValues = state.value.filter((val) => state.dictData.find((e) => e[props.propValue] == val) === undefined);
			if (errorValues && errorValues.length > 0) {
				reject(`[g-sys-dict] 未匹配到选项值：${JSON.stringify(errorValues)}`);
			}
		} else if (state.value) {
			if (!state.dictData.find((e) => e[props.propValue] == state.value)) {
				reject(`[g-sys-dict] 未匹配到选项值：${state.value}`);
			}
		}
		resolve(undefined);
	});
};

// 监听数据变化
watch(
	() => props.modelValue,
	(newValue) => {
		state.value = parseMultipleValue(newValue);
		validateInitialValue();
	}
);
watch(() => [userStore.dictList, userStore.constList, props.data, state], initData, { immediate: true });
</script>

<template>
	<!-- 渲染标签 -->
	<template v-if="props.renderAs === 'tag'">
		<template v-if="Array.isArray(currentDictItems)">
			<el-tag v-for="(item, index) in currentDictItems" :key="index" v-bind="$attrs" :type="ensureTagType(item)" :style="item.styleSetting" :class="item.classSetting" class="mr2">
				{{ getDisplayText(item) }}
			</el-tag>
		</template>
		<template v-else>
			<el-tag v-if="currentDictItems" v-bind="$attrs" :type="ensureTagType(currentDictItems)" :style="currentDictItems.styleSetting" :class="currentDictItems.classSetting">
				{{ getDisplayText(currentDictItems) }}
			</el-tag>
			<span v-else>{{ getDisplayText() }}</span>
		</template>
	</template>

	<!-- 渲染选择器 -->
	<el-select v-else-if="props.renderAs === 'select'" v-model="state.value" v-bind="$attrs" :multiple="props.multiple" @change="updateValue" filterable allow-create default-first-option clearable>
		<el-option v-for="(item, index) in formattedDictData" :key="index" :label="getDisplayText(item)" :value="item.value" :disabled="item.disabled" />
		<slot />
	</el-select>

	<!-- 多选框（多选） -->
	<el-checkbox-group v-else-if="props.renderAs === 'checkbox'" v-model="state.value" v-bind="$attrs" @change="updateValue" class="g-sys-dict-group">
		<el-checkbox v-for="(item, index) in formattedDictData" :key="index" :value="item.value" :label="getDisplayText(item)" :disabled="item.disabled" />
		<span v-if="$slots.default" class="g-sys-dict-inline-slot">
			<slot />
		</span>
	</el-checkbox-group>

	<!-- 多选框-按钮（多选） -->
	<el-checkbox-group v-else-if="props.renderAs === 'checkbox-button'" v-model="state.value" v-bind="$attrs" @change="updateValue" class="g-sys-dict-group">
		<el-checkbox-button v-for="(item, index) in formattedDictData" :key="index" :value="item.value" :disabled="item.disabled">
			{{ getDisplayText(item) }}
		</el-checkbox-button>
		<span v-if="$slots.default" class="g-sys-dict-inline-slot">
			<slot />
		</span>
	</el-checkbox-group>

	<!-- 渲染单选框 -->
	<el-radio-group v-else-if="props.renderAs === 'radio'" v-model="state.value" v-bind="$attrs" class="g-sys-dict-group">
		<el-radio v-for="(item, index) in formattedDictData" :key="index" :value="item.value" @click.prevent="radioClick(item.value, Boolean($attrs.disabled))">
			{{ getDisplayText(item) }}
		</el-radio>
		<span v-if="$slots.default" class="g-sys-dict-inline-slot">
			<slot />
		</span>
	</el-radio-group>

	<!-- 渲染单选框按钮 -->
	<el-radio-group v-else-if="props.renderAs === 'radio-button'" v-model="state.value" v-bind="$attrs" @change="updateValue" class="g-sys-dict-group">
		<el-radio-button v-for="(item, index) in formattedDictData" :key="index" :value="item.value" @click.prevent="radioClick(item.value, Boolean($attrs.disabled))">
			{{ getDisplayText(item) }}
		</el-radio-button>
		<span v-if="$slots.default" class="g-sys-dict-inline-slot">
			<slot />
		</span>
	</el-radio-group>
</template>
<style scoped lang="scss">
.g-sys-dict-group {
	display: inline-flex;
	flex-wrap: wrap;
	align-items: center;
}

.g-sys-dict-inline-slot {
	display: inline-flex;
	align-items: center;
	margin-left: 12px;

	:deep(.el-form-item) {
		margin-bottom: 0;
	}

	:deep(.el-form-item__content) {
		margin-left: 0 !important;
	}
}
</style>
