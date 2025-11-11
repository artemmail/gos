using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Aggregates information about objects of purchase including drug and non-drug items.
/// </summary>
public class PurchaseObjectsInfo
{
    [XmlElement(ElementName = "notDrugPurchaseObjectsInfo", Namespace = Ns.EPtypes)]
    public NotDrugPurchaseObjectsInfo? NotDrugPurchaseObjectsInfo { get; set; }

    [XmlElement(ElementName = "drugPurchaseObjectsInfo", Namespace = Ns.EPtypes)]
    public DrugPurchaseObjectsInfo? DrugPurchaseObjectsInfo { get; set; }

    [XmlElement(ElementName = "notDrugPurchaseParentObjectsInfo", Namespace = Ns.EPtypes)]
    public NotDrugPurchaseParentObjectsInfo? NotDrugPurchaseParentObjectsInfo { get; set; }
}

public class NotDrugPurchaseObjectsInfo
{
    [XmlElement(ElementName = "purchaseObject", Namespace = Ns.Common)]
    public List<PurchaseObject>? Items { get; set; }

    [XmlElement(ElementName = "totalSum", Namespace = Ns.Common)]
    public decimal? TotalSum { get; set; }

    [XmlElement(ElementName = "quantityUndefined", Namespace = Ns.EPtypes)]
    public bool QuantityUndefined { get; set; }
}

public class NotDrugPurchaseParentObjectsInfo
{
    [XmlElement(ElementName = "purchaseObject")] 
    public List<NotDrugParentPurchaseObject>? Items { get; set; }

    [XmlElement(ElementName = "totalSum", Namespace = Ns.Common)]
    public decimal? TotalSum { get; set; }
}

public class NotDrugParentPurchaseObject
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
    public Okpd2? Okpd2 { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Common)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "customers", Namespace = Ns.Common)]
    public ParentCustomers? Customers { get; set; }

    [XmlElement(ElementName = "sum", Namespace = Ns.Common)]
    public decimal? Sum { get; set; }

    [XmlElement(ElementName = "type", Namespace = Ns.Common)]
    public string? Type { get; set; }

    [XmlElement(ElementName = "hierarchyType", Namespace = Ns.Common)]
    public string? HierarchyType { get; set; }
}

public class ParentCustomers
{
    [XmlElement(ElementName = "customer", Namespace = Ns.Common)]
    public List<Customer>? Items { get; set; }
}

public class PurchaseObject
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "KTRU", Namespace = Ns.Common)]
    public Ktru? Ktru { get; set; }

    [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
    public Okpd2? Okpd2 { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Common)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
    public Okei? Okei { get; set; }

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
    public string? VolumeSpecifyingMethod { get; set; }

    [XmlElement(ElementName = "serviceMarks", Namespace = Ns.Common)]
    public List<string>? ServiceMarks { get; set; }

    [XmlElement(ElementName = "trademarkInfo", Namespace = Ns.Common)]
    public PurchaseObjectTrademarkInfo? TrademarkInfo { get; set; }

    [XmlElement(ElementName = "quantity", Namespace = Ns.Common)]
    public Quantity? Quantity { get; set; }

    [XmlElement(ElementName = "sum", Namespace = Ns.Common)]
    public decimal? Sum { get; set; }

    [XmlElement(ElementName = "type", Namespace = Ns.Common)]
    public string? Type { get; set; }

    [XmlElement(ElementName = "hierarchyType", Namespace = Ns.Common)]
    public string? HierarchyType { get; set; }

    [XmlElement(ElementName = "isMedicalProduct", Namespace = Ns.Common)]
    public bool IsMedicalProduct { get; set; }

    [XmlElement(ElementName = "parentPurchaseObject", Namespace = Ns.Common)]
    public ParentPurchaseObject? ParentPurchaseObject { get; set; }

    [XmlElement(ElementName = "restrictionsInfo", Namespace = Ns.Common)]
    public RestrictionsInfo? RestrictionsInfo { get; set; }
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
    public Customer? Customer { get; set; }

    [XmlElement(ElementName = "quantity", Namespace = Ns.Common)]
    public decimal? Quantity { get; set; }
}

public class ParentPurchaseObject
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }
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

public class Quantity
{
    [XmlElement(ElementName = "value", Namespace = Ns.Common)]
    public decimal? Value { get; set; }

    [XmlElement(ElementName = "volumeTextForm", Namespace = Ns.Common)]
    public string? VolumeTextForm { get; set; }

    [XmlElement(ElementName = "undefined", Namespace = Ns.Common)]
    public bool? Undefined { get; set; }
}
