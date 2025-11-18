import { TenderAnalysisResult } from '../models/notice-analysis.models';

export interface NoticeAnalysisDialogData {
  noticeId: string;
  purchaseNumber: string;
  entryName: string;
  result: string | null;
  completedAt: string | null;
  prompt?: string | null;
  structuredResult?: TenderAnalysisResult | null;
  decisionScore?: number | null;
  recommended?: boolean | null;
}
