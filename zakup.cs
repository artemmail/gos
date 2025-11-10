using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Zakupki.EF2020
{
    // ==== Êîðíåâîé ýëåìåíò <ns3:export ...> ====
    [XmlRoot(ElementName = "export", Namespace = Ns.Export)]
    public class Export
    {
        [XmlElement(ElementName = "epNotificationEF2020", Namespace = Ns.Export)]
        public EpNotificationEf2020 EpNotification { get; set; }
    }

    // ==== Óâåäîìëåíèå ====
    public class EpNotificationEf2020
    {
        [XmlAttribute("schemeVersion")]
        public string? SchemeVersion { get; set; }

        [XmlElement(ElementName = "id", Namespace = Ns.EPtypes)]
        public string Id { get; set; }

        [XmlElement(ElementName = "externalId", Namespace = Ns.EPtypes)]
        public string ExternalId { get; set; }

        [XmlElement(ElementName = "versionNumber", Namespace = Ns.EPtypes)]
        public int VersionNumber { get; set; }

        [XmlElement(ElementName = "commonInfo", Namespace = Ns.EPtypes)]
        public CommonInfo CommonInfo { get; set; }

        [XmlElement(ElementName = "purchaseResponsibleInfo", Namespace = Ns.EPtypes)]
        public PurchaseResponsibleInfo PurchaseResponsibleInfo { get; set; }

        [XmlElement(ElementName = "printFormInfo", Namespace = Ns.EPtypes)]
        public PrintFormInfo PrintFormInfo { get; set; }

        [XmlElement(ElementName = "attachmentsInfo", Namespace = Ns.EPtypes)]
        public AttachmentsInfo AttachmentsInfo { get; set; }

        [XmlElement(ElementName = "serviceSigns", Namespace = Ns.EPtypes)]
        public ServiceSigns ServiceSigns { get; set; }

        [XmlElement(ElementName = "notificationInfo", Namespace = Ns.EPtypes)]
        public NotificationInfo NotificationInfo { get; set; }

        [XmlElement(ElementName = "modificationInfo", Namespace = Ns.EPtypes)]
        public ModificationInfo? ModificationInfo { get; set; }
    }

    // ==== Áëîê: commonInfo ====
    public class CommonInfo
    {
        [XmlElement(ElementName = "purchaseNumber", Namespace = Ns.EPtypes)]
        public string PurchaseNumber { get; set; }

        [XmlElement(ElementName = "docNumber", Namespace = Ns.EPtypes)]
        public string DocNumber { get; set; }

        [XmlElement(ElementName = "plannedPublishDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string PlannedPublishDateRaw { get; set; }

        [XmlElement(ElementName = "publishDTInEIS", Namespace = Ns.EPtypes)]
        public DateTime PublishDtInEis { get; set; }

        [XmlElement(ElementName = "href", Namespace = Ns.EPtypes)]
        public string Href { get; set; }

        [XmlElement(ElementName = "notPublishedOnEIS", Namespace = Ns.EPtypes)]
        public bool NotPublishedOnEis { get; set; }

        [XmlElement(ElementName = "placingWay", Namespace = Ns.EPtypes)]
        public PlacingWay PlacingWay { get; set; }

        [XmlElement(ElementName = "ETP", Namespace = Ns.EPtypes)]
        public Etp Etp { get; set; }

        [XmlElement(ElementName = "contractConclusionOnSt83Ch2", Namespace = Ns.EPtypes)]
        public bool ContractConclusionOnSt83Ch2 { get; set; }

        [XmlElement(ElementName = "purchaseObjectInfo", Namespace = Ns.EPtypes)]
        public string PurchaseObjectInfo { get; set; }
    }

    public class PlacingWay
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class Etp
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }

        [XmlElement(ElementName = "url", Namespace = Ns.Base)]
        public string Url { get; set; }
    }

    // ==== Áëîê: îòâåòñòâåííûé ====
    public class PurchaseResponsibleInfo
    {
        [XmlElement(ElementName = "responsibleOrgInfo", Namespace = Ns.EPtypes)]
        public ResponsibleOrgInfo ResponsibleOrgInfo { get; set; }

        [XmlElement(ElementName = "responsibleRole", Namespace = Ns.EPtypes)]
        public string ResponsibleRole { get; set; }

        [XmlElement(ElementName = "responsibleInfo", Namespace = Ns.EPtypes)]
        public ResponsibleInfo ResponsibleInfo { get; set; }

        [XmlElement(ElementName = "specializedOrgInfo", Namespace = Ns.EPtypes)]
        public SpecializedOrgInfo SpecializedOrgInfo { get; set; }
    }

    public class ResponsibleOrgInfo
    {
        [XmlElement(ElementName = "regNum", Namespace = Ns.EPtypes)]
        public string RegNum { get; set; }

        [XmlElement(ElementName = "consRegistryNum", Namespace = Ns.EPtypes)]
        public string ConsRegistryNum { get; set; }

        [XmlElement(ElementName = "fullName", Namespace = Ns.EPtypes)]
        public string FullName { get; set; }

        [XmlElement(ElementName = "shortName", Namespace = Ns.EPtypes)]
        public string ShortName { get; set; }

        [XmlElement(ElementName = "postAddress", Namespace = Ns.EPtypes)]
        public string PostAddress { get; set; }

        [XmlElement(ElementName = "factAddress", Namespace = Ns.EPtypes)]
        public string FactAddress { get; set; }

        [XmlElement(ElementName = "INN", Namespace = Ns.EPtypes)]
        public string INN { get; set; }

        [XmlElement(ElementName = "KPP", Namespace = Ns.EPtypes)]
        public string KPP { get; set; }
    }

    public class SpecializedOrgInfo
    {
        [XmlElement(ElementName = "regNum", Namespace = Ns.EPtypes)]
        public string RegNum { get; set; }

        [XmlElement(ElementName = "consRegistryNum", Namespace = Ns.EPtypes)]
        public string ConsRegistryNum { get; set; }

        [XmlElement(ElementName = "fullName", Namespace = Ns.EPtypes)]
        public string FullName { get; set; }

        [XmlElement(ElementName = "shortName", Namespace = Ns.EPtypes)]
        public string ShortName { get; set; }

        [XmlElement(ElementName = "postAddress", Namespace = Ns.EPtypes)]
        public string PostAddress { get; set; }

        [XmlElement(ElementName = "factAddress", Namespace = Ns.EPtypes)]
        public string FactAddress { get; set; }

        [XmlElement(ElementName = "INN", Namespace = Ns.EPtypes)]
        public string INN { get; set; }

        [XmlElement(ElementName = "KPP", Namespace = Ns.EPtypes)]
        public string KPP { get; set; }
    }

    public class ResponsibleInfo
    {
        [XmlElement(ElementName = "orgPostAddress", Namespace = Ns.EPtypes)]
        public string OrgPostAddress { get; set; }

        [XmlElement(ElementName = "orgFactAddress", Namespace = Ns.EPtypes)]
        public string OrgFactAddress { get; set; }

        [XmlElement(ElementName = "contactPersonInfo", Namespace = Ns.EPtypes)]
        public ContactPersonInfo ContactPersonInfo { get; set; }

        [XmlElement(ElementName = "contactEMail", Namespace = Ns.EPtypes)]
        public string ContactEmail { get; set; }

        [XmlElement(ElementName = "contactPhone", Namespace = Ns.EPtypes)]
        public string ContactPhone { get; set; }

        [XmlElement(ElementName = "contactFax", Namespace = Ns.EPtypes)]
        public string ContactFax { get; set; }

        [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
        public string? AddInfo { get; set; }
    }

    public class ContactPersonInfo
    {
        [XmlElement(ElementName = "lastName", Namespace = Ns.Common)]
        public string LastName { get; set; }

        [XmlElement(ElementName = "firstName", Namespace = Ns.Common)]
        public string FirstName { get; set; }

        [XmlElement(ElementName = "middleName", Namespace = Ns.Common)]
        public string MiddleName { get; set; }
    }

    // ==== Ïå÷àòíàÿ ôîðìà ====
    public class PrintFormInfo
    {
        [XmlElement(ElementName = "url", Namespace = Ns.Common)]
        public string Url { get; set; }
    }

    // ==== Âëîæåíèÿ ====
    public class AttachmentsInfo
    {
        [XmlElement(ElementName = "attachmentInfo", Namespace = Ns.Common)]
        public List<AttachmentInfo> Items { get; set; }
    }

    public class AttachmentInfo
    {
        [XmlElement(ElementName = "publishedContentId", Namespace = Ns.Common)]
        public string PublishedContentId { get; set; }

        [XmlElement(ElementName = "fileName", Namespace = Ns.Common)]
        public string FileName { get; set; }

        [XmlElement(ElementName = "fileSize", Namespace = Ns.Common)]
        public long FileSize { get; set; }

        [XmlElement(ElementName = "docDescription", Namespace = Ns.Common)]
        public string DocDescription { get; set; }

        [XmlElement(ElementName = "docDate", Namespace = Ns.Common)]
        public DateTime DocDate { get; set; }

        [XmlElement(ElementName = "url", Namespace = Ns.Common)]
        public string Url { get; set; }

        [XmlElement(ElementName = "docKindInfo", Namespace = Ns.Common)]
        public DocKindInfo DocKindInfo { get; set; }

        [XmlArray(ElementName = "cryptoSigns", Namespace = Ns.Common)]
        [XmlArrayItem(ElementName = "signature", Namespace = Ns.Common)]
        public List<Signature> CryptoSigns { get; set; }
    }

    public class DocKindInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class Signature
    {
        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlText]
        public string Value { get; set; }
    }

    // ==== Ñëóæåáíûå ïðèçíàêè ====
    public class ServiceSigns
    {
        [XmlElement(ElementName = "isIncludeKOKS", Namespace = Ns.EPtypes)]
        public bool IsIncludeKOKS { get; set; }

        [XmlElement(ElementName = "isControlP1Ch5St99", Namespace = Ns.EPtypes)]
        public bool IsControlP1Ch5St99 { get; set; }

        [XmlElement(ElementName = "serviceMarks", Namespace = Ns.EPtypes)]
        public ServiceMarks? ServiceMarks { get; set; }

        [XmlAnyElement]
        public XmlElement[]? AdditionalElements { get; set; }
    }

    public class ServiceMarks
    {
        [XmlElement(ElementName = "serviceMark", Namespace = Ns.EPtypes)]
        public List<ServiceMark>? Items { get; set; }

        [XmlAnyElement]
        public XmlElement[]? AdditionalElements { get; set; }
    }

    public class ServiceMark
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string? Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string? Name { get; set; }

        [XmlElement(ElementName = "value", Namespace = Ns.Base)]
        public string? Value { get; set; }

        [XmlAnyElement]
        public XmlElement[]? AdditionalElements { get; set; }
    }

    // ==== notificationInfo ====
    public class NotificationInfo
    {
        [XmlElement(ElementName = "procedureInfo", Namespace = Ns.EPtypes)]
        public ProcedureInfo ProcedureInfo { get; set; }

        [XmlElement(ElementName = "contractConditionsInfo", Namespace = Ns.EPtypes)]
        public ContractConditionsInfo ContractConditionsInfo { get; set; }

        [XmlElement(ElementName = "customerRequirementsInfo", Namespace = Ns.EPtypes)]
        public CustomerRequirementsInfo CustomerRequirementsInfo { get; set; }

        [XmlElement(ElementName = "purchaseObjectsInfo", Namespace = Ns.EPtypes)]
        public PurchaseObjectsInfo PurchaseObjectsInfo { get; set; }

        [XmlElement(ElementName = "preferensesInfo", Namespace = Ns.EPtypes)]
        public PreferensesInfo PreferensesInfo { get; set; }

        [XmlElement(ElementName = "requirementsInfo", Namespace = Ns.EPtypes)]
        public RequirementsInfo RequirementsInfo { get; set; }

        [XmlElement(ElementName = "flags", Namespace = Ns.EPtypes)]
        public Flags Flags { get; set; }
    }

    public class ProcedureInfo
    {
        [XmlElement(ElementName = "collectingInfo", Namespace = Ns.EPtypes)]
        public CollectingInfo CollectingInfo { get; set; }

        // äàòû â ôîðìàòå YYYY-MM-DD+TZ (áåç âðåìåíè)  õðàíèì êàê raw
        [XmlElement(ElementName = "biddingDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string BiddingDateRaw { get; set; }

        [XmlElement(ElementName = "summarizingDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string SummarizingDateRaw { get; set; }
    }

    public class CollectingInfo
    {
        [XmlElement(ElementName = "startDT", Namespace = Ns.EPtypes)]
        public DateTime StartDt { get; set; }

        [XmlElement(ElementName = "endDT", Namespace = Ns.EPtypes)]
        public DateTime EndDt { get; set; }
    }

    public class ContractConditionsInfo
    {
        [XmlElement(ElementName = "maxPriceInfo", Namespace = Ns.EPtypes)]
        public MaxPriceInfo MaxPriceInfo { get; set; }

        // íèæå  áîëüøîé áëîê (ÈÊÇ, ïëàí-ãðàôèê, ïëàí ïëàòåæåé è ò.ï.).
        // äëÿ ìèíèìóìà îñòàâëåíû ñàìûå ïîëåçíûå ïîëÿ; ïðè íåîáõîäèìîñòè ðàñøèðÿéòå.
        [XmlElement(ElementName = "deliveryPlacesInfo", Namespace = Ns.EPtypes)]
        public DeliveryPlacesInfo DeliveryPlacesInfo { get; set; }

        [XmlElement(ElementName = "isOneSideRejectionSt95", Namespace = Ns.EPtypes)]
        public bool IsOneSideRejectionSt95 { get; set; }
    }

    public class MaxPriceInfo
    {
        [XmlElement(ElementName = "maxPrice", Namespace = Ns.EPtypes)]
        public decimal MaxPrice { get; set; }

        [XmlElement(ElementName = "currency", Namespace = Ns.EPtypes)]
        public Currency Currency { get; set; }
    }

    public class Currency
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    // ==== Òðåáîâàíèÿ ê çàêàç÷èêó / îáåñïå÷åíèå è ò.ï. ====
    public class CustomerRequirementsInfo
    {
        [XmlElement(ElementName = "customerRequirementInfo", Namespace = Ns.EPtypes)]
        public List<CustomerRequirementInfo> Items { get; set; }
    }

    public class CustomerRequirementInfo
    {
        [XmlElement(ElementName = "customer", Namespace = Ns.EPtypes)]
        public Customer Customer { get; set; }

        [XmlElement(ElementName = "applicationGuarantee", Namespace = Ns.EPtypes)]
        public Guarantee ApplicationGuarantee { get; set; }

        [XmlElement(ElementName = "contractGuarantee", Namespace = Ns.EPtypes)]
        public Guarantee ContractGuarantee { get; set; }

        [XmlElement(ElementName = "contractConditionsInfo", Namespace = Ns.EPtypes)]
        public InnerContractConditionsInfo InnerContractConditionsInfo { get; set; }

        [XmlElement(ElementName = "warrantyInfo", Namespace = Ns.EPtypes)]
        public WarrantyInfo WarrantyInfo { get; set; }

        [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
        public string AddInfo { get; set; }
    }

    public class Customer
    {
        [XmlElement(ElementName = "regNum", Namespace = Ns.Base)]
        public string RegNum { get; set; }

        [XmlElement(ElementName = "consRegistryNum", Namespace = Ns.Base)]
        public string ConsRegistryNum { get; set; }

        [XmlElement(ElementName = "fullName", Namespace = Ns.Base)]
        public string FullName { get; set; }
    }

    public class Guarantee
    {
        [XmlElement(ElementName = "amount", Namespace = Ns.EPtypes)]
        public decimal? Amount { get; set; }

        [XmlElement(ElementName = "account", Namespace = Ns.EPtypes)]
        public BankAccount Account { get; set; }

        [XmlElement(ElementName = "accountBudget", Namespace = Ns.EPtypes)]
        public AccountBudget AccountBudget { get; set; }

        [XmlElement(ElementName = "procedureInfo", Namespace = Ns.EPtypes)]
        public string ProcedureInfo { get; set; }

        [XmlElement(ElementName = "part", Namespace = Ns.EPtypes)]
        public decimal? Part { get; set; }
    }

    public class BankAccount
    {
        [XmlElement(ElementName = "bik", Namespace = Ns.Common)]
        public string Bik { get; set; }

        [XmlElement(ElementName = "settlementAccount", Namespace = Ns.Common)]
        public string SettlementAccount { get; set; }

        [XmlElement(ElementName = "personalAccount", Namespace = Ns.Common)]
        public string PersonalAccount { get; set; }

        [XmlElement(ElementName = "creditOrgName", Namespace = Ns.Common)]
        public string CreditOrgName { get; set; }

        [XmlElement(ElementName = "corrAccountNumber", Namespace = Ns.Common)]
        public string CorrAccountNumber { get; set; }
    }

    public class AccountBudget
    {
        [XmlElement(ElementName = "accountBudgetAdmin", Namespace = Ns.EPtypes)]
        public AccountBudgetAdmin AccountBudgetAdmin { get; set; }
    }

    public class AccountBudgetAdmin
    {
        [XmlElement(ElementName = "anotherAdmin", Namespace = Ns.EPtypes)]
        public bool AnotherAdmin { get; set; }

        [XmlElement(ElementName = "INN", Namespace = Ns.EPtypes)]
        public string INN { get; set; }

        [XmlElement(ElementName = "KPP", Namespace = Ns.EPtypes)]
        public string KPP { get; set; }

        [XmlElement(ElementName = "KBK", Namespace = Ns.EPtypes)]
        public string KBK { get; set; }

        [XmlElement(ElementName = "OKTMOInfo", Namespace = Ns.EPtypes)]
        public OktmoInfo OktmoInfo { get; set; }

        [XmlElement(ElementName = "bankAccount", Namespace = Ns.EPtypes)]
        public string BankAccount { get; set; }

        [XmlElement(ElementName = "ksNumber", Namespace = Ns.EPtypes)]
        public string KsNumber { get; set; }

        [XmlElement(ElementName = "bik", Namespace = Ns.EPtypes)]
        public string Bik { get; set; }

        [XmlElement(ElementName = "counterpartyName", Namespace = Ns.EPtypes)]
        public string CounterpartyName { get; set; }
    }

    public class OktmoInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class InnerContractConditionsInfo
    {
        [XmlElement(ElementName = "maxPriceInfo", Namespace = Ns.EPtypes)]
        public MaxPriceOnly MaxPriceInfo { get; set; }

        [XmlElement(ElementName = "mustPublicDiscussion", Namespace = Ns.EPtypes)]
        public bool MustPublicDiscussion { get; set; }
    }

    public class MaxPriceOnly
    {
        [XmlElement(ElementName = "maxPrice", Namespace = Ns.EPtypes)]
        public decimal MaxPrice { get; set; }
    }

    [XmlType(Namespace = Ns.EPtypes)]
    public class WarrantyInfo
    {
        [XmlElement(ElementName = "warrantyServiceRequirement", Namespace = Ns.EPtypes)]
        public string WarrantyServiceRequirement { get; set; }

        [XmlElement(ElementName = "manufacturersWarrantyRequirement", Namespace = Ns.EPtypes)]
        public string ManufacturersWarrantyRequirement { get; set; }

        [XmlElement(ElementName = "warrantyTerm", Namespace = Ns.EPtypes)]
        public string WarrantyTerm { get; set; }
    }

    // ==== Ìåñòî ïîñòàâêè (óêîðî÷åííàÿ ìîäåëü) ====
    public class DeliveryPlacesInfo
    {
        [XmlElement(ElementName = "byGARInfo", Namespace = Ns.EPtypes)]
        public List<ByGarInfo> Items { get; set; }
    }

    public class ByGarInfo
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        [XmlElement(ElementName = "externalSId", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "countryInfo", Namespace = Ns.Common)]
        public CountryInfo CountryInfo { get; set; }

        [XmlElement(ElementName = "GARInfo", Namespace = Ns.Common)]
        public GarInfo GarInfo { get; set; }

        [XmlElement(ElementName = "deliveryPlace", Namespace = Ns.Common)]
        public string DeliveryPlace { get; set; }
    }

    public class CountryInfo
    {
        [XmlElement(ElementName = "countryCode", Namespace = Ns.Base)]
        public string CountryCode { get; set; }

        [XmlElement(ElementName = "countryFullName", Namespace = Ns.Base)]
        public string CountryFullName { get; set; }
    }

    public class GarInfo
    {
        [XmlElement(ElementName = "GARGuid", Namespace = Ns.Common)]
        public string GarGuid { get; set; }

        [XmlElement(ElementName = "GARAddress", Namespace = Ns.Common)]
        public string GarAddress { get; set; }
    }

    // ==== Îáúåêòû çàêóïêè (íåäðàã.) ====
    public class PurchaseObjectsInfo
    {
        [XmlElement(ElementName = "notDrugPurchaseObjectsInfo", Namespace = Ns.EPtypes)]
        public NotDrugPurchaseObjectsInfo NotDrugPurchaseObjectsInfo { get; set; }

        [XmlElement(ElementName = "drugPurchaseObjectsInfo", Namespace = Ns.EPtypes)]
        public DrugPurchaseObjectsInfo DrugPurchaseObjectsInfo { get; set; }
    }

    public class NotDrugPurchaseObjectsInfo
    {
        [XmlElement(ElementName = "purchaseObject", Namespace = Ns.Common)]
        public List<PurchaseObject> Items { get; set; }

        [XmlElement(ElementName = "totalSum", Namespace = Ns.Common)]
        public decimal TotalSum { get; set; }

        [XmlElement(ElementName = "quantityUndefined", Namespace = Ns.EPtypes)]
        public bool QuantityUndefined { get; set; }
    }

    public class DrugPurchaseObjectsInfo
    {
        [XmlElement(ElementName = "drugPurchaseObjectInfo", Namespace = Ns.Common)]
        public List<DrugPurchaseObjectInfo> Items { get; set; }

        [XmlElement(ElementName = "total", Namespace = Ns.Common)]
        public decimal Total { get; set; }

        [XmlElement(ElementName = "p2Ch8St24Purchase", Namespace = Ns.EPtypes)]
        public bool P2Ch8St24Purchase { get; set; }

        [XmlElement(ElementName = "quantityUndefined", Namespace = Ns.EPtypes)]
        public bool QuantityUndefined { get; set; }
    }

    public class DrugPurchaseObjectInfo
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        [XmlElement(ElementName = "externalSId", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "objectInfoUsingReferenceInfo", Namespace = Ns.Common)]
        public DrugObjectInfoUsingReferenceInfo ObjectInfoUsingReferenceInfo { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "isZNVLP", Namespace = Ns.Common)]
        public bool IsZnvlp { get; set; }

        [XmlElement(ElementName = "isNarcotic", Namespace = Ns.Common)]
        public bool IsNarcotic { get; set; }

        [XmlElement(ElementName = "drugQuantityCustomersInfo", Namespace = Ns.Common)]
        public DrugQuantityCustomersInfo DrugQuantityCustomersInfo { get; set; }

        [XmlElement(ElementName = "pricePerUnit", Namespace = Ns.Common)]
        public decimal PricePerUnit { get; set; }

        [XmlElement(ElementName = "positionPrice", Namespace = Ns.Common)]
        public decimal PositionPrice { get; set; }

        [XmlElement(ElementName = "restrictionsInfo", Namespace = Ns.Common)]
        public RestrictionsInfo RestrictionsInfo { get; set; }
    }

    public class DrugObjectInfoUsingReferenceInfo
    {
        [XmlElement(ElementName = "drugsInfo", Namespace = Ns.Common)]
        public DrugDrugsInfo DrugsInfo { get; set; }
    }

    public class DrugDrugsInfo
    {
        [XmlElement(ElementName = "drugInfo", Namespace = Ns.Common)]
        public List<DrugInfo> Items { get; set; }
    }

    public class DrugInfo
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        [XmlElement(ElementName = "externalSId", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "MNNInfo", Namespace = Ns.Common)]
        public DrugMnnInfo MnnInfo { get; set; }

        [XmlElement(ElementName = "tradeInfo", Namespace = Ns.Common)]
        public List<DrugTradeInfo> TradeInfos { get; set; }

        [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
        public Okpd2 Okpd2 { get; set; }

        [XmlElement(ElementName = "KTRU", Namespace = Ns.Common)]
        public Ktru Ktru { get; set; }

        [XmlElement(ElementName = "medicamentalFormInfo", Namespace = Ns.Common)]
        public MedicamentalFormInfo MedicamentalFormInfo { get; set; }

        [XmlElement(ElementName = "dosageInfo", Namespace = Ns.Common)]
        public DosageInfo DosageInfo { get; set; }

        [XmlElement(ElementName = "manualUserOKEI", Namespace = Ns.Common)]
        public ManualUserOkei ManualUserOkei { get; set; }

        [XmlElement(ElementName = "basicUnit", Namespace = Ns.Common)]
        public bool BasicUnit { get; set; }

        [XmlElement(ElementName = "drugQuantity", Namespace = Ns.Common)]
        public decimal DrugQuantity { get; set; }

        [XmlElement(ElementName = "limPriceValuePerUnit", Namespace = Ns.Common)]
        public decimal LimPriceValuePerUnit { get; set; }
    }

    public class DrugMnnInfo
    {
        [XmlElement(ElementName = "MNNExternalCode", Namespace = Ns.Common)]
        public string MnnExternalCode { get; set; }

        [XmlElement(ElementName = "MNNName", Namespace = Ns.Common)]
        public string MnnName { get; set; }
    }

    public class DrugTradeInfo
    {
        [XmlElement(ElementName = "positionTradeNameExternalCode", Namespace = Ns.Common)]
        public string PositionTradeNameExternalCode { get; set; }

        [XmlElement(ElementName = "tradeName", Namespace = Ns.Common)]
        public string TradeName { get; set; }
    }

    public class MedicamentalFormInfo
    {
        [XmlElement(ElementName = "medicamentalFormName", Namespace = Ns.Common)]
        public string MedicamentalFormName { get; set; }
    }

    public class DosageInfo
    {
        [XmlElement(ElementName = "dosageGRLSValue", Namespace = Ns.Common)]
        public string DosageGrlsValue { get; set; }

        [XmlElement(ElementName = "dosageUserOKEI", Namespace = Ns.Common)]
        public DosageUserOkei DosageUserOkei { get; set; }

        [XmlElement(ElementName = "dosageUserName", Namespace = Ns.Common)]
        public string DosageUserName { get; set; }
    }

    public class DosageUserOkei
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class ManualUserOkei
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class DrugQuantityCustomersInfo
    {
        [XmlElement(ElementName = "drugQuantityCustomerInfo", Namespace = Ns.Common)]
        public List<DrugQuantityCustomerInfo> Items { get; set; }

        [XmlElement(ElementName = "total", Namespace = Ns.Common)]
        public decimal Total { get; set; }
    }

    public class DrugQuantityCustomerInfo
    {
        [XmlElement(ElementName = "customer", Namespace = Ns.Common)]
        public Customer Customer { get; set; }

        [XmlElement(ElementName = "quantity", Namespace = Ns.Common)]
        public decimal Quantity { get; set; }
    }

    public class PurchaseObject
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        [XmlElement(ElementName = "externalSId", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "KTRU", Namespace = Ns.Common)]
        public Ktru Ktru { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
        public Okei Okei { get; set; }

        [XmlElement(ElementName = "price", Namespace = Ns.Common)]
        public decimal Price { get; set; }

        [XmlElement(ElementName = "volumeSpecifyingMethod", Namespace = Ns.Common)]
        public string VolumeSpecifyingMethod { get; set; }

        [XmlElement(ElementName = "quantity", Namespace = Ns.Common)]
        public Quantity Quantity { get; set; }

        [XmlElement(ElementName = "sum", Namespace = Ns.Common)]
        public decimal Sum { get; set; }

        [XmlElement(ElementName = "isMedicalProduct", Namespace = Ns.Common)]
        public bool? IsMedicalProduct { get; set; }

        [XmlElement(ElementName = "type", Namespace = Ns.Common)]
        public string Type { get; set; }

        [XmlElement(ElementName = "hierarchyType", Namespace = Ns.Common)]
        public string HierarchyType { get; set; }

        [XmlElement(ElementName = "restrictionsInfo", Namespace = Ns.Common)]
        public RestrictionsInfo RestrictionsInfo { get; set; }
    }

    public class RestrictionsInfo
    {
        [XmlElement(ElementName = "isProhibitionForeignPurchaseObjects", Namespace = Ns.Common)]
        public bool IsProhibitionForeignPurchaseObjects { get; set; }

        [XmlElement(ElementName = "isRestrictForeignPurchaseObjects", Namespace = Ns.Common)]
        public bool IsRestrictForeignPurchaseObjects { get; set; }

        [XmlElement(ElementName = "isPreferenseRFPurchaseObjects", Namespace = Ns.Common)]
        public bool IsPreferenseRfPurchaseObjects { get; set; }

        [XmlElement(ElementName = "isImposibilityProhibition", Namespace = Ns.Common)]
        public bool IsImposibilityProhibition { get; set; }

        [XmlElement(ElementName = "reasonImposibilityProhibition", Namespace = Ns.Common)]
        public string? ReasonImposibilityProhibition { get; set; }
    }

    public class Ktru
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }

        [XmlElement(ElementName = "versionId", Namespace = Ns.Base)]
        public string VersionId { get; set; }

        [XmlElement(ElementName = "versionNumber", Namespace = Ns.Base)]
        public int VersionNumber { get; set; }

        [XmlElement(ElementName = "characteristics", Namespace = Ns.Common)]
        public KtruCharacteristics Characteristics { get; set; }

        [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
        public Okpd2 Okpd2 { get; set; }
    }

    public class KtruCharacteristics
    {
        [XmlElement(ElementName = "characteristicsUsingReferenceInfo", Namespace = Ns.Common)]
        public List<KtruCharacteristicReferenceInfo> ReferenceInfos { get; set; }

        [XmlElement(ElementName = "characteristicsUsingTextForm", Namespace = Ns.Common)]
        public List<KtruCharacteristicTextForm> TextForms { get; set; }

        [XmlElement(ElementName = "addCharacteristicInfoReason", Namespace = Ns.Common)]
        public string AddCharacteristicInfoReason { get; set; }
    }

    public class KtruCharacteristicReferenceInfo
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        [XmlElement(ElementName = "externalSId", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "code", Namespace = Ns.Common)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "type", Namespace = Ns.Common)]
        public string Type { get; set; }

        [XmlElement(ElementName = "kind", Namespace = Ns.Common)]
        public string Kind { get; set; }

        [XmlElement(ElementName = "values", Namespace = Ns.Common)]
        public KtruCharacteristicReferenceValues Values { get; set; }

        [XmlElement(ElementName = "characteristicsFillingInstruction", Namespace = Ns.Common)]
        public CharacteristicsFillingInstruction CharacteristicsFillingInstruction { get; set; }
    }

    public class KtruCharacteristicReferenceValues
    {
        [XmlElement(ElementName = "value", Namespace = Ns.Common)]
        public List<KtruCharacteristicReferenceValue> Items { get; set; }

        [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
        public List<KtruCharacteristicReferenceValueSet> Sets { get; set; }
    }

    public class KtruCharacteristicReferenceValue
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        [XmlElement(ElementName = "externalSId", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
        public Okei Okei { get; set; }

        [XmlElement(ElementName = "valueFormat", Namespace = Ns.Common)]
        public string ValueFormat { get; set; }

        [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
        public KtruCharacteristicRangeSet RangeSet { get; set; }

        [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
        public string QualityDescription { get; set; }
    }

    public class KtruCharacteristicReferenceValueSet
    {
        [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
        public Okei Okei { get; set; }

        [XmlElement(ElementName = "valueFormat", Namespace = Ns.Common)]
        public string ValueFormat { get; set; }

        [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
        public KtruCharacteristicRangeSet RangeSet { get; set; }

        [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
        public string QualityDescription { get; set; }

        [XmlElement(ElementName = "value", Namespace = Ns.Common)]
        public List<KtruCharacteristicReferenceValue> Items { get; set; }
    }

    public class KtruCharacteristicRangeSet
    {
        [XmlElement(ElementName = "valueRange", Namespace = Ns.Common)]
        public List<KtruCharacteristicValueRange> Items { get; set; }
    }

    public class KtruCharacteristicValueRange
    {
        [XmlElement(ElementName = "minMathNotation", Namespace = Ns.Common)]
        public string MinMathNotation { get; set; }

        [XmlElement(ElementName = "min", Namespace = Ns.Common)]
        public string Min { get; set; }

        [XmlElement(ElementName = "maxMathNotation", Namespace = Ns.Common)]
        public string MaxMathNotation { get; set; }

        [XmlElement(ElementName = "max", Namespace = Ns.Common)]
        public string Max { get; set; }
    }

    public class CharacteristicsFillingInstruction
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class KtruCharacteristicTextForm
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        [XmlElement(ElementName = "externalSId", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "code", Namespace = Ns.Common)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "type", Namespace = Ns.Common)]
        public string Type { get; set; }

        [XmlElement(ElementName = "characteristicsFillingInstruction", Namespace = Ns.Common)]
        public CharacteristicsFillingInstruction CharacteristicsFillingInstruction { get; set; }

        [XmlElement(ElementName = "values", Namespace = Ns.Common)]
        public KtruCharacteristicTextValues Values { get; set; }
    }

    public class KtruCharacteristicTextValues
    {
        [XmlElement(ElementName = "value", Namespace = Ns.Common)]
        public List<KtruCharacteristicTextValue> Items { get; set; }
    }

    public class KtruCharacteristicTextValue
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        [XmlElement(ElementName = "externalSId", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
        public string QualityDescription { get; set; }
    }

    public class Okpd2
    {
        [XmlElement(ElementName = "OKPDCode", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "OKPDName", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class Okei
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "nationalCode", Namespace = Ns.Base)]
        public string NationalCode { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class Quantity
    {
        [XmlElement(ElementName = "value", Namespace = Ns.Common)]
        public decimal Value { get; set; }
    }

    // ==== Ïðåôåðåíöèè/òðåáîâàíèÿ/ôëàãè (óêîðî÷åíî) ====
    public class PreferensesInfo
    {
        [XmlElement(ElementName = "preferenseInfo", Namespace = Ns.EPtypes)]
        public List<PreferenseInfo> Items { get; set; }
    }

    public class PreferenseInfo
    {
        [XmlElement(ElementName = "preferenseRequirementInfo", Namespace = Ns.Common)]
        public PreferenseRequirementInfo PreferenseRequirementInfo { get; set; }
    }

    public class PreferenseRequirementInfo
    {
        [XmlElement(ElementName = "shortName", Namespace = Ns.Base)]
        public string ShortName { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class RequirementsInfo
    {
        [XmlElement(ElementName = "requirementInfo", Namespace = Ns.EPtypes)]
        public List<RequirementInfo> Items { get; set; }
    }

    public class RequirementInfo
    {
        [XmlElement(ElementName = "preferenseRequirementInfo", Namespace = Ns.Common)]
        public PreferenseRequirementInfo PreferenseRequirementInfo { get; set; }

        [XmlElement(ElementName = "reqValue", Namespace = Ns.Common)]
        public decimal? ReqValue { get; set; }

        [XmlElement(ElementName = "addRequirements", Namespace = Ns.Common)]
        public AddRequirements AddRequirements { get; set; }
    }

    public class AddRequirements
    {
        [XmlElement(ElementName = "addRequirement", Namespace = Ns.Common)]
        public List<AddRequirement> Items { get; set; }
    }

    public class AddRequirement
    {
        [XmlElement(ElementName = "shortName", Namespace = Ns.Common)]
        public string ShortName { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "content", Namespace = Ns.Common)]
        public string Content { get; set; }
    }

    public class Flags
    {
        [XmlElement(ElementName = "purchaseObjectsCh9St37", Namespace = Ns.EPtypes)]
        public bool PurchaseObjectsCh9St37 { get; set; }
    }

    public class ModificationInfo
    {
        [XmlElement(ElementName = "info", Namespace = Ns.EPtypes)]
        public string? Info { get; set; }

        [XmlElement(ElementName = "reasonInfo", Namespace = Ns.EPtypes)]
        public ModificationReasonInfo? ReasonInfo { get; set; }
    }

    public class ModificationReasonInfo
    {
        [XmlElement(ElementName = "responsibleDecisionInfo", Namespace = Ns.EPtypes)]
        public ResponsibleDecisionInfo? ResponsibleDecisionInfo { get; set; }
    }

    public class ResponsibleDecisionInfo
    {
        [XmlElement(ElementName = "decisionDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string? DecisionDateRaw { get; set; }
    }

    // ==== Ïðîñòàÿ îáåðòêà äëÿ ïðîñòðàíñòâ èìåí ====
    internal static class Ns
    {
        public const string Base   = "http://zakupki.gov.ru/oos/base/1";
        public const string Common = "http://zakupki.gov.ru/oos/common/1";
        public const string EPtypes = "http://zakupki.gov.ru/oos/EPtypes/1";
        public const string Export = "http://zakupki.gov.ru/oos/export/1";
    }

    // ==== Çàãðóç÷èê ====
    public static class ZakupkiLoader
    {
        /// <summary>
        /// Äåñåðèàëèçóåò ôàéë/ïîòîê ñ XML (epNotificationEF2020 âíóòðè export).
        /// </summary>
        public static Export LoadFromFile(string path)
        {
            using var fs = File.OpenRead(path);
            return LoadFromStream(fs);
        }

        public static Export LoadFromStream(Stream stream)
        {
            var rootType = typeof(Export);
            var serializer = new XmlSerializer(rootType);

            // Âàæíî: óêàçûâàåì èçâåñòíûå òèïû (íå îáÿçàòåëüíî, íî óñêîðÿåò èíèöèàëèçàöèþ)
            return (Export)serializer.Deserialize(stream);
        }
    }
}
