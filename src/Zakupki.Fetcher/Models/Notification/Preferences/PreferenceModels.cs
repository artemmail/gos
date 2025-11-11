using System.Collections.Generic;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Preferential treatment blocks for specific supplier categories.
/// </summary>
public class PreferensesInfo
{
    [XmlElement(ElementName = "preferenseInfo", Namespace = Ns.EPtypes)]
    public List<PreferenseInfo>? Items { get; set; }
}

public class PreferenseInfo
{
    [XmlElement(ElementName = "preferenseRequirementInfo", Namespace = Ns.Common)]
    public PreferenseRequirementInfo? PreferenseRequirementInfo { get; set; }

    [XmlElement(ElementName = "prefValue", Namespace = Ns.Common)]
    public decimal? PrefValue { get; set; }
}

public class PreferenseRequirementInfo
{
    [XmlElement(ElementName = "shortName", Namespace = Ns.Base)]
    public string? ShortName { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class RequirementsInfo
{
    [XmlElement(ElementName = "requirementInfo", Namespace = Ns.EPtypes)]
    public List<RequirementInfo>? Items { get; set; }
}

public class RequirementInfo
{
    [XmlElement(ElementName = "preferenseRequirementInfo", Namespace = Ns.Common)]
    public PreferenseRequirementInfo? PreferenseRequirementInfo { get; set; }

    [XmlElement(ElementName = "reqValue", Namespace = Ns.Common)]
    public decimal? ReqValue { get; set; }

    [XmlElement(ElementName = "addRequirements", Namespace = Ns.Common)]
    public AddRequirements? AddRequirements { get; set; }

    [XmlElement(ElementName = "content", Namespace = Ns.Common)]
    public string? Content { get; set; }
}

public class AddRequirements
{
    [XmlElement(ElementName = "addRequirement", Namespace = Ns.Common)]
    public List<AddRequirement>? Items { get; set; }
}

public class AddRequirement
{
    [XmlElement(ElementName = "shortName", Namespace = Ns.Common)]
    public string? ShortName { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Common)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "content", Namespace = Ns.Common)]
    public string? Content { get; set; }
}

/// <summary>
/// Boolean flags signalling special procurement conditions.
/// </summary>
public class Flags
{
    [XmlElement(ElementName = "purchaseObjectsCh9St37", Namespace = Ns.EPtypes)]
    public bool PurchaseObjectsCh9St37 { get; set; }

    [XmlElement(ElementName = "competitionCh19St48", Namespace = Ns.EPtypes)]
    public bool CompetitionCh19St48 { get; set; }
}
