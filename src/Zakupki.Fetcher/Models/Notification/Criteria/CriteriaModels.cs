using System.Collections.Generic;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Defines the evaluation criteria used when scoring bids.
/// </summary>
public class CriteriaInfo
{
    [XmlElement(ElementName = "isCertainWorks", Namespace = Ns.EPtypes)]
    public bool IsCertainWorks { get; set; }

    [XmlElement(ElementName = "criterionInfo", Namespace = Ns.EPtypes)]
    public List<CriterionInfo>? Items { get; set; }
}

public class CriterionInfo
{
    [XmlElement(ElementName = "costCriterionInfo", Namespace = Ns.EPtypes)]
    public CostCriterionInfo? CostCriterionInfo { get; set; }

    [XmlElement(ElementName = "qualitativeCriterionInfo", Namespace = Ns.EPtypes)]
    public QualitativeCriterionInfo? QualitativeCriterionInfo { get; set; }
}

public class CostCriterionInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.EPtypes)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "valueInfo", Namespace = Ns.EPtypes)]
    public CriterionValueInfo? ValueInfo { get; set; }

    [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
    public string? AddInfo { get; set; }
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
    public string? Code { get; set; }

    [XmlElement(ElementName = "valueInfo", Namespace = Ns.EPtypes)]
    public CriterionValueInfo? ValueInfo { get; set; }

    [XmlElement(ElementName = "indicatorsInfo", Namespace = Ns.EPtypes)]
    public IndicatorsInfo? IndicatorsInfo { get; set; }

    [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
    public string? AddInfo { get; set; }
}

public class IndicatorsInfo
{
    [XmlElement(ElementName = "indicatorInfo", Namespace = Ns.EPtypes)]
    public List<IndicatorInfo>? Items { get; set; }
}

public class IndicatorInfo
{
    [XmlElement(ElementName = "sId", Namespace = Ns.EPtypes)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "purchaseObjectCharsInfo", Namespace = Ns.EPtypes)]
    public PurchaseObjectCharsInfo? PurchaseObjectCharsInfo { get; set; }

    [XmlElement(ElementName = "qualPurchaseParticipantsInfo", Namespace = Ns.EPtypes)]
    public QualPurchaseParticipantsInfo? QualPurchaseParticipantsInfo { get; set; }

    [XmlElement(ElementName = "value", Namespace = Ns.EPtypes)]
    public decimal? Value { get; set; }

    [XmlElement(ElementName = "detailIndicatorsInfo", Namespace = Ns.EPtypes)]
    public DetailIndicatorsInfo? DetailIndicatorsInfo { get; set; }

    [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
    public string? AddInfo { get; set; }
}

public class QualPurchaseParticipantsInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class PurchaseObjectCharsInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class DetailIndicatorsInfo
{
    [XmlElement(ElementName = "detailIndicatorInfo", Namespace = Ns.EPtypes)]
    public List<DetailIndicatorInfo>? Items { get; set; }
}

public class DetailIndicatorInfo
{
    [XmlElement(ElementName = "sId", Namespace = Ns.EPtypes)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "indicatorInfo", Namespace = Ns.EPtypes)]
    public IndicatorNameInfo? IndicatorInfo { get; set; }

    [XmlElement(ElementName = "value", Namespace = Ns.EPtypes)]
    public decimal? Value { get; set; }

    [XmlElement(ElementName = "orderEvalIndicatorsInfo", Namespace = Ns.EPtypes)]
    public OrderEvalIndicatorsInfo? OrderEvalIndicatorsInfo { get; set; }

    [XmlElement(ElementName = "availAbsEvaluation", Namespace = Ns.EPtypes)]
    public string? AvailAbsEvaluation { get; set; }

    [XmlElement(ElementName = "limitMin", Namespace = Ns.EPtypes)]
    public decimal? LimitMin { get; set; }

    [XmlElement(ElementName = "limitMax", Namespace = Ns.EPtypes)]
    public decimal? LimitMax { get; set; }
}

public class IndicatorNameInfo
{
    [XmlElement(ElementName = "manualEnteredName", Namespace = Ns.EPtypes)]
    public string? ManualEnteredName { get; set; }

    [XmlElement(ElementName = "indicatorDictInfo", Namespace = Ns.EPtypes)]
    public IndicatorDictInfo? IndicatorDictInfo { get; set; }
}

public class IndicatorDictInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class OrderEvalIndicatorsInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}
