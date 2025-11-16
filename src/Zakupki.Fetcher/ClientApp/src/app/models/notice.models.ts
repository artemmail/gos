export interface NoticeListItem {
  id: string;
  purchaseNumber: string;
  entryName: string;
  purchaseObjectInfo: string | null;
  maxPrice: number | null;
  maxPriceCurrencyCode: string | null;
  maxPriceCurrencyName: string | null;
  okpd2Code: string | null;
  okpd2Name: string | null;
  kvrCode: string | null;
  kvrName: string | null;
  publishDate: string | null;
  etpName: string | null;
  documentType: string;
  source: string;
  updatedAt: string;
  region: string | null;
  period: string | null;
  placingWayName: string | null;
  collectingEnd: string | null;
  submissionProcedureDateRaw: string | null;
  rawJson: string | null;
  computedRegion?: string | null;
  hasAnalysisAnswer: boolean;
  analysisStatus: string | null;
  analysisUpdatedAt: string | null;
  isFavorite: boolean;
}

export interface NoticeListResponse {
  items: NoticeListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface NoticeQuery {
  page: number;
  pageSize: number;
  search?: string;
  purchaseNumber?: string;
  okpd2Codes?: string;
  kvrCodes?: string;
  sortField?: string;
  sortDirection?: string;
}
