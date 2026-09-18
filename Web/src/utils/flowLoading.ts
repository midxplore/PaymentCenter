// utils/flowLoading.ts
/**
 * @author easycreatecode.com
 * @created 2025-03-05
 * @updated 2025-03-08
 * @version 1.2.0
 * @description 基于Element Plus的流程加载控制器，支持链式操作和动态文本更新，如果使用请保留作者信息
 */
import { ElLoading } from 'element-plus';
import type { Component, Ref } from 'vue';
import { isRef, unref } from 'vue';
import type { LoadingOptions } from 'element-plus/es/components/loading/src/types';

// 1. 明确控制器类型接口
interface LoadingController {
	step: (text: string | Ref<string>) => LoadingController;
	end: () => void;
}

type CustomLoadingOptions = Omit<LoadingOptions, 'text'> & {
	text?: string | Ref<string>;
	spinner?: string | Component;
};

const DEFAULT_CONFIG: CustomLoadingOptions = {
	lock: true,
	text: '',
	//background: 'rgba(0, 199, 255, 0.3)',
	background: 'rgba(94, 158, 214, 0.12)', // 景德镇青花色
	//spinner: 'circle-check'
	//spinner: 'el-icon-potato',
	spinner: 'el-icon-loading',
};

class FlowLoadingManager {
	private instance: ReturnType<typeof ElLoading.service> | null = null;

	// 2. 统一返回类型
	start(options?: string | Ref<string> | CustomLoadingOptions): LoadingController {
		try {
			if (this.instance) {
				console.warn('Existing loading instance detected');
				return this.createController();
			}

			const resolved = this.normalizeOptions(options);
			this.validateConfig(resolved);

			this.instance = ElLoading.service(resolved as LoadingOptions);
			return this.createController();
		} catch (e) {
			console.error('Loading initialization failed:', e);
			return this.createController(true); // 传入错误标记
		}
	}

	private normalizeOptions(options?: string | Ref<string> | CustomLoadingOptions): CustomLoadingOptions {
		const baseConfig = { ...DEFAULT_CONFIG };

		if (!options) return baseConfig;

		if (typeof options === 'string' || isRef(options)) {
			return {
				...baseConfig,
				text: unref(options),
			};
		}

		return {
			...baseConfig,
			...options,
			text: options.text ? unref(options.text) : baseConfig.text,
		};
	}

	private validateConfig(config: CustomLoadingOptions) {
		if (config.spinner && typeof config.spinner !== 'string') {
			console.warn('Custom spinner components require proper registration');
		}

		if (typeof config.text !== 'string') {
			throw new Error('Loading text must be a string');
		}
	}

	// 3. 增强控制器创建方法
	private createController(isError = false): LoadingController {
		return {
			step: (text: string | Ref<string>) => {
				if (!isError && this.instance) {
					this.instance.setText(unref(text));
				}
				return this.createController(isError); // 保持链式调用
			},
			end: () => {
				if (!isError) {
					this.instance?.close();
				}
				this.instance = null;
			},
		};
	}
}

export const flowLoading = new FlowLoadingManager();
