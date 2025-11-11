using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Flags describing special service markers present in the notice.
/// </summary>
public class ServiceSigns
{
    [XmlElement(ElementName = "isIncludeKOKS", Namespace = Ns.EPtypes)]
    public bool IsIncludeKoks { get; set; }

    [XmlElement(ElementName = "isControlP1Ch5St99", Namespace = Ns.EPtypes)]
    public bool IsControlP1Ch5St99 { get; set; }

    [XmlElement(ElementName = "serviceMarks", Namespace = Ns.EPtypes)]
    public ServiceMarks? ServiceMarks { get; set; }

    [XmlAnyElement]
    public XmlElement[]? AdditionalElements { get; set; }
}

/// <summary>
/// Container for custom service marks that do not fit the standard structure.
/// </summary>
public class ServiceMarks
{
    [XmlElement(ElementName = "serviceMark", Namespace = Ns.EPtypes)]
    public List<ServiceMark>? Items { get; set; }

    [XmlAnyElement]
    public XmlElement[]? AdditionalElements { get; set; }
}

/// <summary>
/// Single service mark entry with optional additional XML payload.
/// </summary>
public class ServiceMark
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "value", Namespace = Ns.Base)]
    public string? Value { get; set; }

    [XmlAnyElement]
    public XmlElement[]? AdditionalElements { get; set; }
}
