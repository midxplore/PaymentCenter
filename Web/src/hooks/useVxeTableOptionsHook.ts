import { reactive } from 'vue';
import { dayjs } from 'element-plus';
import { useThemeConfig } from '/@/stores/themeConfig';
import { merge } from 'lodash-es';
import { VxeGridProps, VxeGridPropTypes, VxeComponentSizeType } from 'vxe-table';
import { VxeTablePropTypes } from 'vxe-pc-ui/types/components/table';
import { getAPI } from '/@/utils/axios-utils';
import { SysColumnCustomApi } from '/@/api-services/system/api';

// 根据主题配置获取组件大小
const vxeSize: VxeComponentSizeType = useThemeConfig().themeConfig.globalComponentSize == 'small' ? 'mini' : useThemeConfig().themeConfig.globalComponentSize == 'default' ? 'small' : 'medium';

/**
 * @param {String} id 表格唯一标识，为空时自动随机产生;
 * @param {String} id name:表格名称，与导出文件名有关;
 * @param {VxeGridPropTypes.Columns<any>} columns 列配置;
 * @param {Boolean} showFooter 是否显示表尾;
 * @param {any} footerData 表尾数据;
 * @param {VxeTablePropTypes.FooterMethod<any>} footerMethod 表尾方法;
 * @param {Boolean} remoteCustom 是否使用服务端列配置;
 */
interface iVxeOption {
	id?: string;
	name?: string;
	columns: VxeGridPropTypes.Columns<any>;
	data?: VxeTablePropTypes.Data<any>;
	sortConfig?: VxeTablePropTypes.SortConfig<any>;
	showFooter?: boolean;
	footerData?: VxeTablePropTypes.FooterData;
	footerMethod?: VxeTablePropTypes.FooterMethod<any>;
	remoteCustom?: boolean;
}

/**
 * Vxe表格参数化Hook
 * @param {iVxeOption} opt 参数
 * @param {VxeGridProps<T>} extras 要覆盖的参数
 * @returns
 */
export const useVxeTable = <T>(opt: iVxeOption, extras?: VxeGridProps<T>) => {
	// 创建tableId,表格id固定才可以记录调整列宽，再次刷新仍有效。
	opt.id = opt.id ? opt.id : opt.name;
	const options = reactive<VxeGridProps>({
		id: opt.id,
		height: 'auto',
		autoResize: true,
		size: vxeSize,
		loading: false,
		align: 'center', // 自动监听父元素的变化去重新计算表格（对于父元素可能存在动态变化、显示隐藏的容器中、列宽异常等场景中的可能会用到）
		// data: [] as Array<T>,
		columns: opt.columns,
		showFooter: opt.showFooter,
		footerData: opt.footerData,
		footerMethod: opt.footerMethod,
		toolbarConfig: {
			size: vxeSize,
			slots: { buttons: 'toolbar_buttons', tools: 'toolbar_tools' },
			refresh: true,
			refreshOptions: {
				code: 'query',
			},
			export: true,
			print: true,
			zoom: true,
			custom: true,
		},
		checkboxConfig: { range: true },
		// sortConfig: { trigger: 'cell', remote: true },
		exportConfig: {
			remote: false, // 设置使用服务端导出
			filename: `${opt.name}导出_${dayjs().format('YYMMDDHHmmss')}`,
		},
		pagerConfig: {
			enabled: true,
			size: vxeSize,
			pageSize: 50,
		},
		printConfig: { sheetName: '' },
		// proxyConfig: {
		// 	enabled: true,
		// 	autoLoad: false,
		// 	sort: true,
		// 	props: {
		// 		list: 'data.result', // 不分页时
		// 		result: 'data.result.items', // 分页时
		// 		total: 'data.result.total',
		// 		message: 'data.message',
		// 	},
		// },
		customConfig: {
			storage: {
				// 是否启用 localStorage 本地保存，会将列操作状态保留在本地（需要有 id）
				visible: true, // 启用显示/隐藏列状态
				resizable: true, // 启用列宽状态
				sort: true, // 启用列顺序缓存
				fixed: true, // 启用冻结列状态缓存
			},
		},
	});
	// data优先级高于proxyConfig
	if (opt.data) {
		options.data = opt.data;
	} else {
		options.proxyConfig = {
			enabled: true,
			autoLoad: false,
			sort: true,
			response: {
				list: 'data.result', // 全量
				result: 'data.result.items', // 分页
				total: 'data.result.total',
				message: 'data.message',
			},
		};
	}
	if (opt.sortConfig) {
		options.sortConfig = opt.sortConfig;
	} else {
		options.sortConfig = { remote: true };
	}
	if (opt.remoteCustom && options.customConfig != undefined) {
		// 重写默认的恢复自定义配置逻辑
		options.customConfig.restoreStore = async ({ id, storeData }) => {
			const { data } = await getAPI(SysColumnCustomApi).apiSysColumnCustomDetailGet(id);
			if (data.result?.fixedData) storeData.fixedData = data.result.fixedData as any;
			if (data.result?.resizableData) storeData.resizableData = data.result.resizableData;
			if (data.result?.sortData) storeData.sortData = data.result.sortData;
			if (data.result?.visibleData) storeData.visibleData = data.result.visibleData;
			return storeData;
		};
		// 重写默认的保存方法
		options.customConfig.updateStore = async ({ id, type, storeData }) => {
			if (type === 'reset') await getAPI(SysColumnCustomApi).apiSysColumnCustomResetPost({ gridId: id });
			else {
				await getAPI(SysColumnCustomApi).apiSysColumnCustomStorePost({
					gridId: id,
					fixedData: storeData?.fixedData as any,
					resizableData: storeData?.resizableData,
					sortData: storeData?.sortData,
					visibleData: storeData?.visibleData,
				});
			}
		};
	}
	return extras ? merge(options, extras) : options;
};
