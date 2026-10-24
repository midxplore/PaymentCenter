/* tslint:disable */
/* eslint-disable */
export interface PayOpsDailyRowOutput {
	bizDate?: string;
	quotaInject?: number;
	quotaReclaim?: number;
	quotaNet?: number;
	lockOccurred?: number;
	receivedCompleted?: number;
	completedCount?: number;
	expireReleased?: number;
	markUnpaidCount?: number;
	accountAddCount?: number;
	quotaAdjustCount?: number;
	accountUpdateCount?: number;
	hasSnapshot?: boolean;
	eodTotalQuotaSum?: number | null;
	eodUsedQuotaSum?: number | null;
	eodLockedQuotaSum?: number | null;
	eodRemainingQuotaSum?: number | null;
	eodUnpaidLockedSum?: number | null;
	eodAccountEnabledCount?: number | null;
}
