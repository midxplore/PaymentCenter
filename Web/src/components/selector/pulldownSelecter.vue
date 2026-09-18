<!-- 
	高级下拉选择组件 (PulldownSelecter)
	
	核心功能：
	1. 🔍 远程搜索 - 支持关键词搜索和即时查询
	2. 📄 分页支持 - 大数据量下的分页加载
	3. 📝 查询表单 - 支持自定义查询条件表单
	4. 📊 表格展示 - 以表格形式展示选项数据
	5. ✅ 单选/多选 - 灵活的选择模式
	6. 🎨 高亮显示 - 选中行自动高亮
	7. 🔄 数据回显 - 根据值自动选中对应数据
	
	适用场景：
	- 用户选择、部门选择、产品选择等需要搜索和筛选的场景
	- 需要展示详细信息的选择器（如显示用户的姓名、账号、电话等）
	- 大数据量的选择场景（支持分页加载）
-->

<!-- 
	 * 📖 使用示例（单选 / 多选，均包含查询参数与回显设置）：
	 * 导入下拉列表组件
	 * import pulldownSelecter from '/@/components/selector/pulldownSelecter.vue';

	 * 🔸 一、基础单选模式（最常见用法）
	 * <pulldown-selecter
	 *   class="w100"                              // 📏 选择器宽度
	 *   v-model="state.ruleForm.userId"           // 🎯 绑定单个值（用户ID）
	 *   :fetch-options="handleSysUserTable"       // 🔌 查询数据函数名
	 *   :queryParams="{ page: 1, pageSize: 10 }"  // 📄 查询/分页参数
	 *   :pagination="true"                        // 🔢 true 开启分页 / false 关闭分页，关闭时注意 pageSize 设置大一点
	 *   :default-options="state.defaultUsers"     // 🔁 回显：放入对象 [{id:xx,name:xx}]
	 *   label-prop="realName"                     // 🏷️ 行数据中用作展示的字段
	 *   value-prop="id"                           // 🔑 行数据中用作取值的字段
	 *   placeholder="请选择用户"                   // 💬 输入框提示
	 *   :auto-load="true"                         // ⚡ 打开下拉自动查询，false 为默认不查询数据
	 *   clearable                                 // 🧹 支持一键清空
	 *   filterable                                // 🔍 支持关键字过滤
	 *   @change="onUserChange"                    // 🔁 选中变化回调(value, row),value 为选中 id，row 为整行信息
	 * >
	 * </pulldown-selecter>
	 *
	 * -------------------------------------------------
	 * 🔸 二、多选模式（支持跨页多选与回显）
	 * <pulldown-selecter
	 *   class="w100"                              // 📏 选择器宽度
	 *   multiple                                  // 🌀 开启多选
	 *   v-model="state.ruleForm.userIds"          // 🎯 绑定数组（多个用户ID）
	 *   :fetch-options="fetchUserPage"            // 🔌 查询数据函数名
	 *   :queryParams="{ page: 1, pageSize: 10 }"  // 📄 查询/分页参数
	 *   :pagination="true"                        // 🔢 分页开关，同上
	 *   :default-options="state.defaultUsers"     // 🔁 回显：对象数组 [{id:xx,name:xx},{id:xx,name:xx}]
	 *   label-prop="realName"                     // 🏷️ 展示字段
	 *   value-prop="id"                           // 🔑 取值字段
	 *   placeholder="请选择用户（多选）"           // 💬 输入框提示
	 *   :auto-load="true"                         // ⚡ 打开下拉自动查询
	 *   clearable                                 // 🧹 支持一键清空
	 *   filterable                                // 🔍 支持关键字过滤
	 *   @change="onUserChange"                    // 选中变化回调(value, row),value 为选中 id，row 为整行信息
	 * >
	 * </pulldown-selecter>
	  

	  🔎 查询函数示例（单选 / 多选通用）：
	  *   const fetchUserPage = async (params: any) => {
	  *     const request = {
	  *       page: params.page,          // 🔢 当前页码
	  *       pageSize: params.pageSize,  // 📄 每页条数
	  *       field: 'id',                // 📌 排序字段
	  *       order: 'asc',               // ⬆️ 排序方式 asc/desc
	  *       descStr: 'desc',            // 📌 备用的降序标记
	  *
	  *       // 🎯 下面是查询条件，根据实际业务添加
	  *       account: params.account,    // 👤 账号
	  *       realName: params.realName,  // 🧾 姓名
	  *     } as any;
	  *     return await getAPI(SysUserApi).apiSysUserGetSysUserPost(request).then();
	  *   };
-->


<script lang="ts" setup>
// 引入Vue 3的核心API：生命周期钩子、响应式API、引用和监听器
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';

// 常量定义
const CONSTANTS = {
	MAX_PAGE_SIZE: 99999, // 不分页时的最大页面大小
	DROPDOWN_DELAY: 1000, // 下拉框打开后的延迟时间（毫秒）
	TABLE_HEIGHT_OFFSET: 175, // 表格高度基础偏移量（像素）
};

/**
 * 表格数据接口
 * @template T 表格行数据的类型
 */
interface TableData<T = any> {
	items: T[]; // 当前页的数据列表
	total: number; // 总数据条数，用于分页
}

/**
 * 查询参数接口
 * 包含分页参数和其他自定义查询条件
 */
interface QueryParams {
	[key: string]: any; // 允许动态添加其他查询字段
	page?: number; // 当前页码，从1开始
	pageSize?: number; // 每页显示的数据条数
}

// 定义组件属性（Props）
const props = defineProps({

	// v-model绑定的值，支持字符串、数字、数组或null类型
	modelValue: [String, Number, Array, null],

	/**
	 * 获取表格数据的异步方法（必填）
	 * @example
	 * const handleSysUserTable = (params: any) => {
	 *  return getAPI(SysUserApi).apiSysUserPagePost(params);
	 * };
	 */
	fetchOptions: {
		type: Function, // 函数类型
		required: true, // 必填项
	},

	/**
	 * 选中记录后绑定值的属性名
	 * 默认为'id'，即选中某行后，会取该行数据的id字段作为值
	 */
	valueProp: {
		type: String,
		default: 'id',
	},

	/**
	 * 选中记录后显示文本的属性名
	 * 默认为'name'，即在下拉框中显示的文本取自该字段
	 */
	labelProp: {
		type: String,
		default: 'name',
	},

	/**
	 * 显示值的格式化方法
	 * 可以自定义选中后在输入框中显示的文本格式
	 * @example
	 * :labelFormat="(item: any) => `${item.realName}(${item.account})`"
	 */
	labelFormat: {
		type: Function,
		default: (item: any) => undefined, // 默认不进行格式化
	},

	/**
	 * 默认查询条件的属性名
	 * 在输入框中输入的关键词会赋值给该字段
	 */
	keywordProp: {
		type: String,
		default: 'keyword',
	},

	/**
	 * 下拉框的宽度
	 */
	dropdownWidth: {
		type: String,
		default: '100%',
	},

	/**
	 * 下拉框的高度
	 */
	dropdownHeight: {
		type: String,
		default: '550px',
	},

	/**
	 * 输入框的占位符文本
	 */
	placeholder: {
		type: String,
		default: '请输入关键词',
	},

	/**
	 * 默认选项，用于回显已选中的数据
	 * 当数据不在当前页时，通过该属性提供数据进行回显
	 */
	defaultOptions: {
		type: Array<any>,
		default: [],
	},

	/**
	 * 查询表单的高度偏移量
	 * 用于计算表格高度时减去查询表单占用的高度
	 */
	queryHeightOffset: {
		type: Number,
		default: 35,
	},

	/**
	 * 查询表单标签的宽度
	 */
	queryLabelWidth: {
		type: String,
	},

	/**
	 * 查询参数对象
	 * 父组件传入的额外查询条件，会与组件内部的查询条件合并
	 */
	queryParams: {
		type: Object,
		default: () => {
			return {};
		},
	},

	/**
	 * 是否显示分页组件
	 */
	pagination: {
		type: Boolean,
		default: true,
	},

	/**
	 * 是否禁用组件
	 */
	disabled: Boolean,

	/**
	 * 是否多选模式
	 */
	multiple: Boolean,

	/**
	 * 是否可清空
	 */
	clearable: Boolean,

	/**
	 * 是否自动加载数据
	 * 默认为true，当设置为false时，下拉框打开时不会自动加载数据
	 */
	autoLoad: {
		type: Boolean,
		default: true,
	},
});

// 创建表格组件的引用，用于调用表格的方法（如setCurrentRow）
const tableRef = ref();
// 创建下拉选择器组件的引用，用于调用选择器的方法（如blur）
const selectRef = ref();
// 定义组件触发的事件：update:modelValue用于v-model双向绑定，change用于值改变通知
const emit = defineEmits(['update:modelValue', 'change']);

// 定义组件的响应式状态对象
const state = reactive({
	// 当前选中的值，单选时为字符串/数字，多选时为数组
	selectedValues: '' as string | string[],

	// 表格查询参数对象
	tableQuery: {
		[props.keywordProp]: '', // 动态属性名，默认为'keyword'，用于存储搜索关键词
		page: props.queryParams.page, // 当前页码，从父组件传入
		pageSize: props.queryParams?.pageSize, // 每页显示数量，从父组件传入
	} as QueryParams,

	// 表格数据对象
	tableData: {
		items: [] as any[], // 当前页的数据列表
		total: 0, // 总数据条数
	} as TableData,

	// 默认选项数组，用于回显
	defaultOptions: props.defaultOptions,

	// 加载状态标识
	loading: false,

	// 选中的行数据数组，用于表格高亮显示和跨页选择
	selectedRows: [] as any[],

	// 查询执行标识，防止重复执行
	isQuerying: false,

	// 下拉框是否刚打开的标识（用于防止remoteMethod在打开时重复查询）
	justOpened: false,
});

/**
 * 当前键盘选中的行索引
 * 用于键盘上下键导航，初始值为-1表示未选中任何行
 */
const currentRowIndex = ref(-1);

/**
 * 计算属性：表格高度
 * 根据是否有查询表单动态计算表格的显示高度
 * @returns 返回CSS calc表达式字符串
 *
 * 计算逻辑：
 * 1. 基础高度 = 下拉框高度 - 表格高度偏移量（175px，用于容纳分页等组件）
 * 2. 如果有查询表单，额外减去查询表单高度偏移量
 */
const tableHeight = computed(() => {
	const baseHeight = `${props.dropdownHeight} - ${CONSTANTS.TABLE_HEIGHT_OFFSET}px`;
	const queryOffset = props.queryHeightOffset > 0 ? ` - ${props.queryHeightOffset}px` : '';
	return `calc(${baseHeight}${queryOffset})`;
});

/**
 * 合并选中行数据（公共方法）
 * 从当前页数据和之前保存的数据中合并选中的行，避免跨页选择时数据丢失
 *
 * 使用场景：
 * - 多选模式下，用户可能在不同页面选择数据
 * - 当切换页面时，之前页面选中的数据不在当前表格中，但仍需保持选中状态
 *
 * 合并策略：
 * 1. 优先使用当前页的数据（因为数据可能已更新）
 * 2. 对于不在当前页的选中数据，从之前保存的selectedRows中获取
 * 3. 通过valueProp进行去重，避免重复数据
 *
 * @param values 选中的值数组（valueProp对应的值）
 * @returns 合并后的选中行数组（包含完整的行数据对象）
 */
const mergeSelectedRows = (values: any[]): any[] => {
	// 从当前页数据中找到选中的行
	const currentPageRows = state.tableData.items.filter((item) => values.includes(item[props.valueProp]));

	// 从之前保存的选中行中找到仍然选中的行（处理跨页选择）
	const previousRows = state.selectedRows.filter((item) => values.includes(item[props.valueProp]));

	// 合并去重：优先使用当前页数据（数据可能更新），如果不在当前页则使用之前保存的
	const allRows = [...currentPageRows];
	previousRows.forEach((row) => {
		// 检查该行是否已在当前页数据中，避免重复
		if (!allRows.find((item) => item[props.valueProp] === row[props.valueProp])) {
			allRows.push(row);
		}
	});

	return allRows;
};

/**
 * 判断指定行是否被选中
 * @param row 要判断的行数据
 * @returns 返回true表示已选中，false表示未选中
 */
const isRowSelected = (row: any) => {
	// 防止无效数据
	if (!row || row[props.valueProp] === undefined || row[props.valueProp] === null) {
		return false;
	}

	if (props.multiple) {
		// 多选模式：检查selectedValues数组中是否包含该行的值
		if (!Array.isArray(state.selectedValues)) {
			return false;
		}
		// 使用 some 方法进行严格比较，确保类型一致的情况下判断
		return state.selectedValues.some((val) => val === row[props.valueProp]);
	} else {
		// 单选模式：使用严格相等检查selectedValues是否等于该行的值
		return state.selectedValues !== null && state.selectedValues !== undefined && state.selectedValues === row[props.valueProp];
	}
};

/**
 * 根据选中值更新选中行数据
 * 当数据加载完成或选中值改变时调用，更新selectedRows数组
 * 注意：这个方法会同时从当前页数据和之前保存的selectedRows中查找，确保跨页选择不丢失
 */
const updateSelectedRows = () => {
	if (props.multiple) {
		// 多选模式：使用公共方法合并选中行
		const selectedValues = Array.isArray(state.selectedValues) ? state.selectedValues : [];
		state.selectedRows = mergeSelectedRows(selectedValues);
	} else {
		// 单选模式：从当前表格数据中查找选中值对应的行
		const selectedRow = state.tableData.items.find((item) => item[props.valueProp] === state.selectedValues);
		// 如果找到则放入数组，否则清空
		state.selectedRows = selectedRow ? [selectedRow] : [];
	}
};

/**
 * 处理键盘按键事件
 * 支持上下键导航和回车键选中
 * @param e 键盘事件对象
 */
const handleKeydown = (e: KeyboardEvent) => {
	// 获取当前表格数据
	const tableData = state.tableData?.items || [];
	// 如果没有数据，直接返回
	if (!tableData.length) return;

	// 处理上下方向键
	if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
		e.preventDefault(); // 阻止默认行为（防止页面滚动）
		const direction = e.key === 'ArrowDown' ? 1 : -1; // 向下为1，向上为-1
		// 计算新的索引，确保不超出范围[0, length-1]
		const newIndex = Math.max(0, Math.min(currentRowIndex.value + direction, tableData.length - 1));

		// 如果索引发生变化
		if (newIndex !== currentRowIndex.value) {
			currentRowIndex.value = newIndex; // 更新当前索引
			const row = tableData[newIndex]; // 获取新索引对应的行数据
			tableRef.value?.setCurrentRow(row); // 设置表格当前行（高亮显示）
		}
	} else if (e.key === 'Enter') {
		// 处理回车键：选中当前高亮的行
		const row = tableData[currentRowIndex.value];
		handleChange(row); // 触发选中逻辑
	}
};

/**
 * 处理分页大小改变事件
 * @param pageSize 新的每页显示数量
 */
const handleSizeChange = (pageSize: number) => {
	// 立即显示加载动画
	state.loading = true;

	state.tableQuery.pageSize = pageSize; // 更新每页显示数量
	state.tableQuery.page = 1; // 重置到第一页（因为改变了分页大小）
	executeQuery(); // 立即执行查询
};

/**
 * 处理页码改变事件
 * @param page 新的页码
 */
const handleCurrentChange = (page: number) => {
	// 立即显示加载动画
	state.loading = true;

	state.tableQuery.page = page; // 更新当前页码
	executeQuery(); // 立即执行查询
};

/**
 * 组件挂载后执行
 * 为输入框添加键盘事件监听
 */
onMounted(() => {
	selectRef.value?.inputRef?.addEventListener('keydown', handleKeydown);
});

/**
 * 组件卸载前执行
 * 移除键盘事件监听，防止内存泄漏
 */
onUnmounted(() => {
	selectRef.value?.inputRef?.removeEventListener('keydown', handleKeydown);
});

/**
 * 核心查询方法（立即执行，不带防抖）
 * 用于：按钮点击、分页切换、下拉框打开等需要立即响应的操作
 */
const executeQuery = () => {
	// 如果正在查询中，忽略重复请求（但不关闭loading，等待当前查询完成）
	if (state.isQuerying) {
		return;
	}

	// 设置查询标识和加载状态
	state.isQuerying = true;
	state.loading = true;

	// 合并查询参数：先使用父组件传入的参数，再用组件内部的参数覆盖
	const finalParams = Object.assign({}, props.queryParams, state.tableQuery);

	// 调用父组件传入的fetchOptions方法获取数据
	props
		.fetchOptions(finalParams)
		.then((res: any) => {
			const result = res.data?.result; // 获取响应结果
			state.tableData.items = result?.items ?? []; // 更新表格数据
			state.tableData.total = result?.total ?? 0; // 更新总数据条数
			state.loading = false; // 关闭加载状态
			state.isQuerying = false; // 重置查询标识
			// 数据加载完成后，更新选中行状态
			updateSelectedRows();
		})
		.catch((error: any) => {
			// 请求失败时的处理
			state.tableData.items = []; // 清空数据
			state.tableData.total = 0; // 重置总数
			state.loading = false; // 关闭加载状态
			state.isQuerying = false; // 重置查询标识
		});
};

/**
 * 重置查询条件
 * 将查询参数重置为初始状态，但保留分页大小设置
 * 当autoLoad为false时，只重置条件不执行查询
 */
const resetQuery = () => {
	const currentPageSize = state.tableQuery.pageSize; // 保存当前分页大小
	// 使用父组件传入的查询参数重置，并保留分页大小
	state.tableQuery = Object.assign({}, props.queryParams, { pageSize: currentPageSize }) as QueryParams;
	state.tableQuery.page = 1; // 重置到第一页

	// 清空输入框的值
	const input = selectRef.value?.$el?.querySelector('input');
	if (input) input.value = '';

	// 如果启用了自动加载，则执行查询
	if (props.autoLoad) {
		// 立即显示加载动画（在 executeQuery 之前设置）
		state.loading = true;
		executeQuery(); // 立即执行查询
	} else {
		// 如果禁用了自动加载，只清空数据不执行查询
		state.tableData.items = [];
		state.tableData.total = 0;
		state.loading = false;
	}
};

/**
 * 远程查询方法（即时执行，已取消防抖）
 * 用于输入框的 remote-method，实现即时搜索
 * 通过 isQuerying 标识防止重复请求，无需防抖延迟
 * @param query 查询参数，可以是字符串（关键词）或对象（完整查询条件）
 */
const remoteMethod = (query: any) => {
	// 如果是刚打开下拉框，忽略（因为 selectVisibleChange 已经触发了查询）
	if (state.justOpened) {
		state.justOpened = false;
		return;
	}

	// 如果正在查询中，忽略（防止重复请求）
	if (state.isQuerying) {
		return;
	}

	// 如果禁用了自动加载且查询字符串为空，则不执行查询
	if (!props.autoLoad && typeof query === 'string' && query.trim() === '') {
		return;
	}

	// 立即显示加载动画
	state.loading = true;

	// 如果传入的是字符串，更新关键词字段
	if (typeof query === 'string') {
		state.tableQuery[props.keywordProp] = query.trim();
		// 输入框搜索时，重置到第1页（因为搜索关键词变了）
		state.tableQuery.page = 1;
	}
	// 如果传入的是对象，不做处理，因为通常传入的就是state.tableQuery的引用

	// 调用核心查询方法
	executeQuery();
};

/**
 * 处理查询操作（用于自定义查询表单插槽）
 * @param clearKeyword 是否清空关键词，默认false
 */
const handleQuery = (clearKeyword: boolean = false) => {
	// 立即显示加载动画
	state.loading = true;

	if (clearKeyword) {
		// 如果需要清空关键词
		state.tableQuery[props.keywordProp] = undefined; // 清空查询参数中的关键词
		const input = selectRef.value.$el.querySelector('input'); // 获取输入框DOM元素
		if (input) input.value = ''; // 清空输入框的值
	}

	// 查询时重置到第1页（因为查询条件变了）
	state.tableQuery.page = 1;

	remoteMethod(state.tableQuery); // 调用远程查询方法
};

/**
 * 查询按钮点击事件处理
 * 直接调用核心查询方法，立即执行，不使用防抖
 * 查询时重置到第1页（因为查询条件变了，应该从头开始查看结果）
 */
const handleQueryClick = () => {
	// 立即显示加载动画（在 executeQuery 之前设置，确保不显示"暂无数据"）
	state.loading = true;

	state.tableQuery.page = 1; // 重置到第1页

	executeQuery(); // 立即执行查询
};

/**
 * 处理行选择改变事件
 * 当用户点击表格行时触发
 * @param row 被点击的行数据
 */
const handleChange = (row: any) => {
	// 防止无效点击
	if (!row || !row[props.valueProp]) return;

	// 去除值字段的首尾空格
	if (typeof row[props.valueProp] === 'string') row[props.valueProp] = row[props.valueProp]?.trim();
	// 将该行添加到默认选项中，用于回显
	setDefaultOptions([row]);

	if (props.multiple) {
		// 多选模式处理
		// 确保selectedValues是数组类型
		if (!Array.isArray(state.selectedValues)) {
			state.selectedValues = [];
		}

		// 检查该行是否已被选中
		const currentValues = Array.isArray(state.selectedValues) ? [...state.selectedValues] : [];
		const valueIndex = currentValues.indexOf(row[props.valueProp]); // 查找值在数组中的索引

		if (valueIndex > -1) {
			// 如果已选中，则取消选中
			currentValues.splice(valueIndex, 1); // 从值数组中移除
			// 从选中行数组中移除该行数据
			state.selectedRows = state.selectedRows.filter((item) => item[props.valueProp] !== row[props.valueProp]);
		} else {
			// 如果未选中，则添加到选中列表
			currentValues.push(row[props.valueProp]); // 添加值到数组
			// 检查是否已存在，避免重复添加
			const existingRow = state.selectedRows.find((item) => item[props.valueProp] === row[props.valueProp]);
			if (!existingRow) {
				state.selectedRows.push(row); // 添加行数据到选中行数组
			}
		}

		// 使用新数组触发响应式更新
		state.selectedValues = currentValues;
	} else {
		// 单选模式处理
		state.selectedValues = row[props.valueProp]; // 直接赋值
		state.selectedRows = [row]; // 选中行数组只包含当前行
	}

	// 通知父组件值已改变（用于v-model双向绑定）
	emit('update:modelValue', state.selectedValues);
	// 触发change事件，传递选中值和选中行数据
	emit('change', state.selectedValues, props.multiple ? state.selectedRows : row);

	// 设置表格选中效果
	if (!props.multiple) {
		// 单选模式：设置当前行高亮
		tableRef.value?.setCurrentRow(row);
		// 主动失焦，触发表单校验（如果在表单中使用）
		selectRef.value?.blur();
	}
	// 多选模式：不使用setCurrentRow，完全依赖row-style来显示选中效果
	// 这样可以同时高亮多行，而setCurrentRow只能高亮一行
};

/**
 * 选择器下拉框显示/隐藏事件处理
 * @param visible true表示显示，false表示隐藏
 */
const selectVisibleChange = (visible: boolean) => {
	if (visible) {
		// 下拉框打开时
		// 设置标识，防止 remoteMethod 在打开时重复查询
		state.justOpened = true;

		// 如果启用了自动加载，则执行查询
		if (props.autoLoad) {
			// 立即显示加载动画（在 executeQuery 之前设置）
			state.loading = true;

			// 不在这里清空数据，等查询成功后再更新，避免闪现"暂无数据"

			// 保留当前分页状态，只更新其他查询参数
			const paginationParams = {
				page: state.tableQuery.page, // 保留当前页码
				pageSize: state.tableQuery.pageSize, // 保留分页大小
				[props.keywordProp]: undefined, // 清空关键词（打开时不带搜索条件）
			};
			// 合并父组件参数和分页参数
			state.tableQuery = Object.assign({}, props.queryParams, paginationParams) as QueryParams;
			executeQuery(); // 立即执行查询
		}

		// 延迟后重置标识（防止remoteMethod被一直忽略）
		setTimeout(() => {
			state.justOpened = false;
		}, CONSTANTS.DROPDOWN_DELAY);
	} else {
		// 下拉框关闭时
		state.loading = false; // 关闭加载状态
		state.justOpened = false; // 重置标识
	}
};

/**
 * 设置默认选项
 * 用于将选中的数据添加到defaultOptions中，确保数据可以正确回显
 *
 * 为什么需要默认选项：
 * - 当选中的数据不在当前页时，el-select需要这些隐藏选项来正确显示选中值
 * - 例如：用户在第5页选择了数据，然后关闭下拉框，输入框需要显示该数据的label
 *
 * 去重机制：
 * - 使用Map而不是Set进行去重，因为Set基于对象引用判断，无法正确去重对象数组
 * - Map以valueProp的值作为key，可以基于业务唯一标识进行精确去重
 * - 后添加的同key值会覆盖先添加的，确保数据是最新的
 *
 * @param options 要添加的选项数组
 */
const setDefaultOptions = (options: any[]) => {
	// 使用Map进行基于valueProp的去重（Map的key是valueProp的值）
	const map = new Map();

	// 遍历新选项和已有的默认选项
	for (const item of [...(options ?? []), ...state.defaultOptions]) {
		const value = item?.[props.valueProp]; // 获取值字段（作为唯一标识）
		const label = props.labelFormat?.(item) || item?.[props.labelProp]; // 获取显示文本（优先使用格式化函数）

		// 只添加有效的选项（值和标签都不为空）
		if (value && label) {
			// Map会自动去重：相同key的值会被覆盖
			map.set(value, { [props.valueProp]: value, [props.labelProp]: label });
		}
	}

	// 从Map中提取去重后的选项数组
	state.defaultOptions = Array.from(map.values());
};

/**
 * 设置查询参数
 * 提供给父组件调用，用于动态修改查询条件
 * @param query 要设置的查询参数对象
 * @param append 是否追加到现有参数（true）还是替换（false），默认为true
 */
const setQueryParams = (query: any, append: boolean = true) => {
	if (!props.pagination) {
		// 如果不启用分页，设置一个很大的pageSize以获取全部数据
		query = Object.assign(query, {
			pageSize: CONSTANTS.MAX_PAGE_SIZE,
		});
	}
	// 合并父组件参数和传入的查询参数
	query = Object.assign({}, props.queryParams, query ?? {});
	// 根据append参数决定是追加还是替换
	state.tableQuery = append ? Object.assign(state.tableQuery, query) : query;
};

/**
 * 设置选中值
 * 提供给父组件调用，用于程序化设置选中的数据
 * @param option 要选中的数据，可以是单个对象或对象数组
 * @param row 保留参数，暂未使用
 */
const setValue = (option: any | any[], row?: any) => {
	// 统一转换为数组处理
	option = Array.isArray(option) ? option : [option];
	state.tableData.total = option.length; // 设置总数
	state.tableData.items = option; // 设置表格数据

	if (props.multiple) {
		// 多选模式：提取所有值到数组
		state.selectedValues = option.map((item: any) => item[props.valueProp]);
		state.selectedRows = option; // 保存选中行数据
		emit('update:modelValue', state.selectedValues); // 通知父组件
		emit('change', state.selectedValues, option); // 触发change事件
	} else {
		// 单选模式：只取第一个值
		state.selectedValues = option[0]?.[props.valueProp];
		state.selectedRows = option[0] ? [option[0]] : []; // 保存选中行数据
		emit('update:modelValue', state.selectedValues); // 通知父组件
		emit('change', state.selectedValues, option[0]); // 触发change事件
	}
};

/**
 * 选中值改变事件处理
 * 当用户通过输入框的标签（Tag）删除选中项时触发
 * @param val 新的选中值
 */
const selectedValuesChange = (val: any) => {
	state.selectedValues = val; // 更新选中值

	// 通知父组件更新值
	emit('update:modelValue', val);

	if (props.multiple) {
		// 多选模式：需要同步更新selectedRows数组
		const selectedValues = Array.isArray(val) ? val : [];

		// 使用公共方法合并选中行数据
		const allSelectedRows = mergeSelectedRows(selectedValues);

		// 更新选中行数据
		state.selectedRows = allSelectedRows;

		// 多选模式完全依赖row-style来显示选中效果，不使用setCurrentRow

		emit('change', val, allSelectedRows); // 触发change事件，传递所有选中行数据
	} else {
		// 单选模式
		const selectedRow = state.tableData?.items?.find((item) => item[props.valueProp] === val);
		if (selectedRow) {
			// 如果在当前页找到了选中的行
			state.selectedRows = [selectedRow]; // 更新选中行数据
			tableRef.value?.setCurrentRow(selectedRow); // 设置表格当前行高亮
		} else {
			// 如果没找到（可能被清空了）
			state.selectedRows = []; // 清空选中行数据
			tableRef.value?.setCurrentRow(null); // 清除表格高亮
		}
		emit('change', val, selectedRow); // 触发change事件
	}
};

/**
 * 监听modelValue的变化
 * 当父组件修改v-model绑定的值时，同步更新组件内部状态
 */
watch(
	() => props.modelValue, // 监听的数据源
	(val: any) => {
		if (props.multiple) {
			// 多选模式：确保selectedValues是数组类型
			// 如果val是数组则直接使用，如果是单个值则转为数组，如果为空则使用空数组
			state.selectedValues = Array.isArray(val) ? val : val ? [val] : [];
		} else {
			// 单选模式：直接赋值
			state.selectedValues = val;
		}
		// 更新选中行状态（确保表格高亮正确）
		updateSelectedRows();
	},
	{ immediate: true } // 立即执行一次，确保初始化时也能正确设置
);

/**
 * 监听defaultOptions的变化
 * 当父组件修改默认选项时，同步更新组件内部状态
 *
 * 支持两种写法：
 * - 单选：传单个对象  { id: 1, name: '张三' }
 * - 多选：传对象数组 [{ id: 1, name: '张三' }, ...]
 * 内部统一转换为数组再处理，兼容旧用法
 */
watch(
	() => props.defaultOptions, // 监听的数据源
	(val: any) => {
		// 统一转换为数组：数组保持不变，单个对象包一层，空值转为空数组
		const normalized = Array.isArray(val) ? val : val ? [val] : [];
		setDefaultOptions(normalized);
	},
	{ immediate: true } // 立即执行一次
);

/**
 * 暴露给父组件的方法
 * 父组件可以通过ref调用这些方法
 *
 * 使用示例：
 * const selecterRef = ref();
 *
 * // 设置选中值（程序化设置）
 * selecterRef.value.setValue([{ id: 1, name: '张三' }]);
 *
 * // 执行查询（可选择是否清空关键词）
 * selecterRef.value.handleQuery(true);
 *
 * // 设置查询参数（动态修改查询条件）
 * selecterRef.value.setQueryParams({ status: 1 }, true);
 *
 * // 设置默认选项（手动添加回显选项）
 * selecterRef.value.setDefaultOptions([{ id: 1, name: '张三' }]);
 */
defineExpose({
	setValue, // 设置选中值（用于程序化设置选中的数据）
	handleQuery, // 执行查询（用于手动触发查询，可选择是否清空关键词）
	setQueryParams, // 设置查询参数（用于动态修改查询条件）
	setDefaultOptions, // 设置默认选项（用于手动添加回显选项）
});
</script>

<template>
	<!-- Element Plus下拉选择器组件 -->
	<el-select
		v-bind="$attrs"
		v-model="state.selectedValues"
		:clearable="clearable"
		:multiple="multiple"
		:disabled="disabled"
		:placeholder="placeholder"
		:remote-method="remoteMethod"
		@visible-change="selectVisibleChange"
		@change="selectedValuesChange"
		:style="{ width: dropdownWidth }"
		popper-class="popper-class"
		ref="selectRef"
		remote-show-suffix
		filterable
		remote
	>
		<!-- 隐藏的选项，用于占位，防止Element Plus警告 -->
		<el-option style="width: 0; height: 0" value="" />

		<!-- 默认选项，用于回显数据 -->
		<!-- 当选中的数据不在当前页时，通过这些隐藏选项进行回显 -->
		<el-option v-for="item in state.defaultOptions ?? []" :key="item[valueProp]" :label="labelFormat(item) || item[labelProp]" :value="item[valueProp]" style="width: 0; height: 0" />

		<!-- 下拉框内容区域 -->
		<!-- 
		v-loading指令：显示加载动画
		注意：loading状态放在div上而不是table上，是为了覆盖整个下拉区域（包括查询表单和分页）
		这样可以避免查询时闪现"暂无数据"的提示
	-->
		<div class="w100 selector-loading-container" v-loading="state.loading">
			<!-- 查询表单区域（仅当父组件提供了queryForm插槽时显示） -->
			<el-form :model="state.tableQuery" v-if="$slots.queryForm" class="mg5 query-form" :label-width="queryLabelWidth ?? ''" @click.stop @submit.prevent>
				<el-row :gutter="10">
					<!-- 查询表单插槽：由父组件提供表单内容 -->
					<slot name="queryForm" :query="state.tableQuery" :handleQuery="handleQuery"></slot>

					<!-- 查询和重置按钮组 -->
					<el-button-group style="position: absolute; right: 10px">
						<!-- 查询按钮：点击时立即显示加载动画并执行查询 -->
						<el-button type="primary" icon="ele-Search" @click="handleQueryClick" v-reclick="1000"> 查询 </el-button>
						<!-- 重置按钮：重置查询条件并重新查询 -->
						<el-button icon="ele-Refresh" @click="resetQuery" v-reclick="1000"> 重置 </el-button>
					</el-button-group>
				</el-row>
			</el-form>

			<!-- 
			数据表格 
			关键配置说明：
			1. height: 动态计算高度，有查询表单时使用计算属性tableHeight，否则使用固定计算式
			2. highlight-current-row: 单选模式启用行高亮，多选模式禁用（多选用row-style实现）
			3. row-class-name: 为选中行添加'selected-row'类名，用于CSS样式控制
			4. row-style: 为选中行添加内联样式（背景色和左边框），确保跨浏览器兼容性
		-->
			<el-table
				ref="tableRef"
				@row-click="handleChange"
				:data="state.tableData?.items ?? []"
				:height="$slots.queryForm ? tableHeight : `calc(${dropdownHeight} - ${CONSTANTS.TABLE_HEIGHT_OFFSET}px)`"
				:highlight-current-row="!multiple"
				:row-class-name="({ row }: { row: any }) => (isRowSelected(row) ? 'selected-row' : '')"
				:row-style="({ row }: { row: any }) => (isRowSelected(row) ? { backgroundColor: 'var(--el-color-primary-light-9)', borderLeft: '3px solid var(--el-color-primary)' } : {})"
			>
				<!-- 空数据提示：根据不同情况显示不同的提示 -->
				<template #empty>
					<el-empty v-if="!state.loading && props.autoLoad" :image-size="25" />
					<div v-else-if="!state.loading && !props.autoLoad" class="empty-tip">
						<el-empty :image-size="25" description="请输入关键词进行搜索" />
					</div>
				</template>
				<!-- 表格列插槽：由父组件提供列定义 -->
				<slot name="columns"></slot>
			</el-table>

			<!-- 
				分页组件（仅当启用分页时显示）
				配置说明：
				1. disabled: 加载时禁用分页操作，防止重复请求
				2. pager-count: 显示5个页码按钮，适合下拉框的有限空间
				3. layout: 简化布局（只显示上一页、页码、下一页），节省空间
				4. size: 使用小尺寸，适配下拉框高度
			-->
			<el-pagination
				v-if="props.pagination"
				:disabled="state.loading"
				:currentPage="state.tableQuery.page"
				:page-size="state.tableQuery.pageSize"
				:total="state.tableData.total"
				:pager-count="5"
				@size-change="handleSizeChange"
				@current-change="handleCurrentChange"
				layout="prev, pager, next"
				size="small"
				background
			/>
		</div>
	</el-select>
</template>

<!-- 
	样式定义（scoped）
	说明：这些样式只作用于当前组件，不会影响其他组件
-->
<style scoped>
/**
 * 查询表单层级控制
 * 目的：确保查询表单在下拉框内容的最上层，防止被其他元素遮挡
 * 场景：当查询表单内有下拉框、日期选择器等弹出组件时，需要确保它们能正常显示
 */
.query-form {
	z-index: 9999;
}

/**
 * 隐藏下拉选择器的滚动条
 * 目的：提升UI美观度，使用自定义的滚动方式
 * 说明：使用 :deep 深度选择器穿透 scoped 限制，影响 Element Plus 内部元素
 * 注意：display: none 而不是 visibility: hidden，完全移除滚动条占用的空间
 */
:deep(.el-select-dropdown) {
	.el-scrollbar > .el-scrollbar__bar {
		display: none !important;
	}
}

/**
 * 下拉框最小宽度限制
 * 目的：确保下拉框有足够的宽度显示表格内容
 * 值：400px 是经过测试的最小宽度，能够容纳常见的表格列
 * 优先级：使用 !important 确保样式不被其他全局样式覆盖
 */
.popper-class {
	min-width: 400px !important;
}

/**
 * 下拉框包裹器最大高度（scoped版本）
 * 目的：限制下拉框内容区域的最大高度，防止超出屏幕
 * 值：600px 是合理的最大高度，适配大部分屏幕分辨率
 * 说明：使用双层 :deep 选择器穿透 Element Plus 的嵌套结构
 */
:deep(.popper-class) :deep(.el-select-dropdown__wrap) {
	max-height: 600px !important;
}
</style>

<!-- 
	全局样式（不使用scoped）
	说明：这些样式是全局的，会影响整个应用
	原因：Element Plus 的下拉框是通过 Teleport 挂载到 body 下的，scoped 样式无法穿透
	注意：请谨慎修改，避免影响其他组件
-->
<style>
/**
 * 下拉框包裹器最大高度（全局版本）
 * 目的：限制下拉框滚动区域的最大高度
 * 值：450px 与 scoped 中的 600px 不同，这是考虑到实际显示效果的调整
 * 
 * 选择器说明：
 * 1. .popper-class .el-select-dropdown__wrap - 针对当前组件的下拉框
 * 2. .el-select-dropdown__wrap[max-height] - 针对带有 max-height 属性的下拉框
 * 
 * 优先级：使用 !important 确保覆盖 Element Plus 的默认样式
 */
.popper-class .el-select-dropdown__wrap,
.el-select-dropdown__wrap[max-height] {
	max-height: 450px !important;
}

/**
 * 选中行的基础样式
 * 目的：为选中的表格行添加视觉反馈，让用户清楚知道哪些行被选中
 * 
 * 样式设计：
 * 1. 背景色：使用主题色的浅色版本（light-9），保持视觉柔和
 * 2. 左边框：3px 实线，使用主题色，增强选中状态的识别度
 * 
 * 选择器说明：
 * 1. .popper-class .selected-row - 下拉框内的选中行
 * 2. .el-table .selected-row - 普通表格的选中行（保持一致性）
 * 
 * CSS变量：
 * - var(--el-color-primary-light-9) - Element Plus 主题色的浅色版本
 * - var(--el-color-primary) - Element Plus 主题色
 */
.popper-class .selected-row,
.el-table .selected-row {
	background-color: var(--el-color-primary-light-9) !important;
	border-left: 3px solid var(--el-color-primary) !important;
}

/**
 * 选中行的悬停样式
 * 目的：当鼠标悬停在选中行上时，提供额外的视觉反馈
 * 
 * 样式设计：
 * 1. 背景色：使用 light-8（比 light-9 稍深），表示交互状态
 * 2. 保持左边框不变，维持选中状态的一致性
 * 
 * 交互说明：
 * - 正常选中状态：light-9（较浅）
 * - 悬停选中状态：light-8（稍深）
 * - 形成层次感，提升用户体验
 */
.popper-class .selected-row:hover,
.el-table .selected-row:hover {
	background-color: var(--el-color-primary-light-8) !important;
}

/**
 * 选中行的单元格样式（基础状态）
 * 目的：确保表格单元格（td）也应用选中样式
 * 
 * 为什么需要单独设置：
 * - Element Plus 表格的 td 元素有自己的背景色样式
 * - 仅设置 tr 的背景色可能被 td 的样式覆盖
 * - 必须显式设置 td 的背景色，确保选中效果完整显示
 * 
 * 优先级：使用 !important 确保覆盖 Element Plus 的默认 td 样式
 */
.popper-class .selected-row td,
.el-table .selected-row td {
	background-color: var(--el-color-primary-light-9) !important;
}

/**
 * 选中行的单元格样式（悬停状态）
 * 目的：当鼠标悬停时，单元格也要同步改变背景色
 * 
 * 重要性：
 * - 与上面的 .selected-row:hover 配合使用
 * - 确保悬停时，整行（包括所有单元格）的背景色都统一改变
 * - 避免出现行背景色改变，但单元格背景色不变的视觉问题
 */
.popper-class .selected-row:hover td,
.el-table .selected-row:hover td {
	background-color: var(--el-color-primary-light-8) !important;
}
</style>
