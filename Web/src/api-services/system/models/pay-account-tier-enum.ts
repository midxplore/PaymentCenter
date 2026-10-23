/* tslint:disable */
/* eslint-disable */
/**
 * 收款账号分层
 * @export
 * @enum {number}
 */
export enum PayAccountTierEnum {
	NUMBER_1 = 1,
	NUMBER_2 = 2,
}

export function PayAccountTierEnumFromJSON(json: any): PayAccountTierEnum {
	return PayAccountTierEnumFromJSONTyped(json, false);
}

export function PayAccountTierEnumFromJSONTyped(json: any, _ignoreDiscriminator: boolean): PayAccountTierEnum {
	return json as PayAccountTierEnum;
}

export function PayAccountTierEnumToJSON(value?: PayAccountTierEnum | null): any {
	return value as any;
}
