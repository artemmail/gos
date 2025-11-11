using System;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Groups the detailed blocks that describe how the procurement will be executed.
/// </summary>
public class NotificationInfo
{
    [XmlElement(ElementName = "procedureInfo", Namespace = Ns.EPtypes)]
    public ProcedureInfo? ProcedureInfo { get; set; }

    [XmlElement(ElementName = "contractConditionsInfo", Namespace = Ns.EPtypes)]
    public ContractConditionsInfo? ContractConditionsInfo { get; set; }

    [XmlElement(ElementName = "customerRequirementsInfo", Namespace = Ns.EPtypes)]
    public CustomerRequirementsInfo? CustomerRequirementsInfo { get; set; }

    [XmlElement(ElementName = "purchaseObjectsInfo", Namespace = Ns.EPtypes)]
    public PurchaseObjectsInfo? PurchaseObjectsInfo { get; set; }

    [XmlElement(ElementName = "preferensesInfo", Namespace = Ns.EPtypes)]
    public PreferensesInfo? PreferensesInfo { get; set; }

    [XmlElement(ElementName = "requirementsInfo", Namespace = Ns.EPtypes)]
    public RequirementsInfo? RequirementsInfo { get; set; }

    [XmlElement(ElementName = "flags", Namespace = Ns.EPtypes)]
    public Flags? Flags { get; set; }

    [XmlElement(ElementName = "criteriaInfo", Namespace = Ns.EPtypes)]
    public CriteriaInfo? CriteriaInfo { get; set; }
}

/// <summary>
/// Describes procedure related dates and phases.
/// </summary>
public class ProcedureInfo
{
    [XmlElement(ElementName = "collectingInfo", Namespace = Ns.EPtypes)]
    public CollectingInfo? CollectingInfo { get; set; }

    /// <summary>
    /// Raw string representation for the bidding date. Consumers are expected to parse manually if needed.
    /// </summary>
    [XmlElement(ElementName = "biddingDate", Namespace = Ns.EPtypes, DataType = "string")]
    public string? BiddingDateRaw { get; set; }

    [XmlElement(ElementName = "summarizingDate", Namespace = Ns.EPtypes, DataType = "string")]
    public string? SummarizingDateRaw { get; set; }

    [XmlElement(ElementName = "firstPartsDate", Namespace = Ns.EPtypes, DataType = "string")]
    public string? FirstPartsDateRaw { get; set; }

    [XmlElement(ElementName = "submissionProcedureDate", Namespace = Ns.EPtypes, DataType = "string")]
    public string? SubmissionProcedureDateRaw { get; set; }

    [XmlElement(ElementName = "secondPartsDate", Namespace = Ns.EPtypes, DataType = "string")]
    public string? SecondPartsDateRaw { get; set; }
}

/// <summary>
/// Time interval for collecting applications from participants.
/// </summary>
public class CollectingInfo
{
    [XmlElement(ElementName = "startDT", Namespace = Ns.EPtypes)]
    public DateTime StartDt { get; set; }

    [XmlElement(ElementName = "endDT", Namespace = Ns.EPtypes)]
    public DateTime EndDt { get; set; }
}
