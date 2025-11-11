using System.Collections.Generic;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Information about drug related purchase objects.
/// </summary>
public class DrugPurchaseObjectsInfo
{
    [XmlElement(ElementName = "drugPurchaseObjectInfo", Namespace = Ns.Common)]
    public List<DrugPurchaseObjectInfo>? Items { get; set; }

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
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "objectInfoUsingReferenceInfo", Namespace = Ns.Common)]
    public DrugObjectInfoUsingReferenceInfo? ObjectInfoUsingReferenceInfo { get; set; }

    [XmlElement(ElementName = "objectInfoUsingTextForm", Namespace = Ns.Common)]
    public DrugObjectInfoUsingTextForm? ObjectInfoUsingTextForm { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Common)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "isZNVLP", Namespace = Ns.Common)]
    public bool IsZnvlp { get; set; }

    [XmlElement(ElementName = "isNarcotic", Namespace = Ns.Common)]
    public bool IsNarcotic { get; set; }

    [XmlElement(ElementName = "quantityUndefined", Namespace = Ns.Common)]
    public DrugPurchaseObjectQuantityUndefinedInfo? QuantityUndefinedInfo { get; set; }

    [XmlElement(ElementName = "drugQuantityCustomersInfo", Namespace = Ns.Common)]
    public DrugQuantityCustomersInfo? DrugQuantityCustomersInfo { get; set; }

    [XmlElement(ElementName = "pricePerUnit", Namespace = Ns.Common)]
    public decimal? PricePerUnit { get; set; }

    [XmlElement(ElementName = "positionPrice", Namespace = Ns.Common)]
    public decimal? PositionPrice { get; set; }

    [XmlElement(ElementName = "restrictionsInfo", Namespace = Ns.Common)]
    public RestrictionsInfo? RestrictionsInfo { get; set; }
}

public class DrugObjectInfoUsingReferenceInfo
{
    [XmlElement(ElementName = "drugsInfo", Namespace = Ns.Common)]
    public DrugDrugsInfo? DrugsInfo { get; set; }
}

public class DrugObjectInfoUsingTextForm
{
    [XmlElement(ElementName = "drugsInfo", Namespace = Ns.Common)]
    public DrugDrugsInfo? DrugsInfo { get; set; }
}

public class DrugDrugsInfo
{
    [XmlElement(ElementName = "drugInfo", Namespace = Ns.Common)]
    public List<DrugInfo>? Items { get; set; }

    [XmlElement(ElementName = "drugInterchangeInfo", Namespace = Ns.Common)]
    public List<DrugInterchangeInfo>? DrugInterchangeInfos { get; set; }
}

public class DrugInfo
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "MNNInfo", Namespace = Ns.Common)]
    public DrugMnnInfo? MnnInfo { get; set; }

    [XmlElement(ElementName = "tradeInfo", Namespace = Ns.Common)]
    public List<DrugTradeInfo>? TradeInfos { get; set; }

    [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
    public Okpd2? Okpd2 { get; set; }

    [XmlElement(ElementName = "KTRU", Namespace = Ns.Common)]
    public Ktru? Ktru { get; set; }

    [XmlElement(ElementName = "medicamentalFormInfo", Namespace = Ns.Common)]
    public MedicamentalFormInfo? MedicamentalFormInfo { get; set; }

    [XmlElement(ElementName = "dosageInfo", Namespace = Ns.Common)]
    public DosageInfo? DosageInfo { get; set; }

    [XmlElement(ElementName = "manualUserOKEI", Namespace = Ns.Common)]
    public ManualUserOkei? ManualUserOkei { get; set; }

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
    public DrugInterchangeReferenceInfo? ReferenceInfo { get; set; }

    [XmlElement(ElementName = "drugInterchangeManualInfo", Namespace = Ns.Common)]
    public DrugInterchangeManualInfo? ManualInfo { get; set; }
}

public class DrugInterchangeReferenceInfo
{
    [XmlElement(ElementName = "isInterchange", Namespace = Ns.Common)]
    public bool IsInterchange { get; set; }

    [XmlElement(ElementName = "interchangeGroupInfo", Namespace = Ns.Common)]
    public DrugInterchangeGroupInfo? InterchangeGroupInfo { get; set; }

    [XmlElement(ElementName = "drugInfo", Namespace = Ns.Common)]
    public List<DrugInterchangeDrugInfo>? DrugInfos { get; set; }
}

public class DrugInterchangeManualInfo
{
    [XmlElement(ElementName = "isInterchange", Namespace = Ns.Common)]
    public bool IsInterchange { get; set; }

    [XmlElement(ElementName = "drugInfo", Namespace = Ns.Common)]
    public List<DrugInterchangeDrugInfo>? DrugInfos { get; set; }
}

public class DrugInterchangeGroupInfo
{
    [XmlElement(ElementName = "groupCode", Namespace = Ns.Common)]
    public string? GroupCode { get; set; }

    [XmlElement(ElementName = "groupName", Namespace = Ns.Common)]
    public string? GroupName { get; set; }

    [XmlElement(ElementName = "groupOKEI", Namespace = Ns.Common)]
    public DrugInterchangeGroupOkei? GroupOkei { get; set; }
}

public class DrugInterchangeGroupOkei
{
    [XmlElement(ElementName = "name", Namespace = Ns.Common)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
    public Okei? Okei { get; set; }
}

public class DrugInterchangeDrugInfo
{
    [XmlElement(ElementName = "drugInfoUsingReferenceInfo", Namespace = Ns.Common)]
    public DrugInterchangeDrugInfoUsingReferenceInfo? DrugInfoUsingReferenceInfo { get; set; }

    [XmlElement(ElementName = "drugQuantity", Namespace = Ns.Common)]
    public decimal? DrugQuantity { get; set; }

    [XmlElement(ElementName = "quantityMultiplier", Namespace = Ns.Common)]
    public decimal? QuantityMultiplier { get; set; }

    [XmlElement(ElementName = "averagePriceValue", Namespace = Ns.Common)]
    public decimal? AveragePriceValue { get; set; }

    [XmlElement(ElementName = "externalDrugInfoLink", Namespace = Ns.Common)]
    public string? ExternalDrugInfoLink { get; set; }
}

public class DrugInterchangeDrugInfoUsingReferenceInfo
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "MNNInfo", Namespace = Ns.Common)]
    public DrugMnnInfo? MnnInfo { get; set; }

    [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
    public Okpd2? Okpd2 { get; set; }

    [XmlElement(ElementName = "KTRU", Namespace = Ns.Common)]
    public Ktru? Ktru { get; set; }

    [XmlElement(ElementName = "medicamentalFormInfo", Namespace = Ns.Common)]
    public MedicamentalFormInfo? MedicamentalFormInfo { get; set; }

    [XmlElement(ElementName = "dosageInfo", Namespace = Ns.Common)]
    public DosageInfo? DosageInfo { get; set; }

    [XmlElement(ElementName = "manualUserOKEI", Namespace = Ns.Common)]
    public ManualUserOkei? ManualUserOkei { get; set; }

    [XmlElement(ElementName = "basicUnit", Namespace = Ns.Common)]
    public bool BasicUnit { get; set; }
}

public class DrugMnnInfo
{
    [XmlElement(ElementName = "MNNExternalCode", Namespace = Ns.Common)]
    public string? MnnExternalCode { get; set; }

    [XmlElement(ElementName = "MNNName", Namespace = Ns.Common)]
    public string? MnnName { get; set; }
}

public class DrugTradeInfo
{
    [XmlElement(ElementName = "positionTradeNameExternalCode", Namespace = Ns.Common)]
    public string? PositionTradeNameExternalCode { get; set; }

    [XmlElement(ElementName = "tradeName", Namespace = Ns.Common)]
    public string? TradeName { get; set; }
}

public class MedicamentalFormInfo
{
    [XmlElement(ElementName = "medicamentalForm", Namespace = Ns.Common)]
    public MedicamentalFormReference? MedicamentalForm { get; set; }

    [XmlElement(ElementName = "medicamentalFormName", Namespace = Ns.Common)]
    public string? MedicamentalFormName { get; set; }
}

public class MedicamentalFormReference
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class DosageInfo
{
    [XmlElement(ElementName = "dosageGRLSValue", Namespace = Ns.Common)]
    public string? DosageGrlsValue { get; set; }

    [XmlElement(ElementName = "dosageUserOKEI", Namespace = Ns.Common)]
    public DosageUserOkei? DosageUserOkei { get; set; }

    [XmlElement(ElementName = "dosageUserName", Namespace = Ns.Common)]
    public string? DosageUserName { get; set; }
}

public class DosageUserOkei
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class ManualUserOkei
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class DrugQuantityCustomersInfo
{
    [XmlElement(ElementName = "drugQuantityCustomerInfo", Namespace = Ns.Common)]
    public List<DrugQuantityCustomerInfo>? Items { get; set; }

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
    public DrugPurchaseObjectCustomersInfo? DrugPurchaseObjectCustomersInfo { get; set; }
}

public class DrugPurchaseObjectCustomersInfo
{
    [XmlElement(ElementName = "drugPurchaseObjectCustomerInfo", Namespace = Ns.Common)]
    public List<DrugPurchaseObjectCustomerInfo>? Items { get; set; }
}

public class DrugPurchaseObjectCustomerInfo
{
    [XmlElement(ElementName = "customer", Namespace = Ns.Common)]
    public Customer? Customer { get; set; }

    [XmlElement(ElementName = "drugPurchaseObjectIsPurchased", Namespace = Ns.Common)]
    public bool DrugPurchaseObjectIsPurchased { get; set; }
}

public class DrugQuantityCustomerInfo
{
    [XmlElement(ElementName = "customer", Namespace = Ns.Common)]
    public Customer? Customer { get; set; }

    [XmlElement(ElementName = "quantity", Namespace = Ns.Common)]
    public decimal? Quantity { get; set; }
}
