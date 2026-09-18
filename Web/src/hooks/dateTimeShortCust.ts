/**
 * 日期时间设置快捷选项
 * @returns
 */
export const useDateTimeShortCust = () => {
	return [
		{ text: '今天', value: new Date() },
		{
			text: '昨天',
			value: () => {
				const date = new Date();
				date.setTime(date.getTime() - 3600 * 1000 * 24);
				return date;
			},
		},
		{
			text: '上周',
			value: () => {
				const date = new Date();
				date.setTime(date.getTime() - 3600 * 1000 * 24 * 7);
				return date;
			},
		},
	];
};
