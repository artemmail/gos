export interface FabrikantLot {
  number: string;
  name: string;
  startPrice: number | null;
  currency: string;
  quantity: number | null;
  unit: string;
  status: string;
  deliveryAddress: string;
  deliveryTerm: string;
  paymentTerms: string;
  additionalFields: Record<string, string>;
  rawRow: string;
}

export interface FabrikantDocumentLink {
  url: string;
  fileName: string;
}

export interface FabrikantProcedure {
  noticeId: string;
  externalId: string;
  procedureNumber: string;
  lawSection: string;
  title: string;
  procedureType: string;
  status: string;

  organizerName: string;
  organizerInn: string;
  organizerKpp: string;
  organizerAddress: string;

  customerName: string;
  customerFullName: string;
  customerInn: string;
  customerKpp: string;

  publishDate: string | null;
  applyStartDate: string | null;
  applyEndDate: string | null;
  resultDate: string | null;

  nmck: number | null;
  currency: string;

  okpd2: string;
  okved2: string;
  itemName: string;
  quantity: number | null;
  unit: string;
  deliveryAddress: string;
  deliveryTerm: string;
  paymentTerms: string;
  applicationSecurity: number | null;
  contractSecurity: number | null;

  lots: FabrikantLot[];
  documents: FabrikantDocumentLink[];

  rawJson: string;
  rawHtml: string;
}

export interface FabNoticeListResponse {
  items: FabrikantProcedure[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface FabNoticeQuery {
  page: number;
  pageSize: number;
  search?: string;
  sortField?: string;
  sortDirection?: string;
}
