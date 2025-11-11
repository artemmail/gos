using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Collection of documents attached to the notification.
/// </summary>
public class AttachmentsInfo
{
    [XmlElement(ElementName = "attachmentInfo", Namespace = Ns.Common)]
    public List<AttachmentInfo>? Items { get; set; }
}

/// <summary>
/// Represents a single document attached to the notice.
/// </summary>
public class AttachmentInfo
{
    [XmlElement(ElementName = "publishedContentId", Namespace = Ns.Common)]
    public string? PublishedContentId { get; set; }

    [XmlElement(ElementName = "fileName", Namespace = Ns.Common)]
    public string? FileName { get; set; }

    [XmlElement(ElementName = "fileSize", Namespace = Ns.Common)]
    public long FileSize { get; set; }

    [XmlElement(ElementName = "docDescription", Namespace = Ns.Common)]
    public string? DocDescription { get; set; }

    [XmlElement(ElementName = "docDate", Namespace = Ns.Common)]
    public DateTime DocDate { get; set; }

    [XmlElement(ElementName = "url", Namespace = Ns.Common)]
    public string? Url { get; set; }

    [XmlElement(ElementName = "docKindInfo", Namespace = Ns.Common)]
    public DocKindInfo? DocKindInfo { get; set; }

    [XmlArray(ElementName = "cryptoSigns", Namespace = Ns.Common)]
    [XmlArrayItem(ElementName = "signature", Namespace = Ns.Common)]
    public List<Signature>? CryptoSigns { get; set; }
}

/// <summary>
/// Describes the type of the attached document.
/// </summary>
public class DocKindInfo
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

/// <summary>
/// Cryptographic signature metadata for an attachment.
/// </summary>
public class Signature
{
    [XmlAttribute("type")]
    public string? Type { get; set; }

    [XmlText]
    public string? Value { get; set; }
}
