export interface NoticeListItem {
  id: string;
  purchaseNumber: string;
  entryName: string;
  publishDate: string | null;
  etpName: string | null;
  documentType: string;
  source: string;
  updatedAt: string;
  region: string | null;
  period: string | null;
  placingWayName: string | null;
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
  sortField?: string;
  sortDirection?: string;
}
