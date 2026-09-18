import type { FormItemRule } from 'element-plus';
import { ElMessage } from 'element-plus';
import { nextTick, Ref, watch } from 'vue';

const TRIGGER_INPUT: FormItemRule['trigger'] = 'change';
const TRIGGER_BLUR: FormItemRule['trigger'] = 'blur';

export function useFormRulePresets() {
	const required = (label = '此项', trigger: FormItemRule['trigger'] = TRIGGER_BLUR): FormItemRule => ({
		required: true,
		message: `${label}为必填项`,
		trigger,
	});
	//TODO 如果数据发生变化，自动触发验证取消错误标记
	const requiredIf = (required?: boolean, label = '此项', trigger: FormItemRule['trigger'] = TRIGGER_BLUR): FormItemRule => ({
		required: required ?? false,
		message: `${label}为必填项`,
		trigger,
	});

	const validatorIf = (shouldError: () => boolean, formRef?: Ref<any>, label: string = '此项') => {
		let field = '';
		watch(
			() => shouldError(),
			() => nextTick(() => formRef?.value?.validateField(field).catch(() => {})),
			{ immediate: false, deep: true }
		);

		return {
			validator: (_: any, __: any, callback: (error?: Error) => void) => {
				field = _.field;
				if (shouldError()) {
					callback(new Error(`${label}为必填`));
				} else {
					callback();
				}
			},
			trigger: ['blur', 'change'] as const,
		};
	};

	const maxLen = (len: number, label = '此项', trigger = TRIGGER_INPUT): FormItemRule => ({
		max: len,
		message: `${label}长度不能超过 ${len} 个字符`,
		trigger,
	});

	const minLen = (len: number, label = '此项', trigger = TRIGGER_INPUT): FormItemRule => ({
		min: len,
		message: `${label}长度不能少于 ${len} 个字符`,
		trigger,
	});

	const pattern = (re: RegExp, msg: string, trigger = TRIGGER_BLUR): FormItemRule => ({
		pattern: re,
		message: msg,
		trigger,
	});

	const validator = (fn: (value: any) => true | string | Promise<true | string>, trigger = TRIGGER_BLUR): FormItemRule => ({
		trigger,
		validator: (_rule, value, callback) => {
			Promise.resolve(fn(value))
				.then((res) => {
					if (res === true) return callback();
					return callback(new Error(res));
				})
				.catch((err) => callback(err instanceof Error ? err : new Error(String(err))));
		},
	});

	const phone = () => pattern(/^(1[3-9]\d{9})$|^(0\d{2,3}[- ]?\d{7,8})$|^(\(\d{3,4}\)\d{7,8})$/, '请输入有效的手机号或固定电话');
	const idCard = () =>
		validator((value) => {
			if (!value) return true; // 如果为空，让required规则处理

			const idCard = String(value).trim();

			// 15位身份证号验证（老身份证）
			if (idCard.length === 15) {
				const reg15 = /^[1-9]\d{5}\d{2}((0[1-9])|(1[0-2]))(([0|1|2]\d)|3[0-1])\d{3}$/;
				if (!reg15.test(idCard)) {
					return '请输入有效的15位身份证号';
				}
				return true;
			}

			// 18位身份证号验证（新身份证）
			if (idCard.length === 18) {
				const reg18 = /^[1-9]\d{5}(18|19|20)\d{2}((0[1-9])|(1[0-2]))(([0|1|2]\d)|3[0-1])\d{3}(\d|X|x)$/;
				if (!reg18.test(idCard)) {
					return '请输入有效的18位身份证号';
				}

				// 验证校验码
				const factor = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2];
				const parity = ['1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2'];
				let sum = 0;
				let ai = 0;
				let wi = 0;

				for (let i = 0; i < 17; i++) {
					ai = parseInt(idCard[i]);
					wi = factor[i];
					sum += ai * wi;
				}

				const last = parity[sum % 11];
				if (last !== idCard[17].toUpperCase()) {
					return '身份证号校验码错误';
				}

				return true;
			}

			return '身份证号必须是15位或18位';
		}, TRIGGER_BLUR);

	const email = () => pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/, '请输入有效的邮箱地址');

	const digits = (label = '此项', trigger = TRIGGER_INPUT) => pattern(/^\d+$/, `${label}需为纯数字`, trigger);

	const intRange = (min: number, max: number, label = '数值') =>
		validator((v) => {
			if (v === '' || v === undefined || v === null) return `请输入${label}`;
			if (!Number.isInteger(Number(v))) return `${label}需为整数`;
			const n = Number(v);
			if (n < min || n > max) return `${label}需在 ${min}~${max} 之间`;
			return true;
		}, TRIGGER_INPUT);

	const confirmSame = (getOther: () => any, label = '两次输入') => validator((v) => (v === getOther() ? true : `${label}不一致`), TRIGGER_INPUT);

	const toDate = (v: any): Date | null => {
		if (v instanceof Date && !isNaN(v.getTime())) return v;
		if (typeof v === 'number') {
			const d = new Date(v);
			return isNaN(d.getTime()) ? null : d;
		}
		if (typeof v === 'string' && v) {
			const d = new Date(v);
			return isNaN(d.getTime()) ? null : d;
		}
		return null;
	};

	const dateAfterToday = (label = '日期', trigger = TRIGGER_INPUT) =>
		validator((v) => {
			if (v === '' || v === undefined || v === null) return true;
			const d = toDate(v);
			if (!d) return `请选择有效的${label}`;
			const today = new Date();
			today.setHours(0, 0, 0, 0);
			return d.getTime() > today.getTime() ? true : `${label}必须在今天之后`;
		}, trigger);

	const dateBeforeToday = (label = '日期', trigger = TRIGGER_INPUT) =>
		validator((v) => {
			if (v === '' || v === undefined || v === null) return true;
			const d = toDate(v);
			if (!d) return `请选择有效的${label}`;
			const today = new Date();
			today.setHours(0, 0, 0, 0);
			return d.getTime() < today.getTime() ? true : `${label}必须在今天之前`;
		}, trigger);

	const numberPrecision = (maxIntDigits: number | null, maxFracDigits: number | null, label = '此项') =>
		validator((v) => {
			if (v === '' || v === undefined || v === null) return true;

			const str = String(v).trim();

			// 允许 0、整数或小数（正数），不允许负数与多余小数点
			if (!/^\d+(?:\.\d+)?$/.test(str)) {
				return `${label}必须为数字`;
			}

			const [intPart, fracPart = ''] = str.split('.');
			if (maxIntDigits !== null) {
				if (intPart.length > maxIntDigits) {
					return `${label}整数位不能超过${maxIntDigits}位`;
				}
			}

			if (fracPart && maxFracDigits !== null && fracPart.length > maxFracDigits) {
				if (maxFracDigits === 0) {
					return `${label}必须为整数`;
				}
				return `${label}小数位不能超过${maxFracDigits}位`;
			}

			return true;
		}, TRIGGER_BLUR);

	// ========== 直接显示错误的验证函数（直接用于元素）==========

	// 长度验证函数
	const validateLength = (value: any, maxLength: number, fieldName = '字段') => {
		if (!value) return true;
		if (String(value).length > maxLength) {
			ElMessage.error(`${fieldName}长度不能超过${maxLength}个字符`);
			return false;
		}
		return true;
	};

	// 必填验证函数
	const validateRequired = (value: any, fieldName = '字段') => {
		if (!value || String(value).trim() === '') {
			ElMessage.error(`${fieldName}不能为空`);
			return false;
		}
		return true;
	};

	// 数字验证函数
	const validateNumber = (value: any, fieldName = '字段') => {
		if (!value) return true;
		if (!/^\d+$/.test(String(value))) {
			ElMessage.error(`${fieldName}必须为纯数字`);
			return false;
		}
		return true;
	};

	// 整数范围验证函数
	const validateIntRange = (value: any, min: number, max: number, fieldName = '字段') => {
		if (!value) return true;
		const num = Number(value);
		if (!Number.isInteger(num)) {
			ElMessage.error(`${fieldName}必须为整数`);
			return false;
		}
		if (num < min || num > max) {
			ElMessage.error(`${fieldName}必须在${min}~${max}之间`);
			return false;
		}
		return true;
	};

	// 中文验证函数
	const validateChinese = (value: any, fieldName = '字段') => {
		if (!value) return true;
		if (!/^[\u4e00-\u9fa5]+$/.test(String(value))) {
			ElMessage.error(`${fieldName}必须为中文`);
			return false;
		}
		return true;
	};

	// 邮箱验证函数
	const validateEmail = (value: any, fieldName = '字段') => {
		if (!value) return true;
		if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(value))) {
			ElMessage.error(`${fieldName}格式不正确`);
			return false;
		}
		return true;
	};

	// 手机号验证函数
	const validatePhone = (value: any, fieldName = '字段') => {
		if (!value) return true;
		if (!/^1[3-9]\d{9}$/.test(String(value))) {
			ElMessage.error(`${fieldName}格式不正确`);
			return false;
		}
		return true;
	};

	// 身份证号验证函数
	const validateIdCard = (value: any, fieldName = '字段') => {
		if (!value) return true;
		const idCard = String(value).trim();

		if (idCard.length === 15) {
			const reg15 = /^[1-9]\d{5}\d{2}((0[1-9])|(1[0-2]))(([0|1|2]\d)|3[0-1])\d{3}$/;
			if (!reg15.test(idCard)) {
				ElMessage.error(`${fieldName}格式不正确`);
				return false;
			}
			return true;
		}

		if (idCard.length === 18) {
			const reg18 = /^[1-9]\d{5}(18|19|20)\d{2}((0[1-9])|(1[0-2]))(([0|1|2]\d)|3[0-1])\d{3}(\d|X|x)$/;
			if (!reg18.test(idCard)) {
				ElMessage.error(`${fieldName}格式不正确`);
				return false;
			}

			// 验证校验码
			const factor = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2];
			const parity = ['1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2'];
			let sum = 0;

			for (let i = 0; i < 17; i++) {
				sum += parseInt(idCard[i]) * factor[i];
			}

			const last = parity[sum % 11];
			if (last !== idCard[17].toUpperCase()) {
				ElMessage.error(`${fieldName}校验码错误`);
				return false;
			}
			return true;
		}

		ElMessage.error(`${fieldName}必须是15位或18位`);
		return false;
	};

	// 自定义正则验证函数
	const validatePattern = (value: any, pattern: RegExp, message: string) => {
		if (!value) return true;
		if (!pattern.test(String(value))) {
			ElMessage.error(message);
			return false;
		}
		return true;
	};

	// 组合验证函数（可同时验证多个规则）
	const validateMultiple = (value: any, validators: Array<(value: any) => boolean>) => {
		for (const validator of validators) {
			if (!validator(value)) {
				return false;
			}
		}
		return true;
	};

	// 验证小数和整数位数的函数
	const validateDecimalPrecision = (value: any, maxIntDigits: number, maxFracDigits: number, fieldName = '字段') => {
		if (!value) return true;

		const str = String(value).trim();

		// 允许 0、整数或小数（正数），不允许负数与多余小数点
		if (!/^\d+(?:\.\d+)?$/.test(str)) {
			ElMessage.error(`${fieldName}必须为数字`);
			return false;
		}

		const [intPart, fracPart = ''] = str.split('.');

		// 验证整数位数
		if (intPart.length > maxIntDigits) {
			ElMessage.error(`${fieldName}整数位不能超过${maxIntDigits}位`);
			return false;
		}

		// 验证小数位数
		if (fracPart && fracPart.length > maxFracDigits) {
			if (maxFracDigits === 0) {
				ElMessage.error(`${fieldName}必须为整数`);
				return false;
			}
			ElMessage.error(`${fieldName}小数位不能超过${maxFracDigits}位`);
			return false;
		}

		return true;
	};

	// 通用验证函数（根据类型自动选择验证规则）
	const validateField = (value: any, type: string, fieldName: string, options?: any) => {
		switch (type) {
			case 'required':
				return validateRequired(value, fieldName);
			case 'length':
				return validateLength(value, options.maxLength, fieldName);
			case 'number':
				return validateNumber(value, fieldName);
			case 'range':
				return validateIntRange(value, options.min, options.max, fieldName);
			case 'chinese':
				return validateChinese(value, fieldName);
			case 'email':
				return validateEmail(value, fieldName);
			case 'phone':
				return validatePhone(value, fieldName);
			case 'idcard':
				return validateIdCard(value, fieldName);
			case 'pattern':
				return validatePattern(value, options.pattern, options.message);
			case 'decimal':
				return validateDecimalPrecision(value, options.maxIntDigits, options.maxFracDigits, fieldName);
			default:
				return true;
		}
	};

	return {
		required,
		requiredIf,
		validatorIf,
		maxLen,
		minLen,
		pattern,
		validator,
		phone,
		idCard,
		email,
		digits,
		intRange,
		confirmSame,
		dateAfterToday,
		dateBeforeToday,
		numberPrecision,
		validateRequired,
		validateNumber,
		validateIntRange,
		validateChinese,
		validateLength,
		validateEmail,
		validatePhone,
		validateIdCard,
		validatePattern,
		validateMultiple,
		validateField,
		validateDecimalPrecision,
	};
}
