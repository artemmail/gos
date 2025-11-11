using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// High level contract constraints such as price, multi-lot flags and delivery details.
/// </summary>
public class ContractConditionsInfo
{
    [XmlElement(ElementName = "maxPriceInfo", Namespace = Ns.EPtypes)]
    public MaxPriceInfo? MaxPriceInfo { get; set; }

    [XmlElement(ElementName = "standardContractNumber", Namespace = Ns.EPtypes)]
    public string? StandardContractNumber { get; set; }

    [XmlElement(ElementName = "contractMultiInfo", Namespace = Ns.EPtypes)]
    public ContractMultiInfo? ContractMultiInfo { get; set; }

    [XmlElement(ElementName = "deliveryPlacesInfo", Namespace = Ns.EPtypes)]
    public DeliveryPlacesInfo? DeliveryPlacesInfo { get; set; }

    [XmlElement(ElementName = "isOneSideRejectionSt95", Namespace = Ns.EPtypes)]
    public bool IsOneSideRejectionSt95 { get; set; }
}

/// <summary>
/// Indicates whether the contract consists of several independent lots.
/// </summary>
public class ContractMultiInfo
{
    [XmlElement(ElementName = "notProvided", Namespace = Ns.EPtypes)]
    public bool NotProvided { get; set; }
}

/// <summary>
/// Maximum contract price including currency and additional flags.
/// </summary>
public class MaxPriceInfo
{
    [XmlElement(ElementName = "maxPrice", Namespace = Ns.EPtypes)]
    public decimal? MaxPrice { get; set; }

    [XmlElement(ElementName = "currency", Namespace = Ns.EPtypes)]
    public Currency? Currency { get; set; }

    [XmlElement(ElementName = "isContractPriceFormula", Namespace = Ns.EPtypes)]
    public bool? IsContractPriceFormula { get; set; }

    [XmlElement(ElementName = "interbudgetaryTransfer", Namespace = Ns.EPtypes)]
    public bool? InterbudgetaryTransfer { get; set; }
}

/// <summary>
/// Currency reference used across pricing related sections.
/// </summary>
public class Currency
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}
