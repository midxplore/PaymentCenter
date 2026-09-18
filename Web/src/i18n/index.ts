import { createI18n } from 'vue-i18n';
import pinia from '/@/stores/index';
import { storeToRefs } from 'pinia';
import { useThemeConfig } from '/@/stores/themeConfig';

/**
 * 说明：
 * 须在 pages 下新建文件夹（建议 `要国际化界面目录` 与 `i18n 目录` 相同，方便查找），
 * 注意国际化定义的字段，不要与原有的定义字段相同。
 * 1、/src/i18n/lang 下的 ts 为框架的国际化内容
 * 2、/src/i18n/pages 下的 ts 为各界面的国际化内容
 */

// element plus 自带国际化
import deLocale from 'element-plus/es/locale/lang/de';
import enLocale from 'element-plus/es/locale/lang/en';
import esLocale from 'element-plus/es/locale/lang/es';
import fiLocale from 'element-plus/es/locale/lang/fi';
import frLocale from 'element-plus/es/locale/lang/fr';
import itLocale from 'element-plus/es/locale/lang/it';
import jaLocale from 'element-plus/es/locale/lang/ja';
import koLocale from 'element-plus/es/locale/lang/ko';
import noLocale from 'element-plus/es/locale/lang/no';
import plLocale from 'element-plus/es/locale/lang/pl';
import ptLocale from 'element-plus/es/locale/lang/pt';
import ruLocale from 'element-plus/es/locale/lang/ru';
import zhcnLocale from 'element-plus/es/locale/lang/zh-cn';
import zhtwLocale from 'element-plus/es/locale/lang/zh-tw';
import zhhkLocale from 'element-plus/es/locale/lang/zh-tw';
import ththLocale from 'element-plus/es/locale/lang/th';
import idIDLocale from 'element-plus/es/locale/lang/id';
import msMYLocale from 'element-plus/es/locale/lang/ms';
import viVNLocale from 'element-plus/es/locale/lang/vi';

import enUS from 'vxe-table/lib/locale/lang/en-US';
import deDE from 'vxe-table/lib/locale/lang/de-DE.js';
import esES from 'vxe-table/lib/locale/lang/es-ES.js';
import fiFI from 'vxe-table/lib/locale/lang/en-US.js';
import frFR from 'vxe-table/lib/locale/lang/fr-FR.js';
import itIT from 'vxe-table/lib/locale/lang/it-IT.js';
import jaJP from 'vxe-table/lib/locale/lang/ja-JP';
import koKR from 'vxe-table/lib/locale/lang/ko-KR';
import noNO from 'vxe-table/lib/locale/lang/nb-NO.js';
import plPL from 'vxe-table/lib/locale/lang/en-US';
import ptBR from 'vxe-table/lib/locale/lang/pt-BR';
import ruRU from 'vxe-table/lib/locale/lang/ru-RU';
import zhCN from 'vxe-table/lib/locale/lang/zh-CN';
import zhHK from 'vxe-table/lib/locale/lang/zh-HK';
import zhTW from 'vxe-table/lib/locale/lang/zh-TW';
import thTH from 'vxe-table/lib/locale/lang/th-TH';
import idID from 'vxe-table/lib/locale/lang/en-US';
import msMY from 'vxe-table/lib/locale/lang/en-US';
import viVN from 'vxe-table/lib/locale/lang/vi-VN';

// 定义变量内容
const messages = {};
const element = {
	'zh-CN': zhcnLocale,
	'zh-TW': zhtwLocale,
	'zh-HK': zhhkLocale,
	en: enLocale,
	de: deLocale,
	es: esLocale,
	fi: fiLocale,
	fr: frLocale,
	it: itLocale,
	ja: jaLocale,
	ko: koLocale,
	no: noLocale,
	pl: plLocale,
	pt: ptLocale,
	ru: ruLocale,
	th: ththLocale,
	id: idIDLocale,
	ms: msMYLocale,
	vi: viVNLocale,
};

const vxe = {
	'zh-CN': zhCN,
	'zh-TW': zhTW,
	'zh-HK': zhHK,
	en: enUS,
	de: deDE,
	es: esES,
	fi: fiFI,
	fr: frFR,
	it: itIT,
	ja: jaJP,
	ko: koKR,
	no: noNO,
	pl: plPL,
	pt: ptBR,
	ru: ruRU,
	th: thTH,
	id: idID,
	ms: msMY,
	vi: viVN,
};

export const languageList = {
	'zh-CN': '简体中文',
	// 'zh-TW': '繁體中文(台灣)',
	// 'zh-HK': '繁體中文(香港)',
	en: 'English',
	// de: 'Deutsch',
	// es: 'Español',
	// fi: 'Suomeksi',
	// fr: 'Français',
	// it: 'Italiano',
	// ja: '日本語',
	// ko: '한국어',
	// no: 'Norsk',
	// pl: 'Polski',
	// pt: 'Português',
	// ru: 'Русский',
	// th: 'ไทย',
	// id: 'Indonesia',
	// ms: 'Malaysia',
	// vi: 'Việt Nam',
};

const itemize = { en: [], 'zh-CN': [], 'zh-TW': [], 'zh-HK': [], de: [], es: [], fi: [], fr: [], it: [], ja: [], ko: [], no: [], pl: [], pt: [], ru: [], th: [], id: [], ms: [], vi: [] };
const modules: Record<string, any> = import.meta.glob('./**/*.ts', { eager: true });

// 对自动引入的 modules 进行分类 en、zh-cn、zh-tw
// https://vitejs.cn/vite3-cn/guide/features.html#glob-import
for (const path in modules) {
	const key = path.match(/(\S+)\/(\S+).ts/);
	if (itemize[key![2]]) itemize[key![2]].push(modules[path].default);
	else itemize[key![2]] = modules[path];
}
// 合并数组对象（非标准数组对象，数组中对象的每项 key、value 都不同）
function mergeArrObj<T>(list: T, key: string) {
	let obj = {};
	list[key].forEach((i: EmptyObjectType) => {
		obj = Object.assign({}, obj, i);
	});
	return obj;
}

// 处理最终格式
for (const key in itemize) {
	messages[key] = {
		name: key,
		el: element[key].el,
		message: mergeArrObj(itemize, key),
		vxe: vxe[key].vxe,
	};
}

// 读取 pinia 默认语言
const stores = useThemeConfig(pinia);
const { themeConfig } = storeToRefs(stores);

// 导出语言国际化
// https://vue-i18n.intlify.dev/guide/essentials/fallback.html#explicit-fallback-with-one-locale
export const i18n = createI18n({
	legacy: false,
	silentTranslationWarn: true,
	missingWarn: false,
	silentFallbackWarn: true,
	fallbackWarn: false,
	locale: themeConfig.value.globalI18n,
	fallbackLocale: 'zh-CN',
	messages,
	globalInjection: true,
	fallbackFormat: false, // 关闭默认的 fallback 行为
	missing: (locale, key) => {
		// 若没有'.'（翻译内容）则返回整个key
		const lastPart = key.includes('.') ? key.split('.').pop()! : key;
		return lastPart.replace(/\${(.+?)}/g, '$1');
	},
});

//iso 3166-1 国家代码
export const iso_3166_1_CountryList = {
	de: 'de',
	en: 'us',
	es: 'es',
	fi: 'fi',
	fr: 'fr',
	it: 'it',
	ja: 'jp',
	ko: 'kr',
	no: 'no',
	pl: 'pl',
	pt: 'pt',
	ru: 'ru',
	'zh-CN': 'cn',
	'zh-TW': 'tw',
	'zh-HK': 'hk',
	th: 'th',
	id: 'id',
	ms: 'ms',
	vi: 'vi',
};

/**
 * 获取国家代码
 * @param locale 语言
 * @returns 国家代码
 */
export const getCountryCode = (locale: string) => {
	return iso_3166_1_CountryList[locale as keyof typeof iso_3166_1_CountryList];
};
