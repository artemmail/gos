export interface NoticeListItem {
  id: string;
  purchaseNumber: string;
  purchaseObjectInfo: string | null;
  maxPrice: number | null;
  okpd2Code: string | null;
  okpd2Name: string | null;
  kvrCode: string | null;
  kvrName: string | null;
  publishDate: string | null;
  etpName: string | null;
  region: string | null;
  collectingEnd: string | null;
  submissionProcedureDateRaw: string | null;
  rawJson: string | null;
  hasAnalysisAnswer: boolean;
  analysisStatus: string | null;
  analysisUpdatedAt: string | null;
  isFavorite: boolean;
  similarity: number | null;
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
  expiredOnly: boolean;
  filterByUserRegions?: boolean;
  filterByUserOkpd2Codes?: boolean;
  search?: string;
  purchaseNumber?: string;
  sortField?: string;
  sortDirection?: string;
}

export interface NoticeVectorQuery {
  page: number;
  pageSize: number;
  queryVectorId: string;
  similarityThresholdPercent: number;
  expiredOnly: boolean;
  collectingEndLimit: string;
  filterByUserRegions?: boolean;
  filterByUserOkpd2Codes?: boolean;
}
