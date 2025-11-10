using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace Zakupki.EF2020
{
    // ====   <ns3:export ...> ====
    [XmlRoot(ElementName = "export", Namespace = Ns.Export)]
    public class Export
    {
        [XmlElement(ElementName = "epNotificationEF2020", Namespace = Ns.Export, Type = typeof(EpNotificationEf2020))]
        [XmlElement(ElementName = "epNotificationEOK2020", Namespace = Ns.Export, Type = typeof(EpNotificationEok2020))]
        [XmlElement(ElementName = "epNotificationEZK2020", Namespace = Ns.Export, Type = typeof(EpNotificationEzk2020))]
        [XmlElement(ElementName = "epNotificationOK2020", Namespace = Ns.Export, Type = typeof(EpNotificationOk2020))]
        public EpNotificationEf2020? EpNotification { get; set; }

        [XmlIgnore]
        public EpNotificationEf2020? AnyNotification => EpNotification;
    }

    // ====  ====
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

        [XmlElement(ElementName = "printFormFieldsInfo", Namespace = Ns.EPtypes)]
        public PrintFormFieldsInfo PrintFormFieldsInfo { get; set; }

        [XmlElement(ElementName = "modificationInfo", Namespace = Ns.EPtypes)]
        public ModificationInfo? ModificationInfo { get; set; }
    }

    public class EpNotificationEok2020 : EpNotificationEf2020
    {
    }

    public class EpNotificationEzk2020 : EpNotificationEf2020
    {
    }

    public class EpNotificationOk2020 : EpNotificationEf2020
    {
    }

    // ==== : commonInfo ====
    public class CommonInfo
    {
        [XmlElement(ElementName = "purchaseNumber", Namespace = Ns.EPtypes)]
        public string PurchaseNumber { get; set; }

        [XmlElement(ElementName = "docNumber", Namespace = Ns.EPtypes)]
        public string DocNumber { get; set; }

        [XmlElement(ElementName = "plannedPublishDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string PlannedPublishDateRaw { get; set; }

        [XmlElement(ElementName = "directDT", Namespace = Ns.EPtypes)]
        public DateTime? DirectDt { get; set; }

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

    // ==== :  ====
    public class PurchaseResponsibleInfo
    {
        [XmlElement(ElementName = "responsibleOrgInfo", Namespace = Ns.EPtypes)]
        public ResponsibleOrgInfo ResponsibleOrgInfo { get; set; }

        [XmlElement(ElementName = "responsibleRole", Namespace = Ns.EPtypes)]
        public string ResponsibleRole { get; set; }

        [XmlElement(ElementName = "responsibleInfo", Namespace = Ns.EPtypes)]
        public ResponsibleInfo ResponsibleInfo { get; set; }

        [XmlElement(ElementName = "specializedOrgInfo", Namespace = Ns.EPtypes, IsNullable = true)]
        public SpecializedOrgInfo? SpecializedOrgInfo { get; set; }
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
        public string? ConsRegistryNum { get; set; }

        [XmlElement(ElementName = "fullName", Namespace = Ns.EPtypes)]
        public string FullName { get; set; }

        [XmlElement(ElementName = "shortName", Namespace = Ns.EPtypes)]
        public string? ShortName { get; set; }

        [XmlElement(ElementName = "postAddress", Namespace = Ns.EPtypes)]
        public string PostAddress { get; set; }

        [XmlElement(ElementName = "factAddress", Namespace = Ns.EPtypes)]
        public string? FactAddress { get; set; }

        [XmlElement(ElementName = "INN", Namespace = Ns.EPtypes)]
        public string INN { get; set; }

        [XmlElement(ElementName = "KPP", Namespace = Ns.EPtypes)]
        public string? KPP { get; set; }
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

    // ====   ====
    public class PrintFormInfo
    {
        [XmlElement(ElementName = "url", Namespace = Ns.Common)]
        public string Url { get; set; }
    }

    public class PrintFormFieldsInfo
    {
        [XmlElement(ElementName = "is449Features", Namespace = Ns.EPtypes)]
        public bool Is449Features { get; set; }
    }

    // ====  ====
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

    // ====   ====
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

        [XmlElement(ElementName = "criteriaInfo", Namespace = Ns.EPtypes)]
        public CriteriaInfo CriteriaInfo { get; set; }
    }

    public class ProcedureInfo
    {
        [XmlElement(ElementName = "collectingInfo", Namespace = Ns.EPtypes)]
        public CollectingInfo CollectingInfo { get; set; }

        //    YYYY-MM-DD+TZ ( )    raw
        [XmlElement(ElementName = "biddingDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string BiddingDateRaw { get; set; }

        [XmlElement(ElementName = "summarizingDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string SummarizingDateRaw { get; set; }

        [XmlElement(ElementName = "firstPartsDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string FirstPartsDateRaw { get; set; }

        [XmlElement(ElementName = "submissionProcedureDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string SubmissionProcedureDateRaw { get; set; }

        [XmlElement(ElementName = "secondPartsDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string SecondPartsDateRaw { get; set; }
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

        [XmlElement(ElementName = "standardContractNumber", Namespace = Ns.EPtypes)]
        public string? StandardContractNumber { get; set; }

        [XmlElement(ElementName = "contractMultiInfo", Namespace = Ns.EPtypes)]
        public ContractMultiInfo ContractMultiInfo { get; set; }

        //     (, -,    ..).
        //      ;   .
        [XmlElement(ElementName = "deliveryPlacesInfo", Namespace = Ns.EPtypes)]
        public DeliveryPlacesInfo DeliveryPlacesInfo { get; set; }

        [XmlElement(ElementName = "isOneSideRejectionSt95", Namespace = Ns.EPtypes)]
        public bool IsOneSideRejectionSt95 { get; set; }
    }

    public class ContractMultiInfo
    {
        [XmlElement(ElementName = "notProvided", Namespace = Ns.EPtypes)]
        public bool NotProvided { get; set; }
    }

    public class MaxPriceInfo
    {
        [XmlElement(ElementName = "maxPrice", Namespace = Ns.EPtypes)]
        public decimal? MaxPrice { get; set; }

        [XmlElement(ElementName = "currency", Namespace = Ns.EPtypes)]
        public Currency Currency { get; set; }

        [XmlElement(ElementName = "isContractPriceFormula", Namespace = Ns.EPtypes)]
        public bool? IsContractPriceFormula { get; set; }
    }

    public class Currency
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    // ====    /   .. ====
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

        [XmlElement(ElementName = "contractPriceFormula", Namespace = Ns.EPtypes)]
        public string? ContractPriceFormula { get; set; }

        [XmlElement(ElementName = "warrantyInfo", Namespace = Ns.EPtypes)]
        public WarrantyInfo WarrantyInfo { get; set; }

        [XmlElement(ElementName = "provisionWarranty", Namespace = Ns.EPtypes)]
        public ProvisionWarranty ProvisionWarranty { get; set; }

        [XmlElement(ElementName = "bankSupportContractRequiredInfo", Namespace = Ns.EPtypes)]
        public BankSupportContractRequiredInfo? BankSupportContractRequiredInfo { get; set; }

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
        public string Inn { get; set; }

        [XmlElement(ElementName = "KPP", Namespace = Ns.EPtypes)]
        public string Kpp { get; set; }

        [XmlElement(ElementName = "KBK", Namespace = Ns.EPtypes)]
        public string Kbk { get; set; }

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

    public class InnerContractConditionsInfo
    {
        [XmlElement(ElementName = "maxPriceInfo", Namespace = Ns.EPtypes)]
        public MaxPriceOnly MaxPriceInfo { get; set; }

        [XmlElement(ElementName = "mustPublicDiscussion", Namespace = Ns.EPtypes)]
        public bool MustPublicDiscussion { get; set; }

        [XmlElement(ElementName = "advancePaymentSum", Namespace = Ns.EPtypes)]
        public AdvancePaymentSum AdvancePaymentSum { get; set; }

        [XmlElement(ElementName = "IKZInfo", Namespace = Ns.EPtypes)]
        public IkzInfo IkzInfo { get; set; }

        [XmlElement(ElementName = "tenderPlan2020Info", Namespace = Ns.EPtypes)]
        public TenderPlan2020Info TenderPlan2020Info { get; set; }

        [XmlElement(ElementName = "contractExecutionPaymentPlan", Namespace = Ns.EPtypes)]
        public ContractExecutionPaymentPlan ContractExecutionPaymentPlan { get; set; }

        [XmlElement(ElementName = "BOInfo", Namespace = Ns.EPtypes)]
        public BoInfo BoInfo { get; set; }

        [XmlElement(ElementName = "deliveryPlacesInfo", Namespace = Ns.EPtypes)]
        public DeliveryPlacesInfo DeliveryPlacesInfo { get; set; }

        [XmlElement(ElementName = "isOneSideRejectionSt95", Namespace = Ns.EPtypes)]
        public bool IsOneSideRejectionSt95 { get; set; }
    }

    public class AdvancePaymentSum
    {
        [XmlElement(ElementName = "sumInPercents", Namespace = Ns.EPtypes)]
        public decimal? SumInPercents { get; set; }
    }

    public class IkzInfo
    {
        [XmlElement(ElementName = "purchaseCode", Namespace = Ns.EPtypes)]
        public string PurchaseCode { get; set; }

        [XmlElement(ElementName = "publishYear", Namespace = Ns.EPtypes)]
        public int PublishYear { get; set; }

        [XmlElement(ElementName = "OKPD2Info", Namespace = Ns.EPtypes)]
        public Okpd2InfoContainer Okpd2Info { get; set; }

        [XmlElement(ElementName = "KVRInfo", Namespace = Ns.EPtypes)]
        public KvrInfoContainer KvrInfo { get; set; }

        [XmlElement(ElementName = "customerCode", Namespace = Ns.EPtypes)]
        public string CustomerCode { get; set; }

        [XmlElement(ElementName = "purchaseNumber", Namespace = Ns.EPtypes)]
        public string PurchaseNumber { get; set; }

        [XmlElement(ElementName = "purchaseOrderNumber", Namespace = Ns.EPtypes)]
        public string PurchaseOrderNumber { get; set; }
    }

    public class Okpd2InfoContainer
    {
        private static readonly IReadOnlyList<Okpd2> EmptyItems = Array.Empty<Okpd2>();

        private List<Okpd2>? _rawOkpd2;
        private List<string>? _rawUndefined;

        [JsonIgnore]
        [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
        public List<Okpd2> RawOkpd2
        {
            get => _rawOkpd2 ??= new List<Okpd2>();
            set => _rawOkpd2 = value ?? new List<Okpd2>();
        }

        [JsonIgnore]
        [XmlElement(ElementName = "undefined", Namespace = Ns.Common)]
        public List<string> RawUndefined
        {
            get => _rawUndefined ??= new List<string>();
            set => _rawUndefined = value ?? new List<string>();
        }

        [XmlIgnore]
        public IReadOnlyList<Okpd2> Items
        {
            get
            {
                var source = _rawOkpd2;
                if (source is { Count: > 0 })
                {
                    return source;
                }

                return EmptyItems;
            }
        }

        [XmlIgnore]
        public string? Undefined
        {
            get
            {
                var source = _rawUndefined;
                if (source is null)
                {
                    return null;
                }

                foreach (var value in source)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }

                return null;
            }
        }
    }

    public class KvrInfoContainer
    {
        [XmlElement(ElementName = "KVR", Namespace = Ns.Common)]
        public List<Kvr> Items { get; set; }
    }

    public class Kvr
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class TenderPlan2020Info
    {
        [XmlElement(ElementName = "plan2020Number", Namespace = Ns.Common)]
        public string Plan2020Number { get; set; }

        [XmlElement(ElementName = "position2020Number", Namespace = Ns.Common)]
        public string Position2020Number { get; set; }
    }

    public class ContractExecutionPaymentPlan
    {
        [XmlElement(ElementName = "contractExecutionTermsInfo", Namespace = Ns.EPtypes)]
        public ContractExecutionTermsInfo ContractExecutionTermsInfo { get; set; }

        [XmlElement(ElementName = "financingSourcesInfo", Namespace = Ns.EPtypes)]
        public FinancingSourcesInfo FinancingSourcesInfo { get; set; }

        [XmlElement(ElementName = "stagesInfo", Namespace = Ns.EPtypes)]
        public StagesInfo StagesInfo { get; set; }
    }

    public class ContractExecutionTermsInfo
    {
        [XmlElement(ElementName = "notRelativeTermsInfo", Namespace = Ns.Common)]
        public NotRelativeTermsInfo NotRelativeTermsInfo { get; set; }

        [XmlElement(ElementName = "relativeTermsInfo", Namespace = Ns.Common)]
        public RelativeTermsInfo RelativeTermsInfo { get; set; }
    }

    public class NotRelativeTermsInfo
    {
        [XmlElement(ElementName = "isFromConclusionDate", Namespace = Ns.Common)]
        public bool IsFromConclusionDate { get; set; }

        [XmlElement(ElementName = "isNotEarlierConclusionDate", Namespace = Ns.Common)]
        public bool? IsNotEarlierConclusionDate { get; set; }

        [XmlElement(ElementName = "startDate", Namespace = Ns.Common, DataType = "string")]
        public string StartDateRaw { get; set; }

        [XmlElement(ElementName = "endDate", Namespace = Ns.Common, DataType = "string")]
        public string EndDateRaw { get; set; }
    }

    public class RelativeTermsInfo
    {
        [XmlElement(ElementName = "start", Namespace = Ns.Common)]
        public decimal? Start { get; set; }

        [XmlElement(ElementName = "startDayType", Namespace = Ns.Common)]
        public string StartDayType { get; set; }

        [XmlElement(ElementName = "term", Namespace = Ns.Common)]
        public decimal? Term { get; set; }

        [XmlElement(ElementName = "termDayType", Namespace = Ns.Common)]
        public string TermDayType { get; set; }
    }

    public class FinancingSourcesInfo
    {
        [XmlElement(ElementName = "budgetFinancingsInfo", Namespace = Ns.EPtypes)]
        public BudgetFinancingsInfo BudgetFinancingsInfo { get; set; }

        [XmlElement(ElementName = "nonbudgetFinancingsInfo", Namespace = Ns.EPtypes)]
        public NonbudgetFinancingsInfo NonbudgetFinancingsInfo { get; set; }

        [XmlElement(ElementName = "currentYear", Namespace = Ns.EPtypes)]
        public int? CurrentYear { get; set; }

        [XmlElement(ElementName = "financeInfo", Namespace = Ns.EPtypes)]
        public FinanceInfo FinanceInfo { get; set; }
    }

    public class BudgetFinancingsInfo
    {
        [XmlElement(ElementName = "budgetInfo", Namespace = Ns.EPtypes)]
        public BudgetInfo BudgetInfo { get; set; }

        [XmlElement(ElementName = "budgetLevelInfo", Namespace = Ns.EPtypes)]
        public BudgetLevelInfo BudgetLevelInfo { get; set; }

        [XmlElement(ElementName = "OKTMOInfo", Namespace = Ns.EPtypes)]
        public OktmoInfo OktmoInfo { get; set; }
    }

    public class NonbudgetFinancingsInfo
    {
        [XmlElement(ElementName = "selfFunds", Namespace = Ns.EPtypes)]
        public bool SelfFunds { get; set; }
    }

    public class BudgetInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class BudgetLevelInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class FinanceInfo
    {
        [XmlElement(ElementName = "total", Namespace = Ns.EPtypes)]
        public decimal? Total { get; set; }

        [XmlElement(ElementName = "currentYear", Namespace = Ns.EPtypes)]
        public decimal? CurrentYear { get; set; }

        [XmlElement(ElementName = "firstYear", Namespace = Ns.EPtypes)]
        public decimal? FirstYear { get; set; }

        [XmlElement(ElementName = "secondYear", Namespace = Ns.EPtypes)]
        public decimal? SecondYear { get; set; }

        [XmlElement(ElementName = "subsecYears", Namespace = Ns.EPtypes)]
        public decimal? SubsequentYears { get; set; }
    }

    public class StagesInfo
    {
        [XmlElement(ElementName = "stageInfo", Namespace = Ns.EPtypes)]
        public List<StageInfo> Items { get; set; }
    }

    public class StageInfo
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.EPtypes)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.EPtypes)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "termsInfo", Namespace = Ns.EPtypes)]
        public TermsInfo TermsInfo { get; set; }

        [XmlElement(ElementName = "financeInfo", Namespace = Ns.EPtypes)]
        public FinanceInfo FinanceInfo { get; set; }

        [XmlElement(ElementName = "budgetFinancingsInfo", Namespace = Ns.EPtypes)]
        public StageBudgetFinancingsInfo BudgetFinancingsInfo { get; set; }

        [XmlElement(ElementName = "nonbudgetFinancingInfo", Namespace = Ns.EPtypes)]
        public StageNonbudgetFinancingInfo NonbudgetFinancingInfo { get; set; }
    }

    public class TermsInfo
    {
        [XmlElement(ElementName = "notRelativeTermsInfo", Namespace = Ns.Common)]
        public NotRelativeTermsInfo NotRelativeTermsInfo { get; set; }

        [XmlElement(ElementName = "relativeTermsInfo", Namespace = Ns.Common)]
        public RelativeTermsInfo RelativeTermsInfo { get; set; }
    }

    public class StageBudgetFinancingsInfo
    {
        [XmlElement(ElementName = "budgetFinancingInfo", Namespace = Ns.EPtypes)]
        public List<BudgetFinancingInfo> Items { get; set; }
    }

    public class BudgetFinancingInfo
    {
        [XmlElement(ElementName = "KBK", Namespace = Ns.EPtypes)]
        public string Kbk { get; set; }

        [XmlElement(ElementName = "paymentYearInfo", Namespace = Ns.EPtypes)]
        public PaymentYearInfo PaymentYearInfo { get; set; }
    }

    public class StageNonbudgetFinancingInfo
    {
        [XmlElement(ElementName = "paymentYearInfo", Namespace = Ns.EPtypes)]
        public PaymentYearInfo PaymentYearInfo { get; set; }

        [XmlElement(ElementName = "KVRsInfo", Namespace = Ns.EPtypes)]
        public StageKvrInfos KvrInfos { get; set; }

        [XmlElement(ElementName = "targetArticlesInfo", Namespace = Ns.EPtypes)]
        public StageTargetArticlesInfo TargetArticlesInfo { get; set; }
    }

    public class StageKvrInfos
    {
        [XmlElement(ElementName = "currentYear", Namespace = Ns.Common)]
        public int? CurrentYear { get; set; }

        [XmlElement(ElementName = "KVRInfo", Namespace = Ns.Common)]
        public List<StageKvrInfo> Items { get; set; }

        [XmlElement(ElementName = "totalSum", Namespace = Ns.Common)]
        public decimal? TotalSum { get; set; }
    }

    public class StageKvrInfo
    {
        [XmlElement(ElementName = "KVR", Namespace = Ns.Common)]
        public Kvr Kvr { get; set; }

        [XmlElement(ElementName = "KVRYearsInfo", Namespace = Ns.Common)]
        public StageKvrYearsInfo KvrYearsInfo { get; set; }
    }

    public class StageKvrYearsInfo
    {
        [XmlElement(ElementName = "total", Namespace = Ns.Common)]
        public decimal? Total { get; set; }

        [XmlElement(ElementName = "currentYear", Namespace = Ns.Common)]
        public decimal? CurrentYear { get; set; }

        [XmlElement(ElementName = "firstYear", Namespace = Ns.Common)]
        public decimal? FirstYear { get; set; }

        [XmlElement(ElementName = "secondYear", Namespace = Ns.Common)]
        public decimal? SecondYear { get; set; }

        [XmlElement(ElementName = "subsecYears", Namespace = Ns.Common)]
        public decimal? SubsequentYears { get; set; }
    }

    public class StageTargetArticlesInfo
    {
        [XmlElement(ElementName = "currentYear", Namespace = Ns.Common)]
        public int? CurrentYear { get; set; }

        [XmlElement(ElementName = "targetArticleInfo", Namespace = Ns.Common)]
        public List<StageTargetArticleInfo> Items { get; set; }

        [XmlElement(ElementName = "totalSum", Namespace = Ns.Common)]
        public decimal? TotalSum { get; set; }
    }

    public class StageTargetArticleInfo
    {
        [XmlElement(ElementName = "targetArticle", Namespace = Ns.Common)]
        public string TargetArticle { get; set; }

        [XmlElement(ElementName = "targetArticleYearsInfo", Namespace = Ns.Common)]
        public StageTargetArticleYearsInfo TargetArticleYearsInfo { get; set; }
    }

    public class StageTargetArticleYearsInfo
    {
        [XmlElement(ElementName = "total", Namespace = Ns.Common)]
        public decimal? Total { get; set; }

        [XmlElement(ElementName = "currentYear", Namespace = Ns.Common)]
        public decimal? CurrentYear { get; set; }

        [XmlElement(ElementName = "firstYear", Namespace = Ns.Common)]
        public decimal? FirstYear { get; set; }

        [XmlElement(ElementName = "secondYear", Namespace = Ns.Common)]
        public decimal? SecondYear { get; set; }

        [XmlElement(ElementName = "subsecYears", Namespace = Ns.Common)]
        public decimal? SubsequentYears { get; set; }
    }

    public class PaymentYearInfo
    {
        [XmlElement(ElementName = "total", Namespace = Ns.EPtypes)]
        public decimal? Total { get; set; }

        [XmlElement(ElementName = "currentYear", Namespace = Ns.EPtypes)]
        public decimal? CurrentYear { get; set; }

        [XmlElement(ElementName = "firstYear", Namespace = Ns.EPtypes)]
        public decimal? FirstYear { get; set; }

        [XmlElement(ElementName = "secondYear", Namespace = Ns.EPtypes)]
        public decimal? SecondYear { get; set; }

        [XmlElement(ElementName = "subsecYears", Namespace = Ns.EPtypes)]
        public decimal? SubsequentYears { get; set; }
    }

    public class BoInfo
    {
        [XmlElement(ElementName = "BONumber", Namespace = Ns.EPtypes)]
        public string BoNumber { get; set; }

        [XmlElement(ElementName = "BODate", Namespace = Ns.EPtypes)]
        public DateTime? BoDate { get; set; }

        [XmlElement(ElementName = "inputBOFlag", Namespace = Ns.EPtypes)]
        public string InputBoFlag { get; set; }
    }

    public class MaxPriceOnly
    {
        [XmlElement(ElementName = "maxPrice", Namespace = Ns.EPtypes)]
        public decimal? MaxPrice { get; set; }

        [XmlElement(ElementName = "isContractPriceFormula", Namespace = Ns.EPtypes)]
        public bool? IsContractPriceFormula { get; set; }
    }

    [XmlType(Namespace = Ns.EPtypes)]
    public class WarrantyInfo
    {
        [XmlElement(ElementName = "warrantyServiceRequirement", Namespace = Ns.EPtypes)]
        public string? WarrantyServiceRequirement { get; set; }

        [XmlElement(ElementName = "manufacturersWarrantyRequirement", Namespace = Ns.EPtypes)]
        public string? ManufacturersWarrantyRequirement { get; set; }

        [XmlElement(ElementName = "warrantyTerm", Namespace = Ns.EPtypes)]
        public string? WarrantyTerm { get; set; }
    }

    public class ProvisionWarranty
    {
        [XmlElement(ElementName = "amount", Namespace = Ns.Common)]
        public decimal? Amount { get; set; }

        [XmlElement(ElementName = "part", Namespace = Ns.Common)]
        public decimal? Part { get; set; }

        [XmlElement(ElementName = "procedureInfo", Namespace = Ns.Common)]
        public string ProcedureInfo { get; set; }

        [XmlElement(ElementName = "account", Namespace = Ns.Common)]
        public BankAccount Account { get; set; }
    }

    public class BankSupportContractRequiredInfo
    {
        [XmlElement(ElementName = "treasurySupportContractInfo", Namespace = Ns.Common)]
        public TreasurySupportContractInfo? TreasurySupportContractInfo { get; set; }
    }

    public class TreasurySupportContractInfo
    {
        [XmlElement(ElementName = "treasurySupportContractRequired", Namespace = Ns.Common)]
        public bool TreasurySupportContractRequired { get; set; }

        [XmlElement(ElementName = "treasurySupportContractType", Namespace = Ns.Common)]
        public string? TreasurySupportContractType { get; set; }

        [XmlElement(ElementName = "treasurySupportContractConditions", Namespace = Ns.Common)]
        public TreasurySupportContractConditions? TreasurySupportContractConditions { get; set; }
    }

    public class TreasurySupportContractConditions
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Common)]
        public string? Code { get; set; }

        [XmlElement(ElementName = "nameReason", Namespace = Ns.Common)]
        public string? NameReason { get; set; }
    }

    // ====   ( ) ====
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

    // ====   (.) ====
    public class PurchaseObjectsInfo
    {
        [XmlElement(ElementName = "notDrugPurchaseObjectsInfo", Namespace = Ns.EPtypes)]
        public NotDrugPurchaseObjectsInfo NotDrugPurchaseObjectsInfo { get; set; }

        [XmlElement(ElementName = "drugPurchaseObjectsInfo", Namespace = Ns.EPtypes)]
        public DrugPurchaseObjectsInfo DrugPurchaseObjectsInfo { get; set; }

        [XmlElement(ElementName = "notDrugPurchaseParentObjectsInfo", Namespace = Ns.EPtypes)]
        public NotDrugPurchaseParentObjectsInfo NotDrugPurchaseParentObjectsInfo { get; set; }
    }

    public class NotDrugPurchaseObjectsInfo
    {
        [XmlElement(ElementName = "purchaseObject", Namespace = Ns.Common)]
        public List<PurchaseObject> Items { get; set; }

        [XmlElement(ElementName = "totalSum", Namespace = Ns.Common)]
        public decimal? TotalSum { get; set; }

        [XmlElement(ElementName = "quantityUndefined", Namespace = Ns.EPtypes)]
        public bool QuantityUndefined { get; set; }
    }

    public class NotDrugPurchaseParentObjectsInfo
    {
        [XmlElement(ElementName = "purchaseObject", Namespace = Ns.Common)]
        public List<NotDrugParentPurchaseObject> Items { get; set; }

        [XmlElement(ElementName = "totalSum", Namespace = Ns.Common)]
        public decimal? TotalSum { get; set; }
    }

    public class NotDrugParentPurchaseObject
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
        public Okpd2 Okpd2 { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "customers", Namespace = Ns.Common)]
        public ParentCustomers Customers { get; set; }

        [XmlElement(ElementName = "sum", Namespace = Ns.Common)]
        public decimal? Sum { get; set; }

        [XmlElement(ElementName = "type", Namespace = Ns.Common)]
        public string Type { get; set; }

        [XmlElement(ElementName = "hierarchyType", Namespace = Ns.Common)]
        public string HierarchyType { get; set; }
    }

    public class ParentCustomers
    {
        [XmlElement(ElementName = "customer", Namespace = Ns.Common)]
        public List<Customer> Items { get; set; }
    }

    public class PurchaseObject
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "KTRU", Namespace = Ns.Common)]
        public Ktru Ktru { get; set; }

        [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
        public Okpd2 Okpd2 { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
        public Okei Okei { get; set; }

        [XmlIgnore]
        public List<CustomerQuantity>? CustomerQuantities { get; set; }

        [XmlArray(ElementName = "customerQuantities", Namespace = Ns.Common)]
        [XmlArrayItem(ElementName = "customerQuantity", Namespace = Ns.Common)]
        [JsonIgnore]
        public List<CustomerQuantity>? CustomerQuantitiesLegacy
        {
            get => CustomerQuantities;
            set => CustomerQuantities = value;
        }

        [XmlArray(ElementName = "customerQuantitiesCH", Namespace = Ns.Common)]
        [XmlArrayItem(ElementName = "customerQuantityCH", Namespace = Ns.Common)]
        [JsonIgnore]
        public List<CustomerQuantity>? CustomerQuantitiesCh
        {
            get => CustomerQuantities;
            set => CustomerQuantities = value;
        }

        [XmlElement(ElementName = "price", Namespace = Ns.Common)]
        public decimal? Price { get; set; }

        [XmlElement(ElementName = "volumeSpecifyingMethod", Namespace = Ns.Common)]
        public string VolumeSpecifyingMethod { get; set; }

        [XmlElement(ElementName = "serviceMarks", Namespace = Ns.Common)]
        public List<string>? ServiceMarks { get; set; }

        [XmlElement(ElementName = "trademarkInfo", Namespace = Ns.Common)]
        public PurchaseObjectTrademarkInfo? TrademarkInfo { get; set; }

        [XmlElement(ElementName = "quantity", Namespace = Ns.Common)]
        public Quantity Quantity { get; set; }

        [XmlElement(ElementName = "sum", Namespace = Ns.Common)]
        public decimal? Sum { get; set; }

        [XmlElement(ElementName = "type", Namespace = Ns.Common)]
        public string Type { get; set; }

        [XmlElement(ElementName = "hierarchyType", Namespace = Ns.Common)]
        public string HierarchyType { get; set; }

        [XmlElement(ElementName = "isMedicalProduct", Namespace = Ns.Common)]
        public bool IsMedicalProduct { get; set; }

        [XmlElement(ElementName = "parentPurchaseObject", Namespace = Ns.Common)]
        public ParentPurchaseObject? ParentPurchaseObject { get; set; }

        [XmlElement(ElementName = "restrictionsInfo", Namespace = Ns.Common)]
        public RestrictionsInfo RestrictionsInfo { get; set; }
    }

    public class PurchaseObjectTrademarkInfo
    {
        [XmlElement(ElementName = "trademark", Namespace = Ns.Common)]
        public string? Trademark { get; set; }

        [XmlElement(ElementName = "isEquivalentDeliveryAllowed", Namespace = Ns.Common)]
        public bool? IsEquivalentDeliveryAllowed { get; set; }
    }

    public class CustomerQuantity
    {
        [XmlElement(ElementName = "customer", Namespace = Ns.Common)]
        public Customer Customer { get; set; }

        [XmlElement(ElementName = "quantity", Namespace = Ns.Common)]
        public decimal? Quantity { get; set; }
    }

    public class ParentPurchaseObject
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }
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

    public class DrugPurchaseObjectsInfo
    {
        [XmlElement(ElementName = "drugPurchaseObjectInfo", Namespace = Ns.Common)]
        public List<DrugPurchaseObjectInfo> Items { get; set; }

        [XmlElement(ElementName = "total", Namespace = Ns.Common)]
        public decimal? Total { get; set; }

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
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "objectInfoUsingReferenceInfo", Namespace = Ns.Common)]
        public DrugObjectInfoUsingReferenceInfo? ObjectInfoUsingReferenceInfo { get; set; }

        [XmlElement(ElementName = "objectInfoUsingTextForm", Namespace = Ns.Common)]
        public DrugObjectInfoUsingTextForm? ObjectInfoUsingTextForm { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "isZNVLP", Namespace = Ns.Common)]
        public bool IsZnvlp { get; set; }

        [XmlElement(ElementName = "isNarcotic", Namespace = Ns.Common)]
        public bool IsNarcotic { get; set; }

        [XmlElement(ElementName = "quantityUndefined", Namespace = Ns.Common)]
        public DrugPurchaseObjectQuantityUndefinedInfo QuantityUndefinedInfo { get; set; }

        [XmlElement(ElementName = "drugQuantityCustomersInfo", Namespace = Ns.Common)]
        public DrugQuantityCustomersInfo DrugQuantityCustomersInfo { get; set; }

        [XmlElement(ElementName = "pricePerUnit", Namespace = Ns.Common)]
        public decimal? PricePerUnit { get; set; }

        [XmlElement(ElementName = "positionPrice", Namespace = Ns.Common)]
        public decimal? PositionPrice { get; set; }

        [XmlElement(ElementName = "restrictionsInfo", Namespace = Ns.Common)]
        public RestrictionsInfo RestrictionsInfo { get; set; }
    }

    public class DrugObjectInfoUsingReferenceInfo
    {
        [XmlElement(ElementName = "drugsInfo", Namespace = Ns.Common)]
        public DrugDrugsInfo DrugsInfo { get; set; }
    }

    public class DrugObjectInfoUsingTextForm
    {
        [XmlElement(ElementName = "drugsInfo", Namespace = Ns.Common)]
        public DrugDrugsInfo DrugsInfo { get; set; }
    }

    public class DrugDrugsInfo
    {
        [XmlElement(ElementName = "drugInfo", Namespace = Ns.Common)]
        public List<DrugInfo> Items { get; set; }

        [XmlElement(ElementName = "drugInterchangeInfo", Namespace = Ns.Common)]
        public List<DrugInterchangeInfo> DrugInterchangeInfos { get; set; }
    }

    public class DrugInfo
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
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
        public decimal? DrugQuantity { get; set; }

        [XmlElement(ElementName = "limPriceValuePerUnit", Namespace = Ns.Common)]
        public decimal? LimPriceValuePerUnit { get; set; }
    }

    public class DrugInterchangeInfo
    {
        [XmlElement(ElementName = "drugInterchangeReferenceInfo", Namespace = Ns.Common)]
        public DrugInterchangeReferenceInfo ReferenceInfo { get; set; }

        [XmlElement(ElementName = "drugInterchangeManualInfo", Namespace = Ns.Common)]
        public DrugInterchangeManualInfo ManualInfo { get; set; }
    }

    public class DrugInterchangeReferenceInfo
    {
        [XmlElement(ElementName = "isInterchange", Namespace = Ns.Common)]
        public bool IsInterchange { get; set; }

        [XmlElement(ElementName = "interchangeGroupInfo", Namespace = Ns.Common)]
        public DrugInterchangeGroupInfo InterchangeGroupInfo { get; set; }

        [XmlElement(ElementName = "drugInfo", Namespace = Ns.Common)]
        public List<DrugInterchangeDrugInfo> DrugInfos { get; set; }
    }

    public class DrugInterchangeManualInfo
    {
        [XmlElement(ElementName = "isInterchange", Namespace = Ns.Common)]
        public bool IsInterchange { get; set; }

        [XmlElement(ElementName = "drugInfo", Namespace = Ns.Common)]
        public List<DrugInterchangeDrugInfo> DrugInfos { get; set; }
    }

    public class DrugInterchangeGroupInfo
    {
        [XmlElement(ElementName = "groupCode", Namespace = Ns.Common)]
        public string GroupCode { get; set; }

        [XmlElement(ElementName = "groupName", Namespace = Ns.Common)]
        public string GroupName { get; set; }

        [XmlElement(ElementName = "groupOKEI", Namespace = Ns.Common)]
        public DrugInterchangeGroupOkei GroupOkei { get; set; }
    }

    public class DrugInterchangeGroupOkei
    {
        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
        public Okei Okei { get; set; }
    }

    public class DrugInterchangeDrugInfo
    {
        [XmlElement(ElementName = "drugInfoUsingReferenceInfo", Namespace = Ns.Common)]
        public DrugInterchangeDrugInfoUsingReferenceInfo DrugInfoUsingReferenceInfo { get; set; }

        [XmlElement(ElementName = "drugQuantity", Namespace = Ns.Common)]
        public decimal? DrugQuantity { get; set; }

        [XmlElement(ElementName = "quantityMultiplier", Namespace = Ns.Common)]
        public decimal? QuantityMultiplier { get; set; }

        [XmlElement(ElementName = "averagePriceValue", Namespace = Ns.Common)]
        public decimal? AveragePriceValue { get; set; }
    }

    public class DrugInterchangeDrugInfoUsingReferenceInfo
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "MNNInfo", Namespace = Ns.Common)]
        public DrugMnnInfo MnnInfo { get; set; }

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
        public decimal? Total { get; set; }
    }

    public class DrugPurchaseObjectQuantityUndefinedInfo
    {
        [XmlElement(ElementName = "quantityUndefined", Namespace = Ns.Common)]
        public bool QuantityUndefined { get; set; }

        [XmlElement(ElementName = "price", Namespace = Ns.Common)]
        public decimal? Price { get; set; }

        [XmlElement(ElementName = "drugPurchaseObjectCustomersInfo", Namespace = Ns.Common)]
        public DrugPurchaseObjectCustomersInfo DrugPurchaseObjectCustomersInfo { get; set; }
    }

    public class DrugPurchaseObjectCustomersInfo
    {
        [XmlElement(ElementName = "drugPurchaseObjectCustomerInfo", Namespace = Ns.Common)]
        public List<DrugPurchaseObjectCustomerInfo> Items { get; set; }
    }

    public class DrugPurchaseObjectCustomerInfo
    {
        [XmlElement(ElementName = "customer", Namespace = Ns.Common)]
        public Customer Customer { get; set; }

        [XmlElement(ElementName = "drugPurchaseObjectIsPurchased", Namespace = Ns.Common)]
        public bool DrugPurchaseObjectIsPurchased { get; set; }
    }

    public class DrugQuantityCustomerInfo
    {
        [XmlElement(ElementName = "customer", Namespace = Ns.Common)]
        public Customer Customer { get; set; }

        [XmlElement(ElementName = "quantity", Namespace = Ns.Common)]
        public decimal? Quantity { get; set; }
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
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
        public Okei Okei { get; set; }

        [XmlElement(ElementName = "valueFormat", Namespace = Ns.Common)]
        public string ValueFormat { get; set; }

        [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
        public KtruCharacteristicRangeSet RangeSet { get; set; }

        [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
        public string QualityDescription { get; set; }

        [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
        public KtruCharacteristicReferenceValueSet? ValueSet { get; set; }
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

        [XmlElement(ElementName = "concreteValue", Namespace = Ns.Common)]
        public List<string>? ConcreteValues { get; set; }

        [XmlElement(ElementName = "value", Namespace = Ns.Common)]
        public List<KtruCharacteristicReferenceValue> Items { get; set; }
    }

    public class KtruCharacteristicRangeSet
    {
        [XmlElement(ElementName = "valueRange", Namespace = Ns.Common)]
        public List<KtruCharacteristicValueRange> Items { get; set; }

        [XmlElement(ElementName = "outValueRange", Namespace = Ns.Common)]
        public List<KtruCharacteristicOutValueRange> OutValueRanges { get; set; }
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

    public class KtruCharacteristicOutValueRange
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
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
        public string QualityDescription { get; set; }

        [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
        public Okei? Okei { get; set; }

        [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
        public KtruCharacteristicRangeSet? RangeSet { get; set; }

        [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
        public KtruCharacteristicReferenceValueSet? ValueSet { get; set; }
    }

    public class Okpd2
    {
        [XmlElement(ElementName = "OKPDCode", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "OKPDName", Namespace = Ns.Base)]
        public string Name { get; set; }

        [XmlElement(ElementName = "characteristics", Namespace = Ns.Common)]
        public Okpd2Characteristics Characteristics { get; set; }
    }

    public class Okpd2Characteristics
    {
        [XmlElement(ElementName = "characteristicsUsingTextForm", Namespace = Ns.Common)]
        public List<Okpd2CharacteristicTextForm> TextForms { get; set; }
    }

    public class Okpd2CharacteristicTextForm
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Common)]
        public string Name { get; set; }

        [XmlElement(ElementName = "type", Namespace = Ns.Common)]
        public string Type { get; set; }

        [XmlElement(ElementName = "characteristicsFillingInstruction", Namespace = Ns.Common)]
        public CharacteristicsFillingInstruction CharacteristicsFillingInstruction { get; set; }

        [XmlElement(ElementName = "values", Namespace = Ns.Common)]
        public Okpd2CharacteristicValues Values { get; set; }
    }

    public class Okpd2CharacteristicValues
    {
        [XmlElement(ElementName = "value", Namespace = Ns.Common)]
        public List<Okpd2CharacteristicValue> Items { get; set; }

        [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
        public List<Okpd2CharacteristicValueSet> Sets { get; set; }
    }

    public class Okpd2CharacteristicValue
    {
        [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
        public string ExternalSid { get; set; }

        [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
        public string QualityDescription { get; set; }

        [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
        public Okei Okei { get; set; }

        [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
        public Okpd2CharacteristicRangeSet RangeSet { get; set; }

        [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
        public Okpd2CharacteristicValueSet ValueSet { get; set; }
    }

    public class Okpd2CharacteristicValueSet
    {
        [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
        public Okei Okei { get; set; }

        [XmlElement(ElementName = "valueFormat", Namespace = Ns.Common)]
        public string ValueFormat { get; set; }

        [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
        public Okpd2CharacteristicRangeSet RangeSet { get; set; }

        [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
        public string QualityDescription { get; set; }

        [XmlElement(ElementName = "value", Namespace = Ns.Common)]
        public List<Okpd2CharacteristicValue> Items { get; set; }

        [XmlElement(ElementName = "concreteValue", Namespace = Ns.Common)]
        public List<string> ConcreteValues { get; set; }
    }

    public class Okpd2CharacteristicRangeSet
    {
        [XmlElement(ElementName = "valueRange", Namespace = Ns.Common)]
        public List<Okpd2CharacteristicValueRange> Items { get; set; }

        [XmlElement(ElementName = "outValueRange", Namespace = Ns.Common)]
        public List<Okpd2CharacteristicOutValueRange> OutValueRanges { get; set; }
    }

    public class Okpd2CharacteristicValueRange
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

    public class Okpd2CharacteristicOutValueRange
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
        public decimal? Value { get; set; }

        [XmlElement(ElementName = "volumeTextForm", Namespace = Ns.Common)]
        public string? VolumeTextForm { get; set; }

        [XmlElement(ElementName = "undefined", Namespace = Ns.Common)]
        public bool? Undefined { get; set; }
    }

    // ==== // () ====
    public class PreferensesInfo
    {
        [XmlElement(ElementName = "preferenseInfo", Namespace = Ns.EPtypes)]
        public List<PreferenseInfo> Items { get; set; }
    }

    public class PreferenseInfo
    {
        [XmlElement(ElementName = "preferenseRequirementInfo", Namespace = Ns.Common)]
        public PreferenseRequirementInfo PreferenseRequirementInfo { get; set; }

        [XmlElement(ElementName = "prefValue", Namespace = Ns.Common)]
        public decimal? PrefValue { get; set; }
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

        [XmlElement(ElementName = "content", Namespace = Ns.Common)]
        public string Content { get; set; }
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

        [XmlElement(ElementName = "competitionCh19St48", Namespace = Ns.EPtypes)]
        public bool CompetitionCh19St48 { get; set; }
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

        [XmlElement(ElementName = "authorityPrescriptionInfo", Namespace = Ns.EPtypes)]
        public AuthorityPrescriptionInfo? AuthorityPrescriptionInfo { get; set; }
    }

    public class ResponsibleDecisionInfo
    {
        [XmlElement(ElementName = "decisionDate", Namespace = Ns.EPtypes, DataType = "string")]
        public string? DecisionDateRaw { get; set; }
    }

    public class AuthorityPrescriptionInfo
    {
        [XmlElement(ElementName = "externalPrescription", Namespace = Ns.EPtypes)]
        public ExternalPrescription? ExternalPrescription { get; set; }
    }

    public class ExternalPrescription
    {
        [XmlElement(ElementName = "authorityName", Namespace = Ns.EPtypes)]
        public string AuthorityName { get; set; }

        [XmlElement(ElementName = "authorityType", Namespace = Ns.EPtypes)]
        public string AuthorityType { get; set; }

        [XmlElement(ElementName = "prescriptionProperty", Namespace = Ns.EPtypes)]
        public PrescriptionProperty PrescriptionProperty { get; set; }
    }

    public class PrescriptionProperty
    {
        [XmlElement(ElementName = "docName", Namespace = Ns.Common)]
        public string DocName { get; set; }

        [XmlElement(ElementName = "docNumber", Namespace = Ns.Common)]
        public string DocNumber { get; set; }

        [XmlElement(ElementName = "docDate", Namespace = Ns.Common, DataType = "string")]
        public string DocDateRaw { get; set; }
    }

    public class CriteriaInfo
    {
        [XmlElement(ElementName = "isCertainWorks", Namespace = Ns.EPtypes)]
        public bool IsCertainWorks { get; set; }

        [XmlElement(ElementName = "criterionInfo", Namespace = Ns.EPtypes)]
        public List<CriterionInfo> Items { get; set; }
    }

    public class CriterionInfo
    {
        [XmlElement(ElementName = "costCriterionInfo", Namespace = Ns.EPtypes)]
        public CostCriterionInfo CostCriterionInfo { get; set; }

        [XmlElement(ElementName = "qualitativeCriterionInfo", Namespace = Ns.EPtypes)]
        public QualitativeCriterionInfo QualitativeCriterionInfo { get; set; }
    }

    public class CostCriterionInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.EPtypes)]
        public string Code { get; set; }

        [XmlElement(ElementName = "valueInfo", Namespace = Ns.EPtypes)]
        public CriterionValueInfo ValueInfo { get; set; }

        [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
        public string AddInfo { get; set; }
    }

    public class CriterionValueInfo
    {
        [XmlElement(ElementName = "value", Namespace = Ns.EPtypes)]
        public decimal? Value { get; set; }

        [XmlElement(ElementName = "valueLess25MaxPrice", Namespace = Ns.EPtypes)]
        public decimal? ValueLess25MaxPrice { get; set; }

        [XmlElement(ElementName = "valueMore25MaxPrice", Namespace = Ns.EPtypes)]
        public decimal? ValueMore25MaxPrice { get; set; }
    }

    public class QualitativeCriterionInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.EPtypes)]
        public string Code { get; set; }

        [XmlElement(ElementName = "valueInfo", Namespace = Ns.EPtypes)]
        public CriterionValueInfo ValueInfo { get; set; }

        [XmlElement(ElementName = "indicatorsInfo", Namespace = Ns.EPtypes)]
        public IndicatorsInfo IndicatorsInfo { get; set; }

        [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
        public string AddInfo { get; set; }
    }

    public class IndicatorsInfo
    {
        [XmlElement(ElementName = "indicatorInfo", Namespace = Ns.EPtypes)]
        public List<IndicatorInfo> Items { get; set; }
    }

    public class IndicatorInfo
    {
        [XmlElement(ElementName = "sId", Namespace = Ns.EPtypes)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "purchaseObjectCharsInfo", Namespace = Ns.EPtypes)]
        public PurchaseObjectCharsInfo PurchaseObjectCharsInfo { get; set; }

        [XmlElement(ElementName = "qualPurchaseParticipantsInfo", Namespace = Ns.EPtypes)]
        public QualPurchaseParticipantsInfo QualPurchaseParticipantsInfo { get; set; }

        [XmlElement(ElementName = "value", Namespace = Ns.EPtypes)]
        public decimal? Value { get; set; }

        [XmlElement(ElementName = "detailIndicatorsInfo", Namespace = Ns.EPtypes)]
        public DetailIndicatorsInfo DetailIndicatorsInfo { get; set; }

        [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
        public string AddInfo { get; set; }
    }

    public class QualPurchaseParticipantsInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class PurchaseObjectCharsInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class DetailIndicatorsInfo
    {
        [XmlElement(ElementName = "detailIndicatorInfo", Namespace = Ns.EPtypes)]
        public List<DetailIndicatorInfo> Items { get; set; }
    }

    public class DetailIndicatorInfo
    {
        [XmlElement(ElementName = "sId", Namespace = Ns.EPtypes)]
        public string Sid { get; set; }

        [XmlElement(ElementName = "indicatorInfo", Namespace = Ns.EPtypes)]
        public IndicatorNameInfo IndicatorInfo { get; set; }

        [XmlElement(ElementName = "value", Namespace = Ns.EPtypes)]
        public decimal? Value { get; set; }

        [XmlElement(ElementName = "orderEvalIndicatorsInfo", Namespace = Ns.EPtypes)]
        public OrderEvalIndicatorsInfo OrderEvalIndicatorsInfo { get; set; }

        [XmlElement(ElementName = "availAbsEvaluation", Namespace = Ns.EPtypes)]
        public string AvailAbsEvaluation { get; set; }

        [XmlElement(ElementName = "limitMin", Namespace = Ns.EPtypes)]
        public decimal? LimitMin { get; set; }

        [XmlElement(ElementName = "limitMax", Namespace = Ns.EPtypes)]
        public decimal? LimitMax { get; set; }
    }

    public class IndicatorNameInfo
    {
        [XmlElement(ElementName = "manualEnteredName", Namespace = Ns.EPtypes)]
        public string ManualEnteredName { get; set; }

        [XmlElement(ElementName = "indicatorDictInfo", Namespace = Ns.EPtypes)]
        public IndicatorDictInfo IndicatorDictInfo { get; set; }
    }

    public class IndicatorDictInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    public class OrderEvalIndicatorsInfo
    {
        [XmlElement(ElementName = "code", Namespace = Ns.Base)]
        public string Code { get; set; }

        [XmlElement(ElementName = "name", Namespace = Ns.Base)]
        public string Name { get; set; }
    }

    // ====      ====
    internal static class Ns
    {
        public const string Base   = "http://zakupki.gov.ru/oos/base/1";
        public const string Common = "http://zakupki.gov.ru/oos/common/1";
        public const string EPtypes = "http://zakupki.gov.ru/oos/EPtypes/1";
        public const string Export = "http://zakupki.gov.ru/oos/export/1";
    }

    // ====  ====
    public static class ZakupkiLoader
    {
        /// <summary>
        ///  /  XML (epNotificationEF2020  export).
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

            // :    ( ,   )
            return (Export)serializer.Deserialize(stream);
        }
    }
}
