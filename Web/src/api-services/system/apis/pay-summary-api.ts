/* tslint:disable */
/* eslint-disable */
/**
 * 运营汇总 API（手写；与 PaySummaryService 对齐）
 */
import globalAxios, { AxiosResponse, AxiosInstance, AxiosRequestConfig } from 'axios';
import { Configuration } from '../configuration';
// @ts-ignore
import { BASE_PATH, BaseAPI, RequiredError } from '../base';
import { AdminNETResultPayOpsSummaryOutput } from '../models';
import { PayOpsDailyInput } from '../models';

export const PaySummaryApiAxiosParamCreator = function (configuration?: Configuration) {
	return {
		apiPaySummaryDailyPost: async (body?: PayOpsDailyInput, options: AxiosRequestConfig = {}) => {
			const localVarPath = `/api/paySummary/daily`;
			const localVarUrlObj = new URL(localVarPath, 'https://example.com');
			let baseOptions;
			if (configuration) baseOptions = configuration.baseOptions;
			const localVarRequestOptions: AxiosRequestConfig = { method: 'POST', ...baseOptions, ...options };
			const localVarHeaderParameter = {} as any;
			if (configuration && configuration.accessToken) {
				const accessToken =
					typeof configuration.accessToken === 'function' ? await configuration.accessToken() : await configuration.accessToken;
				localVarHeaderParameter['Authorization'] = 'Bearer ' + accessToken;
			}
			localVarHeaderParameter['Content-Type'] = 'application/json-patch+json';
			localVarRequestOptions.headers = { ...localVarHeaderParameter, ...(baseOptions && baseOptions.headers), ...options.headers };
			localVarRequestOptions.data = JSON.stringify(body !== undefined ? body : {});
			return {
				url: localVarUrlObj.pathname + localVarUrlObj.search + localVarUrlObj.hash,
				options: localVarRequestOptions,
			};
		},
		apiPaySummaryExportDailyPost: async (body?: PayOpsDailyInput, options: AxiosRequestConfig = {}) => {
			const localVarPath = `/api/paySummary/exportDaily`;
			const localVarUrlObj = new URL(localVarPath, 'https://example.com');
			let baseOptions;
			if (configuration) baseOptions = configuration.baseOptions;
			const localVarRequestOptions: AxiosRequestConfig = { method: 'POST', ...baseOptions, ...options };
			const localVarHeaderParameter = {} as any;
			if (configuration && configuration.accessToken) {
				const accessToken =
					typeof configuration.accessToken === 'function' ? await configuration.accessToken() : await configuration.accessToken;
				localVarHeaderParameter['Authorization'] = 'Bearer ' + accessToken;
			}
			localVarHeaderParameter['Content-Type'] = 'application/json-patch+json';
			localVarRequestOptions.headers = { ...localVarHeaderParameter, ...(baseOptions && baseOptions.headers), ...options.headers };
			localVarRequestOptions.data = JSON.stringify(body !== undefined ? body : {});
			return {
				url: localVarUrlObj.pathname + localVarUrlObj.search + localVarUrlObj.hash,
				options: localVarRequestOptions,
			};
		},
	};
};

export const PaySummaryApiFp = function (configuration?: Configuration) {
	return {
		async apiPaySummaryDailyPost(body?: PayOpsDailyInput, options?: AxiosRequestConfig) {
			const localVarAxiosArgs = await PaySummaryApiAxiosParamCreator(configuration).apiPaySummaryDailyPost(body, options);
			return (axios: AxiosInstance = globalAxios, basePath: string = BASE_PATH) => {
				const axiosRequestArgs: AxiosRequestConfig = { ...localVarAxiosArgs.options, url: basePath + localVarAxiosArgs.url };
				return axios.request(axiosRequestArgs) as Promise<AxiosResponse<AdminNETResultPayOpsSummaryOutput>>;
			};
		},
		async apiPaySummaryExportDailyPost(body?: PayOpsDailyInput, options?: AxiosRequestConfig) {
			const localVarAxiosArgs = await PaySummaryApiAxiosParamCreator(configuration).apiPaySummaryExportDailyPost(body, options);
			return (axios: AxiosInstance = globalAxios, basePath: string = BASE_PATH) => {
				const axiosRequestArgs: AxiosRequestConfig = { ...localVarAxiosArgs.options, url: basePath + localVarAxiosArgs.url };
				return axios.request(axiosRequestArgs);
			};
		},
	};
};

export class PaySummaryApi extends BaseAPI {
	public async apiPaySummaryDailyPost(body?: PayOpsDailyInput, options?: AxiosRequestConfig) {
		return PaySummaryApiFp(this.configuration)
			.apiPaySummaryDailyPost(body, options)
			.then((request) => request(this.axios, this.basePath));
	}
	public async apiPaySummaryExportDailyPost(body?: PayOpsDailyInput, options?: AxiosRequestConfig) {
		return PaySummaryApiFp(this.configuration)
			.apiPaySummaryExportDailyPost(body, options)
			.then((request) => request(this.axios, this.basePath));
	}
}
