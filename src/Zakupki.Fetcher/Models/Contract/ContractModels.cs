using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Represents the <contract> payload that can be embedded inside an <export> document.
/// </summary>
[XmlRoot(ElementName = "contract", Namespace = Ns.Export)]
public class ContractExport
{
    [XmlAttribute("schemeVersion")]
    public string? SchemeVersion { get; set; }

    [XmlElement(ElementName = "id", Namespace = Ns.Types)]
    public string? Id { get; set; }

    [XmlElement(ElementName = "publishDate", Namespace = Ns.Types)]
    public DateTime? PublishDate { get; set; }

    [XmlElement(ElementName = "signDate", Namespace = Ns.Types)]
    public DateTime? SignDate { get; set; }

    [XmlElement(ElementName = "versionNumber", Namespace = Ns.Types)]
    public int VersionNumber { get; set; }

    [XmlElement(ElementName = "regNum", Namespace = Ns.Types)]
    public string? RegNum { get; set; }

    [XmlElement(ElementName = "number", Namespace = Ns.Types)]
    public string? Number { get; set; }

    [XmlElement(ElementName = "contractSubject", Namespace = Ns.Types)]
    public string? ContractSubject { get; set; }

    [XmlElement(ElementName = "href", Namespace = Ns.Types)]
    public string? Href { get; set; }

    [XmlElement(ElementName = "foundation", Namespace = Ns.Types)]
    public ContractFoundation? Foundation { get; set; }

    [XmlElement(ElementName = "priceInfo", Namespace = Ns.Types)]
    public ContractPriceInfo? PriceInfo { get; set; }

    [XmlElement(ElementName = "products", Namespace = Ns.Types)]
    public ContractProducts? Products { get; set; }
}

public class ContractFoundation
{
    [XmlElement(ElementName = "fcsOrder", Namespace = Ns.Types)]
    public ContractFcsOrder? FcsOrder { get; set; }
}

public class ContractFcsOrder
{
    [XmlElement(ElementName = "order", Namespace = Ns.Types)]
    public ContractOrder? Order { get; set; }
}

public class ContractOrder
{
    [XmlElement(ElementName = "notificationNumber", Namespace = Ns.Types)]
    public string? NotificationNumber { get; set; }

    [XmlElement(ElementName = "lotNumber", Namespace = Ns.Types)]
    public string? LotNumber { get; set; }
}

public class ContractPriceInfo
{
    [XmlElement(ElementName = "price", Namespace = Ns.Types)]
    public decimal? Price { get; set; }

    [XmlElement(ElementName = "currency", Namespace = Ns.Types)]
    public ContractCurrency? Currency { get; set; }
}

public class ContractCurrency
{
    [XmlElement(ElementName = "code", Namespace = Ns.Types)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Types)]
    public string? Name { get; set; }
}

public class ContractProducts
{
    [XmlElement(ElementName = "product", Namespace = Ns.Types)]
    public List<ContractProduct>? Items { get; set; }
}

public class ContractProduct
{
    [XmlElement(ElementName = "name", Namespace = Ns.Types)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "OKPD2", Namespace = Ns.Types)]
    public ContractOkpd2? Okpd2 { get; set; }
}

public class ContractOkpd2
{
    [XmlElement(ElementName = "code", Namespace = Ns.Types)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Types)]
    public string? Name { get; set; }
}
