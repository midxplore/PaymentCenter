import { App } from 'vue';
// VXETable
import VxeUITable from 'vxe-table';
import VxeUIPluginRenderElement from '@vxe-ui/plugin-render-element';
import VxeUIPluginExportXLSX from '@vxe-ui/plugin-export-xlsx';
// import 'vxe-table/lib/style.css'; // 已在theme/vxe.scss引入
import '@vxe-ui/plugin-render-element/dist/style.css';
// Vxe UI 组件库
import VxeUI, { VxeComponentSizeType } from 'vxe-pc-ui';
import { i18n } from '/@/i18n';
import ExcelJS from 'exceljs';
import { useThemeConfig } from '/@/stores/themeConfig';
const vxeSize: VxeComponentSizeType = useThemeConfig().themeConfig.globalComponentSize == 'small' ? 'mini' : useThemeConfig().themeConfig.globalComponentSize == 'default' ? 'small' : 'medium';

export const setupVXETable = (app: App<Element>) => {
	// 加载插件
	VxeUITable.use(VxeUIPluginRenderElement);
	VxeUITable.use(VxeUIPluginExportXLSX, { ExcelJS: ExcelJS });
	VxeUITable.setTheme('light'); // 主题颜色
	// VXETable全局配置https://vxetable.cn/v4.7/#/table/start/global
	VxeUITable.setConfig({
		size: vxeSize, // 全局尺寸mini、small、medium/default
		i18n: (key, args) => i18n.global.t(key, args),
		// i18n: (key, args) => i18n.global.t(key, args),
		// zIndex: 999, // 全局 zIndex 起始值，如果项目的的 z-index 样式值过大时就需要跟随设置更大，避免被遮挡
		// version: 0, // 版本号，对于某些带数据缓存的功能有用到，上升版本号可以用于重置数据
		// loadingText: '加载中...', // 全局loading提示内容，如果为null则不显示文本
		table: {
			border: true, // 是否带有边框
			stripe: true, // 是否带有斑马纹
			// keepSource: false,// 保持原始值的状态，被某些功能所依赖，比如编辑状态、还原数据等（开启后影响性能，具体取决于数据量）
			showOverflow: true, // 设置所有内容过长时显示为省略号（如果是固定列建议设置该值，提升渲染速度）
			size: vxeSize, // 表格的尺寸[medium, small, mini]
			autoResize: true, // 自动监听父元素的变化去重新计算表格（对于父元素可能存在动态变化、显示隐藏的容器中、列宽异常等场景中的可能会用到）
			round: false, // 是否为圆角边框
			emptyText: '暂无数据', //
			columnConfig: {
				isCurrent: false, // 当鼠标点击列头时，是否要高亮当前列
				isHover: false, // 当鼠标移到列头时，是否要高亮当前头
				resizable: true, // 每一列是否启用列宽调整
				minWidth: 70, // 每一列的最小宽度(auto, px, %)
			},
			rowConfig: {
				isCurrent: true, // 当鼠标点击行时，是否要高亮当前行
				isHover: true, // 当鼠标移到行时，是否要高亮当前行
			},
			currentRowConfig: {
				isFollowSelected: true, // 是否跟随单元格选中而移动高亮行
			},
			radioConfig: {
				strict: true, // 严格模式，选中后不能取消
				highlight: true, // 高亮选中行
			},
			checkboxConfig: {
				strict: true, // 严格模式，当数据为空或全部禁用时，列头的复选框为禁用状态
				highlight: true, // 高亮勾选行
				range: true, // 开启复选框范围选择功能（启用后通过鼠标在复选框的列内滑动选中或取消指定行）***全局配置貌似不生效***
				trigger: 'default', // 触发方式（注：当多种功能重叠时，会同时触发）default（默认）, cell（点击单元格触发）, row（点击行触发）
			},
			exportConfig: {
				remote: false, // 是否服务端导出
				types: ['xlsx', 'csv', 'html', 'xml', 'txt'], // 可选文件类型列表
				modes: ['current', 'selected', 'all'], // 输出数据的方式
			},
			printConfig: { modes: ['current', 'selected', 'all'] }, // 输出数据的方式
			tooltipConfig: {
				showAll: false, // 所有单元格开启工具提示
				enterable: true, // 鼠标是否可进入到工具提示中
				theme: 'dark',
			},
			customConfig: {
				// 貌似全局设置不生效，须要单独设置
				storage: {
					// 是否启用 localStorage 本地保存，会将列操作状态保留在本地（需要有 id）
					visible: true, // 启用显示/隐藏列状态
					resizable: true, // 启用列宽状态
				},
			},
			resizeConfig: {
				refreshDelay: 300, // 配置项调整后刷新延迟时间
			},
			virtualXConfig: {
				enabled: true, // 是否默认开启横向虚拟滚动
				gt: 30, // 指定大于指定列时自动启动横向虚拟滚动
			},
			virtualYConfig: {
				scrollToTopOnChange: true, // 当数据源被更改时，自动将纵向滚动条滚动到顶部
				enabled: true, // 是否默认开启纵向虚拟滚动
				gt: 50, // 当数据大于指定数量时自动触发启用虚拟滚动
			},
		},
		grid: {
			size: vxeSize,
			toolbarConfig: {
				size: vxeSize, // 尺寸 medium, small, mini
				perfect: false, // 配套的样式
			},
			zoomConfig: { escRestore: true }, // 是否允许通过按下 ESC 键还原
		},
		pager: {
			size: vxeSize,
			background: true, // 带背景颜色
			perfect: false, // 配套的样式
			autoHidden: false, // 当只有一页时自动隐藏
			pageSize: 20, // 每页大小
			pagerCount: 10, // 显示页码按钮的数量
			pageSizes: [10, 20, 50, 100, 200, 500, 1000], // 每页大小选项列表
			layouts: ['PrevJump', 'PrevPage', 'JumpNumber', 'NextPage', 'NextJump', 'Sizes', 'FullJump', 'PageCount', 'Total'], // 自定义布局
		},
	});
	// 安装UI组件
	app.use(VxeUI);
	app.use(VxeUITable);
	// 给 vue 实例挂载内部对象，例如：
	// app.config.globalProperties.$XModal = VxeUI.modal
	// app.config.globalProperties.$XPrint = VxeUI.print
	// app.config.globalProperties.$XSaveFile = VxeUI.saveFile
	// app.config.globalProperties.$XReadFile = VxeUI.readFile
};
