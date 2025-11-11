using System;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Contains general information about the published procurement notice.
/// </summary>
public class CommonInfo
{
    [XmlElement(ElementName = "purchaseNumber", Namespace = Ns.EPtypes)]
    public string? PurchaseNumber { get; set; }

    [XmlElement(ElementName = "docNumber", Namespace = Ns.EPtypes)]
    public string? DocNumber { get; set; }

    [XmlElement(ElementName = "plannedPublishDate", Namespace = Ns.EPtypes, DataType = "string")]
    public string? PlannedPublishDateRaw { get; set; }

    [XmlElement(ElementName = "directDT", Namespace = Ns.EPtypes)]
    public DateTime? DirectDt { get; set; }

    [XmlElement(ElementName = "publishDTInEIS", Namespace = Ns.EPtypes)]
    public DateTime PublishDtInEis { get; set; }

    [XmlElement(ElementName = "href", Namespace = Ns.EPtypes)]
    public string? Href { get; set; }

    [XmlElement(ElementName = "notPublishedOnEIS", Namespace = Ns.EPtypes)]
    public bool NotPublishedOnEis { get; set; }

    [XmlElement(ElementName = "placingWay", Namespace = Ns.EPtypes)]
    public PlacingWay? PlacingWay { get; set; }

    [XmlElement(ElementName = "ETP", Namespace = Ns.EPtypes)]
    public Etp? Etp { get; set; }

    [XmlElement(ElementName = "contractConclusionOnSt83Ch2", Namespace = Ns.EPtypes)]
    public bool ContractConclusionOnSt83Ch2 { get; set; }

    [XmlElement(ElementName = "purchaseObjectInfo", Namespace = Ns.EPtypes)]
    public string? PurchaseObjectInfo { get; set; }

    [XmlElement(ElementName = "article15FeaturesInfo", Namespace = Ns.EPtypes)]
    public string? Article15FeaturesInfo { get; set; }
}

/// <summary>
/// Reference to the procurement placing way (procedure type).
/// </summary>
public class PlacingWay
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

/// <summary>
/// Describes the electronic trading platform used for the procurement.
/// </summary>
public class Etp
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "url", Namespace = Ns.Base)]
    public string? Url { get; set; }
}
