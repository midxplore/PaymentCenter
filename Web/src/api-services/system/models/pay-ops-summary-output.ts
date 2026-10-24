/* tslint:disable */
/* eslint-disable */
import { PayOpsDailyRowOutput } from './pay-ops-daily-row-output';
import { PayOpsPoolNowOutput } from './pay-ops-pool-now-output';

export interface PayOpsSummaryOutput {
	current?: PayOpsPoolNowOutput;
	days?: Array<PayOpsDailyRowOutput> | null;
	footnote?: string | null;
}
