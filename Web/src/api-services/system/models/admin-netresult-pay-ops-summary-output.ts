/* tslint:disable */
/* eslint-disable */
import { PayOpsSummaryOutput } from './pay-ops-summary-output';

export interface AdminNETResultPayOpsSummaryOutput {
	code?: number;
	type?: string | null;
	message?: string | null;
	result?: PayOpsSummaryOutput;
	extras?: any | null;
	time?: Date;
}
