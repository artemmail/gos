using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Information about the organisation and employees responsible for the procurement.
/// </summary>
public class PurchaseResponsibleInfo
{
    [XmlElement(ElementName = "responsibleOrgInfo", Namespace = Ns.EPtypes)]
    public ResponsibleOrgInfo? ResponsibleOrgInfo { get; set; }

    [XmlElement(ElementName = "responsibleRole", Namespace = Ns.EPtypes)]
    public string? ResponsibleRole { get; set; }

    [XmlElement(ElementName = "responsibleInfo", Namespace = Ns.EPtypes)]
    public ResponsibleInfo? ResponsibleInfo { get; set; }

    [XmlElement(ElementName = "specializedOrgInfo", Namespace = Ns.EPtypes, IsNullable = true)]
    public SpecializedOrgInfo? SpecializedOrgInfo { get; set; }
}

/// <summary>
/// Basic organisation details for the responsible customer or authority.
/// </summary>
public class ResponsibleOrgInfo
{
    [XmlElement(ElementName = "regNum", Namespace = Ns.EPtypes)]
    public string? RegNum { get; set; }

    [XmlElement(ElementName = "consRegistryNum", Namespace = Ns.EPtypes)]
    public string? ConsRegistryNum { get; set; }

    [XmlElement(ElementName = "fullName", Namespace = Ns.EPtypes)]
    public string? FullName { get; set; }

    [XmlElement(ElementName = "shortName", Namespace = Ns.EPtypes)]
    public string? ShortName { get; set; }

    [XmlElement(ElementName = "postAddress", Namespace = Ns.EPtypes)]
    public string? PostAddress { get; set; }

    [XmlElement(ElementName = "factAddress", Namespace = Ns.EPtypes)]
    public string? FactAddress { get; set; }

    [XmlElement(ElementName = "INN", Namespace = Ns.EPtypes)]
    public string? INN { get; set; }

    [XmlElement(ElementName = "KPP", Namespace = Ns.EPtypes)]
    public string? KPP { get; set; }
}

/// <summary>
/// Optional information about an external specialised organisation.
/// </summary>
public class SpecializedOrgInfo
{
    [XmlElement(ElementName = "regNum", Namespace = Ns.EPtypes)]
    public string? RegNum { get; set; }

    [XmlElement(ElementName = "consRegistryNum", Namespace = Ns.EPtypes)]
    public string? ConsRegistryNum { get; set; }

    [XmlElement(ElementName = "fullName", Namespace = Ns.EPtypes)]
    public string? FullName { get; set; }

    [XmlElement(ElementName = "shortName", Namespace = Ns.EPtypes)]
    public string? ShortName { get; set; }

    [XmlElement(ElementName = "postAddress", Namespace = Ns.EPtypes)]
    public string? PostAddress { get; set; }

    [XmlElement(ElementName = "factAddress", Namespace = Ns.EPtypes)]
    public string? FactAddress { get; set; }

    [XmlElement(ElementName = "INN", Namespace = Ns.EPtypes)]
    public string? INN { get; set; }

    [XmlElement(ElementName = "KPP", Namespace = Ns.EPtypes)]
    public string? KPP { get; set; }
}

/// <summary>
/// Contact details for the responsible person.
/// </summary>
public class ResponsibleInfo
{
    [XmlElement(ElementName = "orgPostAddress", Namespace = Ns.EPtypes)]
    public string? OrgPostAddress { get; set; }

    [XmlElement(ElementName = "orgFactAddress", Namespace = Ns.EPtypes)]
    public string? OrgFactAddress { get; set; }

    [XmlElement(ElementName = "contactPersonInfo", Namespace = Ns.EPtypes)]
    public ContactPersonInfo? ContactPersonInfo { get; set; }

    [XmlElement(ElementName = "contactEMail", Namespace = Ns.EPtypes)]
    public string? ContactEmail { get; set; }

    [XmlElement(ElementName = "contactPhone", Namespace = Ns.EPtypes)]
    public string? ContactPhone { get; set; }

    [XmlElement(ElementName = "contactFax", Namespace = Ns.EPtypes)]
    public string? ContactFax { get; set; }

    [XmlElement(ElementName = "addInfo", Namespace = Ns.EPtypes)]
    public string? AddInfo { get; set; }
}

/// <summary>
/// A natural person that can be contacted regarding the procurement.
/// </summary>
public class ContactPersonInfo
{
    [XmlElement(ElementName = "lastName", Namespace = Ns.Common)]
    public string? LastName { get; set; }

    [XmlElement(ElementName = "firstName", Namespace = Ns.Common)]
    public string? FirstName { get; set; }

    [XmlElement(ElementName = "middleName", Namespace = Ns.Common)]
    public string? MiddleName { get; set; }
}
