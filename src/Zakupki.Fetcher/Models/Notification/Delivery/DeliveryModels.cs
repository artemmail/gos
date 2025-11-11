using System.Collections.Generic;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Defines delivery locations for contract execution.
/// </summary>
public class DeliveryPlacesInfo
{
    [XmlElement(ElementName = "byGARInfo", Namespace = Ns.EPtypes)]
    public List<ByGarInfo>? Items { get; set; }
}

/// <summary>
/// Delivery address linked to the GAR (State Address Register).
/// </summary>
public class ByGarInfo
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "countryInfo", Namespace = Ns.Common)]
    public CountryInfo? CountryInfo { get; set; }

    [XmlElement(ElementName = "GARInfo", Namespace = Ns.Common)]
    public GarInfo? GarInfo { get; set; }

    [XmlElement(ElementName = "deliveryPlace", Namespace = Ns.Common)]
    public string? DeliveryPlace { get; set; }
}

public class CountryInfo
{
    [XmlElement(ElementName = "countryCode", Namespace = Ns.Base)]
    public string? CountryCode { get; set; }

    [XmlElement(ElementName = "countryFullName", Namespace = Ns.Base)]
    public string? CountryFullName { get; set; }
}

public class GarInfo
{
    [XmlElement(ElementName = "GARGuid", Namespace = Ns.Common)]
    public string? GarGuid { get; set; }

    [XmlElement(ElementName = "GARAddress", Namespace = Ns.Common)]
    public string? GarAddress { get; set; }
}
