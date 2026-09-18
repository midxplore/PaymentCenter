import { debounce } from 'lodash-es';
import { Local, Session } from '/@/utils/storage';
import { ElMessageBox } from 'element-plus';
import { accessTokenKey, refreshAccessTokenKey } from '/@/utils/axios-utils';
import { i18n } from '/@/i18n';

type IdleTimeoutConfig = {
	/** 空闲超时时间（秒），默认30分钟 */
	timeout?: number;
	/** 用于设置最后活动时间的监听事件列表 */
	events?: string[];
	/** 登出回调函数，在超时发生时执行 */
	onTimeout?: () => void;
	/** 监听事件防抖间隔（毫秒），默认 200，设为0表示不启用防抖 */
	debounceInterval?: number;
};

// 动态导入 signalR
let signalR: any;

/** 动态导入 signalR */
async function loadSignalR() {
	if (!signalR) {
		const module = await import('/@/views/system/onlineUser/signalR');
		signalR = module.signalR;
	}
	return signalR;
}

class IdleTimeoutManager {
	private timerId: number | null = null;
	private readonly config: Required<IdleTimeoutConfig>;
	private readonly debouncedReset: () => void;
	/** 检查闲置超时时间间隔 */
	private readonly checkTimeoutInterval = 2 * 1000;

	constructor(config: IdleTimeoutConfig = {}) {
		this.config = {
			timeout: 30 * 60,
			events: ['mousewheel', 'keydown', 'click'],
			onTimeout: this.timeOutExec.bind(this),
			debounceInterval: 200,
			...config,
		};

		// 防抖处理
		this.debouncedReset = this.config.debounceInterval > 0 ? debounce(this.setLastActivityTime.bind(this), this.config.debounceInterval) : this.setLastActivityTime.bind(this);

		this.init();
	}

	private init() {
		// 初始化事件监听
		this.config.events.forEach((event) => {
			window.addEventListener(event, this.debouncedReset);
		});

		// 页面可见性监听
		document.addEventListener('visibilitychange', this.handleVisibilityChange);

		// 设置最后活动时间
		this.setLastActivityTime();

		// 更新空闲超时时间（会按需启动检查定时器）
		this.updateIdleTimeout(this.config.timeout);
	}

	private handleVisibilityChange = () => {
		if (document.visibilityState === 'visible') {
			this.setLastActivityTime();
		}
	};

	/** 设置最后活动时间 */
	public setLastActivityTime() {
		Local.set('lastActivityTime', new Date().getTime());
	}

	/**
	 * 更新空闲超时时间
	 * @param timeout - 新的超时时间（毫秒）
	 */
	public updateIdleTimeout(timeout: number) {
		this.config.timeout = timeout;
		// 如果闲置超时时间大于0且检测定时器没启动
		if (this.config.timeout > 0 && this.timerId == null) {
			this.timerId = window.setInterval(this.checkTimeout.bind(this), this.checkTimeoutInterval);
		} else if (this.config.timeout == 0 && this.timerId != null) {
			window.clearInterval(this.timerId);
			this.timerId = null;
		}
	}

	/** 检查是否超时 */
	public checkTimeout() {
		const currentTime = new Date().getTime(); // 当前时间
		const lastActivityTime = Number(Local.get('lastActivityTime'));
		if (lastActivityTime == 0) return;
		const accessToken = Local.get(accessTokenKey);
		if (!accessToken || accessToken == 'invalid_token') return;
		const timeout = this.config.timeout * 1000;
		if (currentTime - lastActivityTime > timeout) {
			this.destroy();
			this.config.onTimeout();
		}
	}

	/** 销毁实例 */
	public destroy() {
		this.config.events.forEach((event) => {
			window.removeEventListener(event, this.debouncedReset);
		});
		document.removeEventListener('visibilitychange', this.handleVisibilityChange);

		if (this.timerId !== null) {
			window.clearInterval(this.timerId);
			this.timerId = null;
		}
	}

	/** 超时时执行 */
	private timeOutExec() {
		// 移除 app 元素，即清空主界面
		const appEl = document.getElementById('app')!;
		appEl?.remove();

		// 动态加载 signalR 并调用 stop 方法
		loadSignalR().then((signalR) => {
			signalR.stop();
		});

		// TODO: 如果要改成调用 logout 登出接口，需要调整 clearAccessTokens 会 reload 页面的问题

		// 清除 token
		Local.remove(accessTokenKey);
		Local.remove(refreshAccessTokenKey);

		// 清除其他
		Session.clear();

		ElMessageBox.alert(i18n.global.t('message.list.idleTimeoutMessage'), i18n.global.t('message.list.sysMessage'), {
			type: 'warning',
			draggable: true,
			callback: () => {
				window.location.reload();
			},
		});
	}
}

/** 初始化函数（在应用启动时调用） */
export function initIdleTimeout(config?: IdleTimeoutConfig) {
	// 确保单例模式
	if (!window.__IDLE_TIMEOUT__) {
		window.__IDLE_TIMEOUT__ = new IdleTimeoutManager(config);
	}
	return window.__IDLE_TIMEOUT__;
}

/** 销毁函数（在需要时调用） */
export function destroyIdleTimeout() {
	if (window.__IDLE_TIMEOUT__) {
		window.__IDLE_TIMEOUT__.destroy();
		window.__IDLE_TIMEOUT__ = undefined;
	}
}

/** 更新空闲超时时间（毫秒） */
export function updateIdleTimeout(timeout: number) {
	if (window.__IDLE_TIMEOUT__) {
		window.__IDLE_TIMEOUT__.updateIdleTimeout(timeout);
	}
}

// 类型扩展
declare global {
	interface Window {
		__IDLE_TIMEOUT__?: IdleTimeoutManager;
	}
}
