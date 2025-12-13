export interface MosNoticeListItem {
  id: string;
  purchaseNumber: string;
  name: string | null;
  publishDate: string | null;
  collectingEnd: string | null;
  maxPrice: number | null;
  federalLawName: string | null;
  region: number;
  customerInn: string | null;
  customerName: string | null;
}

export interface MosNoticeListResponse {
  items: MosNoticeListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface MosNoticeQuery {
  page: number;
  pageSize: number;
  search?: string;
  sortField?: string;
  sortDirection?: string;
}
