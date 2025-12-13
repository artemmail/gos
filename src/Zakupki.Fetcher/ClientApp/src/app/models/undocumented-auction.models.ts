export interface UndocumentedAuctionDto {
  name?: string | null;
  createdByCustomer?: UndocumentedCompanyDto | null;
  state?: UndocumentedAuctionStateDto | null;
  startDate?: string | null;
  initialDuration?: number | null;
  endDate?: string | null;
  startCost?: number | null;
  nextCost?: number | null;
  lastBetSupplier?: UndocumentedCompanyDto | null;
  lastBetCost?: number | null;
  lastBetId?: number | null;
  step?: number | null;
  federalLawName?: string | null;
  conclusionReasonName?: string | null;
  auctionRegion?: UndocumentedAuctionRegionDto[] | null;
  files?: UndocumentedAuctionFileDto[] | null;
  customer?: UndocumentedCompanyDto | null;
  licenseFiles?: UndocumentedAuctionFileDto[] | null;
  auctionItem?: UndocumentedAuctionItemDto[] | null;
  bets?: UndocumentedAuctionBetDto[] | null;
  offerSignTime?: string | null;
  uniqueSupplierCount?: number | null;
  repeatId?: number | null;
  unpublishName?: string | null;
  unpublishDate?: string | null;
  items?: UndocumentedAuctionItemDto[] | null;
  deliveries?: UndocumentedAuctionDeliveryDto[] | null;
  offersSigned?: boolean | null;
  showPurchaseRequestMessageIfFailed?: boolean | null;
  purchaseTypeId?: number | null;
  contractCost?: number | null;
  contracts?: UndocumentedAuctionContractDto[] | null;
  unpublishComment?: string | null;
  externalId?: string | null;
  isElectronicContractExecutionRequired?: boolean | null;
  isContractGuaranteeRequired?: boolean | null;
  contractGuaranteeAmount?: number | null;
  rowVersion?: string | null;
  organizingTypeId?: number | null;
  sharedPurchaseBuyers?: UndocumentedCompanyDto[] | null;
  suppliersAutobetSettings?: UndocumentedAutobetSettingsDto[] | null;
  isLicenseProduction?: boolean | null;
  uploadLicenseDocumentsComment?: string | null;
  isExternalIntegration?: boolean | null;
  shortDescription?: UndocumentedShortDescriptionDto | null;
  id?: number | null;
}

export interface UndocumentedAuctionRegionDto {
  treePathId?: string | null;
  socr?: string | null;
  id?: number | null;
  oktmo?: string | null;
  code?: string | null;
  name?: string | null;
}

export interface UndocumentedAuctionFileDto {
  companyId?: number | null;
  id?: number | null;
  name?: string | null;
}

export interface UndocumentedAuctionStateDto {
  name?: string | null;
  id?: number | null;
}

export interface UndocumentedCompanyDto {
  inn?: string | null;
  name?: string | null;
  id?: number | null;
}

export interface UndocumentedAuctionItemDto {
  currentValue?: number | null;
  costPerUnit?: number | null;
  okeiName?: string | null;
  createdOfferId?: number | null;
  skuId?: number | null;
  imageId?: number | null;
  defaultImageId?: number | null;
  okpdName?: string | null;
  productionDirectoryName?: string | null;
  oksm?: string | null;
  name?: string | null;
  id?: number | null;
  quantity?: number | null;
}

export interface UndocumentedAuctionDeliveryDto {
  periodDaysFrom?: number | null;
  periodDaysTo?: number | null;
  periodDateFrom?: string | null;
  periodDateTo?: string | null;
  deliveryPlace?: string | null;
  quantity?: number | null;
  items?: UndocumentedAuctionDeliveryItemDto[] | null;
  id?: number | null;
}

export interface UndocumentedAuctionDeliveryItemDto {
  sum?: number | null;
  costPerUnit?: number | null;
  quantity?: number | null;
  name?: string | null;
  buyerId?: number | null;
  isBuyerInvitationSent?: boolean | null;
  isApprovedByBuyer?: boolean | null;
}

export interface UndocumentedAuctionBetDto {
  id?: number | null;
  amount?: number | null;
  createdAt?: string | null;
  supplier?: UndocumentedCompanyDto | null;
}

export interface UndocumentedAuctionContractDto {
  id?: number | null;
  cost?: number | null;
}

export interface UndocumentedAutobetSettingsDto {
  supplier?: UndocumentedCompanyDto | null;
  maxAmount?: number | null;
}

export interface UndocumentedShortDescriptionDto {
  fileName?: string | null;
  data?: UndocumentedShortDescriptionEntryDto[] | null;
}

export interface UndocumentedShortDescriptionEntryDto {
  title?: string | null;
  summary?: string | null;
}
