import { h } from 'vue';
import { ElButton, ElNotification } from 'element-plus';

type NoticeDetail = {
	version?: string;
	options?: {
		notificationProps?: {
			title?: string;
			description?: string;
			buttonText?: string;
			dismissButtonText?: string;
		};
	};
};

declare global {
	interface Window {
		pluginWebUpdateNotice_?: {
			dismissUpdate?: () => void;
			closeNotification?: () => void;
		};
	}
}

const EVENT = 'plugin_web_update_notice';

/** hash / history 下收银台路径：/c/:token */
export function isCashierLocation(pathname = window.location.pathname, hash = window.location.hash): boolean {
	if (/^\/c(\/|$)/.test(pathname)) return true;
	// hash 模式兜底：#/c/xxx
	const m = hash.match(/^#(\/[^?]*)/);
	return !!m && /^\/c(\/|$)/.test(m[1]);
}

/**
 * 后台保留「系统更新」提示；收银台直面付款人，不展示。
 * 需配合 vite webUpdateNotice({ hiddenDefaultNotification: true })。
 */
export function initWebUpdateNoticeGate(isCashier?: () => boolean): () => void {
	const shouldSkip = () => (typeof isCashier === 'function' ? isCashier() : isCashierLocation());

	const onNotice = (ev: Event) => {
		if (shouldSkip()) {
			window.pluginWebUpdateNotice_?.closeNotification?.();
			return;
		}
		const detail = (ev as CustomEvent<NoticeDetail>).detail;
		const props = detail?.options?.notificationProps ?? {};
		const title = props.title || '系统更新';
		const description = props.description || '系统更新啦，请刷新页面！';
		const refreshText = props.buttonText || '刷新';
		const dismissText = props.dismissButtonText || '忽略';

		ElNotification({
			title,
			position: 'top-left',
			duration: 0,
			showClose: true,
			message: () =>
				h('div', { class: 'web-update-notice-body' }, [
					h('p', { style: 'margin:0 0 10px;line-height:1.5' }, description),
					h('div', { style: 'display:flex;gap:8px;justify-content:flex-end' }, [
						h(
							ElButton,
							{
								size: 'small',
								onClick: () => {
									window.pluginWebUpdateNotice_?.dismissUpdate?.();
									ElNotification.closeAll();
								},
							},
							() => dismissText
						),
						h(
							ElButton,
							{
								size: 'small',
								type: 'primary',
								onClick: () => {
									window.location.reload();
								},
							},
							() => refreshText
						),
					]),
				]),
			onClose: () => {
				window.pluginWebUpdateNotice_?.dismissUpdate?.();
			},
		});
	};

	document.body.addEventListener(EVENT, onNotice as EventListener);
	return () => document.body.removeEventListener(EVENT, onNotice as EventListener);
}
