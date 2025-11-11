using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Aggregates requirement blocks for each customer participating in the contract.
/// </summary>
public class CustomerRequirementsInfo
{
    [XmlElement(ElementName = "customerRequirementInfo", Namespace = Ns.EPtypes)]
    public List<CustomerRequirementInfo>? Items { get; set; }
}

/// <summary>
/// Detailed requirements and guarantees for a specific customer.
/// </summary>
public class CustomerRequirementInfo
{
    [XmlElement(ElementName = "customer", Namespace = Ns.EPtypes)]
    public Customer? Customer { get; set; }

    [XmlElement(ElementName = "applicationGuarantee", Namespace = Ns.EPtypes)]
    public Guarantee? ApplicationGuarantee { get; set; }

    [XmlElement(ElementName = "contractGuarantee", Namespace = Ns.EPtypes)]
    public Guarantee? ContractGuarantee { get; set; }

    [XmlElement(ElementName = "contractConditionsInfo", Namespace = Ns.EPtypes)]
    public InnerContractConditionsInfo? InnerContractConditionsInfo { get; set; }

    [XmlElement(ElementName = "contractPriceFormula", Namespace = Ns.EPtypes)]
    public string? ContractPriceFormula { get; set; }

    [XmlElement(ElementName = "warrantyInfo", Namespace = Ns.EPtypes)]
    public WarrantyInfo? WarrantyInfo { get; set; }

    [XmlElement(ElementName = "provisionWarranty", Namespace = Ns.EPtypes)]
    public ProvisionWarranty? ProvisionWarranty { get; set; }

    [XmlElement(ElementName = "bankSupportContractRequiredInfo", Namespace = Ns.EPtypes)]
    public BankSupportContractRequiredInfo? BankSupportContractRequiredInfo { get; set; }

    [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
    public string? AddInfo { get; set; }
}

/// <summary>
/// Basic customer information reused across multiple sections.
/// </summary>
public class Customer
{
    [XmlElement(ElementName = "regNum", Namespace = Ns.Base)]
    public string? RegNum { get; set; }

    [XmlElement(ElementName = "consRegistryNum", Namespace = Ns.Base)]
    public string? ConsRegistryNum { get; set; }

    [XmlElement(ElementName = "fullName", Namespace = Ns.Base)]
    public string? FullName { get; set; }
}

/// <summary>
/// Guarantee details for applications or contract execution.
/// </summary>
public class Guarantee
{
    [XmlElement(ElementName = "amount", Namespace = Ns.EPtypes)]
    public decimal? Amount { get; set; }

    [XmlElement(ElementName = "account", Namespace = Ns.EPtypes)]
    public BankAccount? Account { get; set; }

    [XmlElement(ElementName = "accountBudget", Namespace = Ns.EPtypes)]
    public AccountBudget? AccountBudget { get; set; }

    [XmlElement(ElementName = "procedureInfo", Namespace = Ns.EPtypes)]
    public string? ProcedureInfo { get; set; }

    [XmlElement(ElementName = "part", Namespace = Ns.EPtypes)]
    public decimal? Part { get; set; }
}

/// <summary>
/// Treasury account used for guarantees.
/// </summary>
public class AccountBudget
{
    [XmlElement(ElementName = "accountBudgetAdmin", Namespace = Ns.EPtypes)]
    public AccountBudgetAdmin? AccountBudgetAdmin { get; set; }
}

public class AccountBudgetAdmin
{
    [XmlElement(ElementName = "anotherAdmin", Namespace = Ns.EPtypes)]
    public bool AnotherAdmin { get; set; }

    [XmlElement(ElementName = "INN", Namespace = Ns.EPtypes)]
    public string? Inn { get; set; }

    [XmlElement(ElementName = "KPP", Namespace = Ns.EPtypes)]
    public string? Kpp { get; set; }

    [XmlElement(ElementName = "KBK", Namespace = Ns.EPtypes)]
    public string? Kbk { get; set; }

    [XmlElement(ElementName = "OKTMOInfo", Namespace = Ns.EPtypes)]
    public OktmoInfo? OktmoInfo { get; set; }

    [XmlElement(ElementName = "bankAccount", Namespace = Ns.EPtypes)]
    public string? BankAccount { get; set; }

    [XmlElement(ElementName = "ksNumber", Namespace = Ns.EPtypes)]
    public string? KsNumber { get; set; }

    [XmlElement(ElementName = "bik", Namespace = Ns.EPtypes)]
    public string? Bik { get; set; }

    [XmlElement(ElementName = "counterpartyName", Namespace = Ns.EPtypes)]
    public string? CounterpartyName { get; set; }
}

public class OktmoInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class BankAccount
{
    [XmlElement(ElementName = "bik", Namespace = Ns.Common)]
    public string? Bik { get; set; }

    [XmlElement(ElementName = "settlementAccount", Namespace = Ns.Common)]
    public string? SettlementAccount { get; set; }

    [XmlElement(ElementName = "personalAccount", Namespace = Ns.Common)]
    public string? PersonalAccount { get; set; }

    [XmlElement(ElementName = "creditOrgName", Namespace = Ns.Common)]
    public string? CreditOrgName { get; set; }

    [XmlElement(ElementName = "corrAccountNumber", Namespace = Ns.Common)]
    public string? CorrAccountNumber { get; set; }
}

/// <summary>
/// Additional contract parameters defined per customer.
/// </summary>
public class InnerContractConditionsInfo
{
    [XmlElement(ElementName = "maxPriceInfo", Namespace = Ns.EPtypes)]
    public MaxPriceOnly? MaxPriceInfo { get; set; }

    [XmlElement(ElementName = "mustPublicDiscussion", Namespace = Ns.EPtypes)]
    public bool MustPublicDiscussion { get; set; }

    [XmlElement(ElementName = "publicDiscussionInfo", Namespace = Ns.EPtypes)]
    public PublicDiscussionInfo? PublicDiscussionInfo { get; set; }

    [XmlElement(ElementName = "advancePaymentSum", Namespace = Ns.EPtypes)]
    public AdvancePaymentSum? AdvancePaymentSum { get; set; }

    [XmlElement(ElementName = "IKZInfo", Namespace = Ns.EPtypes)]
    public IkzInfo? IkzInfo { get; set; }

    [XmlElement(ElementName = "tenderPlan2020Info", Namespace = Ns.EPtypes)]
    public TenderPlan2020Info? TenderPlan2020Info { get; set; }

    [XmlElement(ElementName = "contractExecutionPaymentPlan", Namespace = Ns.EPtypes)]
    public ContractExecutionPaymentPlan? ContractExecutionPaymentPlan { get; set; }

    [XmlElement(ElementName = "BOInfo", Namespace = Ns.EPtypes)]
    public BoInfo? BoInfo { get; set; }

    [XmlElement(ElementName = "deliveryPlacesInfo", Namespace = Ns.EPtypes)]
    public DeliveryPlacesInfo? DeliveryPlacesInfo { get; set; }

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
    public string? PurchaseCode { get; set; }

    [XmlElement(ElementName = "publishYear", Namespace = Ns.EPtypes)]
    public int PublishYear { get; set; }

    [XmlElement(ElementName = "OKPD2Info", Namespace = Ns.EPtypes)]
    public Okpd2InfoContainer? Okpd2Info { get; set; }

    [XmlElement(ElementName = "KVRInfo", Namespace = Ns.EPtypes)]
    public KvrInfoContainer? KvrInfo { get; set; }

    [XmlElement(ElementName = "customerCode", Namespace = Ns.EPtypes)]
    public string? CustomerCode { get; set; }

    [XmlElement(ElementName = "purchaseNumber", Namespace = Ns.EPtypes)]
    public string? PurchaseNumber { get; set; }

    [XmlElement(ElementName = "purchaseOrderNumber", Namespace = Ns.EPtypes)]
    public string? PurchaseOrderNumber { get; set; }
}

/// <summary>
/// Wraps classifier information for OKPD2 codes. Provides convenient accessors to prefer structured data.
/// </summary>
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
    public List<Kvr>? Items { get; set; }
}

public class Kvr
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class TenderPlan2020Info
{
    [XmlElement(ElementName = "plan2020Number", Namespace = Ns.Common)]
    public string? Plan2020Number { get; set; }

    [XmlElement(ElementName = "position2020Number", Namespace = Ns.Common)]
    public string? Position2020Number { get; set; }
}

public class ContractExecutionPaymentPlan
{
    [XmlElement(ElementName = "contractExecutionTermsInfo", Namespace = Ns.EPtypes)]
    public ContractExecutionTermsInfo? ContractExecutionTermsInfo { get; set; }

    [XmlElement(ElementName = "financingSourcesInfo", Namespace = Ns.EPtypes)]
    public FinancingSourcesInfo? FinancingSourcesInfo { get; set; }

    [XmlElement(ElementName = "stagesInfo", Namespace = Ns.EPtypes)]
    public StagesInfo? StagesInfo { get; set; }
}

public class ContractExecutionTermsInfo
{
    [XmlElement(ElementName = "notRelativeTermsInfo", Namespace = Ns.Common)]
    public NotRelativeTermsInfo? NotRelativeTermsInfo { get; set; }

    [XmlElement(ElementName = "relativeTermsInfo", Namespace = Ns.Common)]
    public RelativeTermsInfo? RelativeTermsInfo { get; set; }
}

public class NotRelativeTermsInfo
{
    [XmlElement(ElementName = "isFromConclusionDate", Namespace = Ns.Common)]
    public bool IsFromConclusionDate { get; set; }

    [XmlElement(ElementName = "isNotEarlierConclusionDate", Namespace = Ns.Common)]
    public bool? IsNotEarlierConclusionDate { get; set; }

    [XmlElement(ElementName = "startDate", Namespace = Ns.Common, DataType = "string")]
    public string? StartDateRaw { get; set; }

    [XmlElement(ElementName = "endDate", Namespace = Ns.Common, DataType = "string")]
    public string? EndDateRaw { get; set; }
}

public class RelativeTermsInfo
{
    [XmlElement(ElementName = "start", Namespace = Ns.Common)]
    public decimal? Start { get; set; }

    [XmlElement(ElementName = "startDayType", Namespace = Ns.Common)]
    public string? StartDayType { get; set; }

    [XmlElement(ElementName = "term", Namespace = Ns.Common)]
    public decimal? Term { get; set; }

    [XmlElement(ElementName = "termDayType", Namespace = Ns.Common)]
    public string? TermDayType { get; set; }
}

public class FinancingSourcesInfo
{
    [XmlElement(ElementName = "budgetFinancingsInfo", Namespace = Ns.EPtypes)]
    public BudgetFinancingsInfo? BudgetFinancingsInfo { get; set; }

    [XmlElement(ElementName = "nonbudgetFinancingsInfo", Namespace = Ns.EPtypes)]
    public NonbudgetFinancingsInfo? NonbudgetFinancingsInfo { get; set; }

    [XmlElement(ElementName = "currentYear", Namespace = Ns.EPtypes)]
    public int? CurrentYear { get; set; }

    [XmlElement(ElementName = "financeInfo", Namespace = Ns.EPtypes)]
    public FinanceInfo? FinanceInfo { get; set; }
}

public class BudgetFinancingsInfo
{
    [XmlElement(ElementName = "budgetInfo", Namespace = Ns.EPtypes)]
    public BudgetInfo? BudgetInfo { get; set; }

    [XmlElement(ElementName = "budgetLevelInfo", Namespace = Ns.EPtypes)]
    public BudgetLevelInfo? BudgetLevelInfo { get; set; }

    [XmlElement(ElementName = "OKTMOInfo", Namespace = Ns.EPtypes)]
    public OktmoInfo? OktmoInfo { get; set; }
}

public class NonbudgetFinancingsInfo
{
    [XmlElement(ElementName = "selfFunds", Namespace = Ns.EPtypes)]
    public bool SelfFunds { get; set; }
}

public class BudgetInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class BudgetLevelInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
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
    public List<StageInfo>? Items { get; set; }
}

public class StageInfo
{
    [XmlElement(ElementName = "sid", Namespace = Ns.EPtypes)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.EPtypes)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "termsInfo", Namespace = Ns.EPtypes)]
    public TermsInfo? TermsInfo { get; set; }

    [XmlElement(ElementName = "financeInfo", Namespace = Ns.EPtypes)]
    public FinanceInfo? FinanceInfo { get; set; }

    [XmlElement(ElementName = "budgetFinancingsInfo", Namespace = Ns.EPtypes)]
    public StageBudgetFinancingsInfo? BudgetFinancingsInfo { get; set; }

    [XmlElement(ElementName = "nonbudgetFinancingInfo", Namespace = Ns.EPtypes)]
    public StageNonbudgetFinancingInfo? NonbudgetFinancingInfo { get; set; }
}

public class TermsInfo
{
    [XmlElement(ElementName = "notRelativeTermsInfo", Namespace = Ns.Common)]
    public NotRelativeTermsInfo? NotRelativeTermsInfo { get; set; }

    [XmlElement(ElementName = "relativeTermsInfo", Namespace = Ns.Common)]
    public RelativeTermsInfo? RelativeTermsInfo { get; set; }
}

public class StageBudgetFinancingsInfo
{
    [XmlElement(ElementName = "budgetFinancingInfo", Namespace = Ns.EPtypes)]
    public List<BudgetFinancingInfo>? Items { get; set; }
}

public class BudgetFinancingInfo
{
    [XmlElement(ElementName = "KBK", Namespace = Ns.EPtypes)]
    public string? Kbk { get; set; }

    [XmlElement(ElementName = "paymentYearInfo", Namespace = Ns.EPtypes)]
    public PaymentYearInfo? PaymentYearInfo { get; set; }
}

public class StageNonbudgetFinancingInfo
{
    [XmlElement(ElementName = "paymentYearInfo", Namespace = Ns.EPtypes)]
    public PaymentYearInfo? PaymentYearInfo { get; set; }

    [XmlElement(ElementName = "KVRsInfo", Namespace = Ns.EPtypes)]
    public StageKvrInfos? KvrInfos { get; set; }

    [XmlElement(ElementName = "targetArticlesInfo", Namespace = Ns.EPtypes)]
    public StageTargetArticlesInfo? TargetArticlesInfo { get; set; }
}

public class StageKvrInfos
{
    [XmlElement(ElementName = "currentYear", Namespace = Ns.Common)]
    public int? CurrentYear { get; set; }

    [XmlElement(ElementName = "KVRInfo", Namespace = Ns.Common)]
    public List<StageKvrInfo>? Items { get; set; }

    [XmlElement(ElementName = "totalSum", Namespace = Ns.Common)]
    public decimal? TotalSum { get; set; }
}

public class StageKvrInfo
{
    [XmlElement(ElementName = "KVR", Namespace = Ns.Common)]
    public Kvr? Kvr { get; set; }

    [XmlElement(ElementName = "KVRYearsInfo", Namespace = Ns.Common)]
    public StageKvrYearsInfo? KvrYearsInfo { get; set; }
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
    public List<StageTargetArticleInfo>? Items { get; set; }

    [XmlElement(ElementName = "totalSum", Namespace = Ns.Common)]
    public decimal? TotalSum { get; set; }
}

public class StageTargetArticleInfo
{
    [XmlElement(ElementName = "targetArticle", Namespace = Ns.Common)]
    public string? TargetArticle { get; set; }

    [XmlElement(ElementName = "targetArticleYearsInfo", Namespace = Ns.Common)]
    public StageTargetArticleYearsInfo? TargetArticleYearsInfo { get; set; }
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
    public string? BoNumber { get; set; }

    [XmlElement(ElementName = "BODate", Namespace = Ns.EPtypes)]
    public DateTime? BoDate { get; set; }

    [XmlElement(ElementName = "inputBOFlag", Namespace = Ns.EPtypes)]
    public string? InputBoFlag { get; set; }
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
    public string? ProcedureInfo { get; set; }

    [XmlElement(ElementName = "account", Namespace = Ns.Common)]
    public BankAccount? Account { get; set; }
}

public class BankSupportContractRequiredInfo
{
    [XmlElement(ElementName = "bankSupportContractRequired", Namespace = Ns.Common)]
    public bool? BankSupportContractRequired { get; set; }

    [XmlElement(ElementName = "treasurySupportContractInfo", Namespace = Ns.Common)]
    public TreasurySupportContractInfo? TreasurySupportContractInfo { get; set; }

    [XmlElement(ElementName = "removeTreasurySupportReasonsInfo", Namespace = Ns.Common)]
    public RemoveTreasurySupportReasonsInfo? RemoveTreasurySupportReasonsInfo { get; set; }
}

public class PublicDiscussionInfo
{
    [XmlElement(ElementName = "publicDiscussionInEISInfo", Namespace = Ns.Common)]
    public PublicDiscussionInEisInfo? PublicDiscussionInEisInfo { get; set; }
}

public class PublicDiscussionInEisInfo
{
    [XmlElement(ElementName = "publicDiscussionInEIS", Namespace = Ns.Common)]
    public bool PublicDiscussionInEis { get; set; }

    [XmlElement(ElementName = "publicDiscussionNum", Namespace = Ns.Common)]
    public string? PublicDiscussionNum { get; set; }
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

public class RemoveTreasurySupportReasonsInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.Common)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "nameReason", Namespace = Ns.Common)]
    public string? NameReason { get; set; }

    [XmlElement(ElementName = "descriptionReason", Namespace = Ns.Common)]
    public string? DescriptionReason { get; set; }
}
