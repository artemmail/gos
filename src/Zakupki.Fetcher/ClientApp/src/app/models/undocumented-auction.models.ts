export interface UndocumentedAuctionDto {
  name?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  startCost?: number | null;
  federalLawName?: string | null;
  auctionRegion?: UndocumentedAuctionRegionDto[] | null;
  files?: UndocumentedAuctionFileDto[] | null;
  customer?: UndocumentedCompanyDto | null;
  id?: number | null;
}

export interface UndocumentedAuctionRegionDto {
  id?: number | null;
}

export interface UndocumentedAuctionFileDto {
  id?: number | null;
  name?: string | null;
}

export interface UndocumentedCompanyDto {
  inn?: string | null;
  name?: string | null;
  id?: number | null;
}
