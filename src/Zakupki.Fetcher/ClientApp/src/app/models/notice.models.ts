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
  source: number;
  collectingEnd: string | null;
  submissionProcedureDateRaw: string | null;
  rawJson: string | null;
  hasAnalysisAnswer: boolean;
  analysisStatus: string | null;
  analysisUpdatedAt: string | null;
  recommended: boolean | null;
  decisionScore: number | null;
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
  sortField?: string;
  sortDirection?: string;
}

export interface NoticeDetails {
  id: string;
  purchaseNumber: string;
  purchaseObjectInfo: string | null;
  rawJson: string | null;
}

export interface NoticeCommonInfo {
  schemeVersion: string;
  id: string;
  externalId: string;
  versionNumber: number;
  maxPrice?: number | null;
  commonInfo: {
    purchaseNumber: string;
    docNumber: string;
    plannedPublishDateRaw?: string;
    publishDtInEis?: string;
    href?: string;
    notPublishedOnEis?: boolean;
    placingWay?: { code: string; name: string };
    etp?: { code: string; name: string; url?: string };
    contractConclusionOnSt83Ch2?: boolean;
    purchaseObjectInfo?: string;
  };
  purchaseResponsibleInfo?: {
    responsibleOrgInfo?: {
      regNum?: string;
      consRegistryNum?: string;
      fullName?: string;
      shortName?: string;
      postAddress?: string;
      factAddress?: string;
      inn?: string;
      kpp?: string;
    };
    responsibleRole?: string;
    responsibleInfo?: {
      orgPostAddress?: string;
      orgFactAddress?: string;
      contactPersonInfo?: {
        lastName?: string;
        firstName?: string;
        middleName?: string;
      };
      contactEmail?: string;
      contactPhone?: string;
    };
    specializedOrgInfo?: {
      regNum?: string;
      consRegistryNum?: string;
      fullName?: string;
      shortName?: string;
      postAddress?: string;
      factAddress?: string;
      inn?: string;
      kpp?: string;
    };
  };
  printFormInfo?: { url?: string };
  attachmentsInfo?: {
    items?: Array<{
      publishedContentId: string;
      fileName: string;
      fileSize: number;
      docDescription?: string;
      docDate?: string;
      url?: string;
      docKindInfo?: { code?: string; name?: string };
    }>;
  };
  serviceSigns?: {
    isIncludeKoks?: boolean;
    isControlP1Ch5St99?: boolean;
  };
  notificationInfo?: {
    procedureInfo?: {
      collectingInfo?: {
        startDt?: string;
        endDt?: string;
      };
      biddingDateRaw?: string;
      summarizingDateRaw?: string;
    };
    contractConditionsInfo?: {
      maxPriceInfo?: {
        maxPrice?: number;
        currency?: { code?: string; name?: string };
      };
      standardContractNumber?: string;
      isOneSideRejectionSt95?: boolean;
    };
    customerRequirementsInfo?: {
      items?: Array<{
        customer?: {
          regNum?: string;
          consRegistryNum?: string;
          fullName?: string;
        };
        applicationGuarantee?: {
          amount?: number;
          account?: {
            bik?: string;
            settlementAccount?: string;
            personalAccount?: string;
            creditOrgName?: string;
            corrAccountNumber?: string;
          };
          procedureInfo?: string;
        };
        contractGuarantee?: {
          account?: {
            bik?: string;
            settlementAccount?: string;
            personalAccount?: string;
            creditOrgName?: string;
            corrAccountNumber?: string;
          };
          procedureInfo?: string;
        };
        innerContractConditionsInfo?: {
          maxPriceInfo?: { maxPrice?: number };
          deliveryPlacesInfo?: {
            items?: Array<{
              sid?: string;
              externalSid?: string;
              countryInfo?: {
                countryCode?: string;
                countryFullName?: string;
              };
              garInfo?: {
                garGuid?: string;
                garAddress?: string;
              };
            }>;
          };
          isOneSideRejectionSt95?: boolean;
        };
        warrantyInfo?: {
          warrantyServiceRequirement?: string;
          warrantyTerm?: string;
        };
        addInfo?: string;
      }>;
    };
    purchaseObjectsInfo?: {
      notDrugPurchaseObjectsInfo?: {
        items?: Array<{
          sid?: string;
          name?: string;
          ktru?: {
            code?: string;
            name?: string;
            okpd2?: {
              code?: string;
              name?: string;
            };
          };
          okei?: {
            code?: string;
            nationalCode?: string;
            name?: string;
          };
          customerQuantities?: Array<{
            customer?: {
              regNum?: string;
              fullName?: string;
            };
            quantity?: number;
          }>;
          price?: number;
          sum?: number;
          quantity?: { value?: number };
        }>;
        totalSum?: number;
      };
    };
    preferensesInfo?: {
      items?: Array<{
        preferenseRequirementInfo?: {
          shortName?: string;
          name?: string;
        };
      }>;
    };
    requirementsInfo?: {
      items?: Array<{
        preferenseRequirementInfo?: {
          shortName?: string;
          name?: string;
        };
        content?: string;
        addRequirements?: {
          items?: Array<{
            shortName?: string;
            name?: string;
            content?: string;
          }>;
        };
      }>;
    };
  };
}
