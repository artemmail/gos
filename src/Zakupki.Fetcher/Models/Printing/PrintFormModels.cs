using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Links to the printable form of the notification.
/// </summary>
public class PrintFormInfo
{
    [XmlElement(ElementName = "url", Namespace = Ns.Common)]
    public string? Url { get; set; }
}

/// <summary>
/// Additional flags for automatically generated print forms.
/// </summary>
public class PrintFormFieldsInfo
{
    [XmlElement(ElementName = "is449Features", Namespace = Ns.EPtypes)]
    public bool Is449Features { get; set; }
}
