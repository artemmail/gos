using System;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Contains information about changes made to the notification after its initial publication.
/// </summary>
public class ModificationInfo
{
    [XmlElement(ElementName = "info", Namespace = Ns.EPtypes)]
    public string? Info { get; set; }

    [XmlElement(ElementName = "reasonInfo", Namespace = Ns.EPtypes)]
    public ModificationReasonInfo? ReasonInfo { get; set; }
}

public class ModificationReasonInfo
{
    [XmlElement(ElementName = "responsibleDecisionInfo", Namespace = Ns.EPtypes)]
    public ResponsibleDecisionInfo? ResponsibleDecisionInfo { get; set; }

    [XmlElement(ElementName = "authorityPrescriptionInfo", Namespace = Ns.EPtypes)]
    public AuthorityPrescriptionInfo? AuthorityPrescriptionInfo { get; set; }
}

public class ResponsibleDecisionInfo
{
    [XmlElement(ElementName = "decisionDate", Namespace = Ns.EPtypes, DataType = "string")]
    public string? DecisionDateRaw { get; set; }
}

public class AuthorityPrescriptionInfo
{
    [XmlElement(ElementName = "reestrPrescription", Namespace = Ns.EPtypes)]
    public ReestrPrescription? ReestrPrescription { get; set; }

    [XmlElement(ElementName = "externalPrescription", Namespace = Ns.EPtypes)]
    public ExternalPrescription? ExternalPrescription { get; set; }
}

public class ReestrPrescription
{
    [XmlElement(ElementName = "regNumber", Namespace = Ns.EPtypes)]
    public string? RegNumber { get; set; }

    [XmlElement(ElementName = "prescriptionNumber", Namespace = Ns.EPtypes)]
    public string? PrescriptionNumber { get; set; }

    [XmlElement(ElementName = "authorityName", Namespace = Ns.EPtypes)]
    public string? AuthorityName { get; set; }

    [XmlElement(ElementName = "docDate", Namespace = Ns.EPtypes)]
    public DateTime? DocDate { get; set; }
}

public class ExternalPrescription
{
    [XmlElement(ElementName = "authorityName", Namespace = Ns.EPtypes)]
    public string? AuthorityName { get; set; }

    [XmlElement(ElementName = "authorityType", Namespace = Ns.EPtypes)]
    public string? AuthorityType { get; set; }

    [XmlElement(ElementName = "prescriptionProperty", Namespace = Ns.EPtypes)]
    public ExternalPrescriptionProperty? PrescriptionProperty { get; set; }
}

public class ExternalPrescriptionProperty
{
    [XmlElement(ElementName = "docName", Namespace = Ns.Common)]
    public string? DocName { get; set; }

    [XmlElement(ElementName = "docNumber", Namespace = Ns.Common)]
    public string? DocNumber { get; set; }

    [XmlElement(ElementName = "docDate", Namespace = Ns.Common)]
    public string? DocDateRaw { get; set; }
}
