import { createApp } from 'vue';
import pinia from '/@/stores/index';
import App from '/@/App.vue';
import router from '/@/router';
import { directive } from '/@/directive/index';
import { i18n } from '/@/i18n/index';
import other from '/@/utils/other';
import ElementPlus from 'element-plus';
import '/@/theme/index.scss';
// Vxe-Table（支付中心列表页刚需，保留全局）
import { setupVXETable } from '/@/hooks/setupVXETableHook';
// 自定义字典组件
import sysDict from '/@/components/sysDict/sysDict.vue';

// ★ 不加载 vite-auto-i18n 的 lang/index（自动翻译运行时）；vue-i18n 仍走 /@/i18n
// ★ 以下框架能力按需加载，不在此全局 import（代码与依赖保留，进对应页面再注册）：
//   animate.css / vue-grid-layout / vue-signature-pad / vue3-tree-org /
//   vform3-builds / vue-plugin-hiprint
//   见 /@/utils/lazyFrameworkPlugins.ts

const app = createApp(App);
directive(app);
other.elSvg(app);

// 注册全局字典组件
app.component('GSysDict', sysDict);

app.use(pinia).use(i18n).use(router).use(ElementPlus).use(setupVXETable).mount('#app');
