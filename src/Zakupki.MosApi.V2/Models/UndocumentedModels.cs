using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Zakupki.MosApi.V2
{
    public class UndocumentedAuctionDto
    {
        [JsonPropertyName("name")]
        public string? name { get; set; }

        [JsonPropertyName("createdByCustomer")]
        public UndocumentedCompanyDto? createdByCustomer { get; set; }

        [JsonPropertyName("state")]
        public UndocumentedAuctionStateDto? state { get; set; }

        [JsonPropertyName("startDate")]
        public string? startDate { get; set; }

        [JsonPropertyName("initialDuration")]
        public int? initialDuration { get; set; }

        [JsonPropertyName("endDate")]
        public string? endDate { get; set; }

        [JsonPropertyName("startCost")]
        public double? startCost { get; set; }

        [JsonPropertyName("nextCost")]
        public double? nextCost { get; set; }

        [JsonPropertyName("lastBetSupplier")]
        public UndocumentedCompanyDto? lastBetSupplier { get; set; }

        [JsonPropertyName("lastBetCost")]
        public double? lastBetCost { get; set; }

        [JsonPropertyName("lastBetId")]
        public long? lastBetId { get; set; }

        [JsonPropertyName("step")]
        public double? step { get; set; }

        [JsonPropertyName("federalLawName")]
        public string? federalLawName { get; set; }

        [JsonPropertyName("conclusionReasonName")]
        public string? conclusionReasonName { get; set; }

        [JsonPropertyName("auctionRegion")]
        public List<UndocumentedAuctionRegionDto>? auctionRegion { get; set; }

        [JsonPropertyName("files")]
        public List<UndocumentedAuctionFileDto>? files { get; set; }

        [JsonPropertyName("customer")]
        public UndocumentedCompanyDto? customer { get; set; }

        [JsonPropertyName("licenseFiles")]
        public List<UndocumentedAuctionFileDto>? licenseFiles { get; set; }

        [JsonPropertyName("auctionItem")]
        public List<UndocumentedAuctionItemDto>? auctionItem { get; set; }

        [JsonPropertyName("bets")]
        public List<UndocumentedAuctionBetDto>? bets { get; set; }

        [JsonPropertyName("offerSignTime")]
        public string? offerSignTime { get; set; }

        [JsonPropertyName("uniqueSupplierCount")]
        public int? uniqueSupplierCount { get; set; }

        [JsonPropertyName("repeatId")]
        public long? repeatId { get; set; }

        [JsonPropertyName("unpublishName")]
        public string? unpublishName { get; set; }

        [JsonPropertyName("unpublishDate")]
        public string? unpublishDate { get; set; }

        [JsonPropertyName("items")]
        public List<UndocumentedAuctionItemDto>? items { get; set; }

        [JsonPropertyName("deliveries")]
        public List<UndocumentedAuctionDeliveryDto>? deliveries { get; set; }

        [JsonPropertyName("offersSigned")]
        public bool? offersSigned { get; set; }

        [JsonPropertyName("showPurchaseRequestMessageIfFailed")]
        public bool? showPurchaseRequestMessageIfFailed { get; set; }

        [JsonPropertyName("purchaseTypeId")]
        public int? purchaseTypeId { get; set; }

        [JsonPropertyName("contractCost")]
        public double? contractCost { get; set; }

        [JsonPropertyName("contracts")]
        public List<UndocumentedAuctionContractDto>? contracts { get; set; }

        [JsonPropertyName("unpublishComment")]
        public string? unpublishComment { get; set; }

        [JsonPropertyName("externalId")]
        public string? externalId { get; set; }

        [JsonPropertyName("isElectronicContractExecutionRequired")]
        public bool? isElectronicContractExecutionRequired { get; set; }

        [JsonPropertyName("isContractGuaranteeRequired")]
        public bool? isContractGuaranteeRequired { get; set; }

        [JsonPropertyName("contractGuaranteeAmount")]
        public double? contractGuaranteeAmount { get; set; }

        [JsonPropertyName("rowVersion")]
        public string? rowVersion { get; set; }

        [JsonPropertyName("organizingTypeId")]
        public int? organizingTypeId { get; set; }

        [JsonPropertyName("sharedPurchaseBuyers")]
        public List<UndocumentedCompanyDto>? sharedPurchaseBuyers { get; set; }

        [JsonPropertyName("suppliersAutobetSettings")]
        public List<UndocumentedAutobetSettingsDto>? suppliersAutobetSettings { get; set; }

        [JsonPropertyName("isLicenseProduction")]
        public bool? isLicenseProduction { get; set; }

        [JsonPropertyName("uploadLicenseDocumentsComment")]
        public string? uploadLicenseDocumentsComment { get; set; }

        [JsonPropertyName("isExternalIntegration")]
        public bool? isExternalIntegration { get; set; }

        [JsonPropertyName("shortDescription")]
        public UndocumentedShortDescriptionDto? shortDescription { get; set; }

        [JsonPropertyName("id")]
        public int? id { get; set; }
    }

    public class UndocumentedAuctionResult
    {
        public UndocumentedAuctionResult(UndocumentedAuctionDto? auction, string rawJson)
        {
            Auction = auction;
            RawJson = rawJson;
        }

        public UndocumentedAuctionDto? Auction { get; }

        public string RawJson { get; }
    }

    public class UndocumentedAuctionFileDto
    {
        [JsonPropertyName("companyId")]
        public long? companyId { get; set; }

        [JsonPropertyName("id")]
        public long? id { get; set; }

        [JsonPropertyName("name")]
        public string? name { get; set; }
    }

    public class UndocumentedAuctionStateDto
    {
        [JsonPropertyName("name")]
        public string? name { get; set; }

        [JsonPropertyName("id")]
        public int? id { get; set; }
    }

    public class UndocumentedAuctionRegionDto
    {
        [JsonPropertyName("treePathId")]
        public string? treePathId { get; set; }

        [JsonPropertyName("socr")]
        public string? socr { get; set; }

        [JsonPropertyName("id")]
        public int? id { get; set; }

        [JsonPropertyName("oktmo")]
        public string? oktmo { get; set; }

        [JsonPropertyName("code")]
        public string? code { get; set; }

        [JsonPropertyName("name")]
        public string? name { get; set; }
    }

    public class UndocumentedCompanyDto
    {
        [JsonPropertyName("inn")]
        public string? inn { get; set; }

        [JsonPropertyName("name")]
        public string? name { get; set; }

        [JsonPropertyName("id")]
        public int? id { get; set; }
    }

    public class UndocumentedAuctionItemDto
    {
        [JsonPropertyName("currentValue")]
        public double? currentValue { get; set; }

        [JsonPropertyName("costPerUnit")]
        public double? costPerUnit { get; set; }

        [JsonPropertyName("okeiName")]
        public string? okeiName { get; set; }

        [JsonPropertyName("createdOfferId")]
        public long? createdOfferId { get; set; }

        [JsonPropertyName("skuId")]
        public long? skuId { get; set; }

        [JsonPropertyName("imageId")]
        public long? imageId { get; set; }

        [JsonPropertyName("defaultImageId")]
        public long? defaultImageId { get; set; }

        [JsonPropertyName("okpdName")]
        public string? okpdName { get; set; }

        [JsonPropertyName("productionDirectoryName")]
        public string? productionDirectoryName { get; set; }

        [JsonPropertyName("oksm")]
        public string? oksm { get; set; }

        [JsonPropertyName("name")]
        public string? name { get; set; }

        [JsonPropertyName("id")]
        public long? id { get; set; }
    }

    public class UndocumentedAuctionDeliveryDto
    {
        [JsonPropertyName("periodDaysFrom")]
        public int? periodDaysFrom { get; set; }

        [JsonPropertyName("periodDaysTo")]
        public int? periodDaysTo { get; set; }

        [JsonPropertyName("periodDateFrom")]
        public string? periodDateFrom { get; set; }

        [JsonPropertyName("periodDateTo")]
        public string? periodDateTo { get; set; }

        [JsonPropertyName("deliveryPlace")]
        public string? deliveryPlace { get; set; }

        [JsonPropertyName("quantity")]
        public double? quantity { get; set; }

        [JsonPropertyName("items")]
        public List<UndocumentedAuctionDeliveryItemDto>? items { get; set; }

        [JsonPropertyName("id")]
        public long? id { get; set; }
    }

    public class UndocumentedAuctionDeliveryItemDto
    {
        [JsonPropertyName("sum")]
        public double? sum { get; set; }

        [JsonPropertyName("costPerUnit")]
        public double? costPerUnit { get; set; }

        [JsonPropertyName("quantity")]
        public double? quantity { get; set; }

        [JsonPropertyName("name")]
        public string? name { get; set; }

        [JsonPropertyName("buyerId")]
        public long? buyerId { get; set; }

        [JsonPropertyName("isBuyerInvitationSent")]
        public bool? isBuyerInvitationSent { get; set; }

        [JsonPropertyName("isApprovedByBuyer")]
        public bool? isApprovedByBuyer { get; set; }
    }

    public class UndocumentedAuctionBetDto
    {
        [JsonPropertyName("id")]
        public long? id { get; set; }

        [JsonPropertyName("amount")]
        public double? amount { get; set; }

        [JsonPropertyName("createdAt")]
        public string? createdAt { get; set; }

        [JsonPropertyName("supplier")]
        public UndocumentedCompanyDto? supplier { get; set; }
    }

    public class UndocumentedAuctionContractDto
    {
        [JsonPropertyName("id")]
        public long? id { get; set; }

        [JsonPropertyName("cost")]
        public double? cost { get; set; }
    }

    public class UndocumentedAutobetSettingsDto
    {
        [JsonPropertyName("supplier")]
        public UndocumentedCompanyDto? supplier { get; set; }

        [JsonPropertyName("maxAmount")]
        public double? maxAmount { get; set; }
    }

    public class UndocumentedShortDescriptionDto
    {
        [JsonPropertyName("fileName")]
        public string? fileName { get; set; }

        [JsonPropertyName("data")]
        public List<UndocumentedShortDescriptionEntryDto>? data { get; set; }
    }

    public class UndocumentedShortDescriptionEntryDto
    {
        [JsonPropertyName("title")]
        public string? title { get; set; }

        [JsonPropertyName("summary")]
        public string? summary { get; set; }
    }
}
