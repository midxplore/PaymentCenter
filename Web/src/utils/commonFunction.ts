// 通用函数
import useClipboard from 'vue-clipboard3';
import { ElMessage } from 'element-plus';
import { useI18n } from 'vue-i18n';
import { formatDate } from '/@/utils/formatTime';

export default function () {
	const { t } = useI18n();
	const { toClipboard } = useClipboard();

	// 百分比格式化
	const percentFormat = (row: EmptyArrayType, column: number, cellValue: string) => {
		return cellValue ? `${cellValue}%` : '-';
	};
	// 列表日期时间格式化
	const dateFormatYMD = (row: EmptyArrayType, column: number, cellValue: string) => {
		if (!cellValue) return '-';
		return formatDate(new Date(cellValue), 'YYYY-mm-dd');
	};
	// 列表日期时间格式化
	const dateFormatYMDHMS = (row: EmptyArrayType, column: number, cellValue: string) => {
		if (!cellValue) return '-';
		return formatDate(new Date(cellValue), 'YYYY-mm-dd HH:MM:SS');
	};
	// 列表日期时间格式化
	const dateFormatHMS = (row: EmptyArrayType, column: number, cellValue: string) => {
		if (!cellValue) return '-';
		let time = 0;
		if (typeof row === 'number') time = row;
		if (typeof cellValue === 'number') time = cellValue;
		return formatDate(new Date(time * 1000), 'HH:MM:SS');
	};
	// 小数格式化
	const scaleFormat = (value: string = '0', scale: number = 4) => {
		return Number.parseFloat(value).toFixed(scale);
	};
	// 小数格式化
	const scale2Format = (value: string = '0') => {
		return Number.parseFloat(value).toFixed(2);
	};
	// 千分符，默认保留两位小数
	const groupSeparator = (value: number, minimumFractionDigits: number = 2) => {
		return value.toLocaleString('en-US', {
			minimumFractionDigits: minimumFractionDigits,
			maximumFractionDigits: 2,
		});
	};
	// 点击复制文本
	const copyText = (text: string) => {
		return new Promise((resolve, reject) => {
			try {
				//复制
				toClipboard(text);
				//下面可以设置复制成功的提示框等操作
				ElMessage.success(t('message.layout.copyTextSuccess'));
				resolve(text);
			} catch (e) {
				//复制失败
				ElMessage.error(t('message.layout.copyTextError'));
				reject(e);
			}
		});
	};
	// 去掉Html标签(取前面5个字符)
	const removeHtmlSub = (value: string) => {
		var str = value.replace(/<[^>]+>/g, '');
		if (str.length > 50) return str.substring(0, 50) + '......';
		else return str;
	};
	// 去掉Html标签
	const removeHtml = (value: string) => {
		return value.replace(/<[^>]+>/g, '');
	};
	// 获取枚举描述
	const getEnumDesc = (key: any, lstEnum: any) => {
		return lstEnum.find((x: any) => x.value == key)?.describe;
	};
	// 追加query参数到url
	const appendQueryParams = (url: string, params: { [key: string]: any }) => {
		if (!params || Object.keys(params).length == 0) return url;
		const queryString = Object.keys(params)
			.map((key) => `${encodeURIComponent(key)}=${encodeURIComponent(params[key])}`)
			.join('&');
		return `${url}${url.includes('?') ? '&' : '?'}${queryString}`;
	};
	// ★ 此处原有 getNameAbbr()，已删除。它调用 apiSysCommonNameAbbrPost ——
	//   该端点在**后端与生成的 API 客户端里都不存在**，且全项目零调用点。
	//   留一个「一调就 404」的导出函数是给未来埋雷：谁顺手用了它，
	//   得到的是网络错误而不是功能。需要简称请在本模块里另行实现。
	// 处理条件字段清空
	const handleConditionalClear = (condition: boolean, fieldValue: any, clearValue: any = undefined) => {
		if (condition) {
			return clearValue;
		}
		return fieldValue;
	};
	// 获取时间范围选择器快捷选项配置
	const getTimeRangePickerShortcuts = () => {
		return [
			{
				text: '近一周',
				value: () => {
					// ★ 原写法是 new Date(themeStore.themeConfig.serverTime as any)，但 serverTime
					//   在整个前端**从未被赋值**（sysInfo.ts 只设 logo / title / 水印等），恒为 undefined
					//   → new Date(undefined) 是 Invalid Date → getFullYear() 得 NaN
					//   → 下面算出的 [start, end] 全是 Invalid Date，快捷选项**一直是坏的**。
					//   改用本地当前时间（与项目「本地 +08」时间基准一致）。
					const now = new Date();
					const end = new Date(now.getFullYear(), now.getMonth(), now.getDate());
					const start = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 7);
					return [start, end];
				},
			},
			{
				text: '近一月',
				value: () => {
					const now = new Date();
					const end = new Date(now.getFullYear(), now.getMonth(), now.getDate());
					const start = new Date(now.getFullYear(), now.getMonth() - 1, now.getDate());
					return [start, end];
				},
			},
			{
				text: '近三月',
				value: () => {
					const now = new Date();
					const end = new Date(now.getFullYear(), now.getMonth(), now.getDate());
					const start = new Date(now.getFullYear(), now.getMonth() - 3, now.getDate());
					return [start, end];
				},
			},
			{
				text: '近半年',
				value: () => {
					const now = new Date();
					const end = new Date(now.getFullYear(), now.getMonth(), now.getDate());
					const start = new Date(now.getFullYear(), now.getMonth() - 6, now.getDate());
					return [start, end];
				},
			},
			{
				text: '本年',
				value: () => {
					const now = new Date();
					const end = new Date(now.getFullYear(), 11, 31);
					const start = new Date(now.getFullYear(), 0, 1);
					return [start, end];
				},
			},
			{
				text: '近两年',
				value: () => {
					const now = new Date();
					const end = new Date(now.getFullYear(), 11, 31);
					const start = new Date(now.getFullYear() - 1, 0, 1);
					return [start, end];
				},
			},
			{
				text: '近三年',
				value: () => {
					const now = new Date();
					const end = new Date(now.getFullYear(), 11, 31);
					const start = new Date(now.getFullYear() - 2, 0, 1);
					return [start, end];
				},
			},
		];
	};
	return {
		percentFormat,
		dateFormatYMD,
		dateFormatYMDHMS,
		dateFormatHMS,
		scaleFormat,
		scale2Format,
		groupSeparator,
		copyText,
		removeHtmlSub,
		removeHtml,
		getEnumDesc,
		appendQueryParams,
		handleConditionalClear,
		getTimeRangePickerShortcuts,
	};
}
