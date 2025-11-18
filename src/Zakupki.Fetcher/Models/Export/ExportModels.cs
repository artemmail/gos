using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Represents the root <export> element from the EIS notification files.
/// It may contain one of several notification payloads depending on the procedure type.
/// </summary>
[XmlRoot(ElementName = "export", Namespace = Ns.Export)]
public class Export
{
    /// <summary>
    /// Notification for EF2020 procedures.
    /// </summary>
    [XmlElement(ElementName = "epNotificationEF2020", Namespace = Ns.Export)]
    public EpNotificationEf2020? EpNotification { get; set; }

    /// <summary>
    /// Notification for EOK2020 procedures.
    /// </summary>
    [XmlElement(ElementName = "epNotificationEOK2020", Namespace = Ns.Export)]
    public EpNotificationEok2020? EpNotificationEok2020 { get; set; }

    /// <summary>
    /// Notification for EZK2020 procedures.
    /// </summary>
    [XmlElement(ElementName = "epNotificationEZK2020", Namespace = Ns.Export)]
    public EpNotificationEzk2020? EpNotificationEzk2020 { get; set; }

    /// <summary>
    /// Contract payloads that are returned for signed agreements.
    /// </summary>
    [XmlElement(ElementName = "contract", Namespace = Ns.Export)]
    public ContractExport? Contract { get; set; }

    /// <summary>
    /// Provides a convenient access point to the populated notification regardless of its specific type.
    /// </summary>
    [XmlIgnore]
    public EpNotificationEf2020? AnyNotification
    {
        get
        {
            if (EpNotification is not null)
            {
                return EpNotification;
            }

            if (EpNotificationEok2020 is not null)
            {
                return EpNotificationEok2020;
            }

            return EpNotificationEzk2020;
        }
    }
}

/// <summary>
/// Describes a generic notification payload in the EF2020 schema.
/// Specific procedure types inherit from this model without adding new fields.
/// </summary>
public class EpNotificationEf2020
{
    [XmlAttribute("schemeVersion")]
    public string? SchemeVersion { get; set; }

    [XmlElement(ElementName = "id", Namespace = Ns.EPtypes)]
    public string? Id { get; set; }

    [XmlElement(ElementName = "externalId", Namespace = Ns.EPtypes)]
    public string? ExternalId { get; set; }

    [XmlElement(ElementName = "versionNumber", Namespace = Ns.EPtypes)]
    public int VersionNumber { get; set; }

    [XmlElement(ElementName = "commonInfo", Namespace = Ns.EPtypes)]
    public CommonInfo? CommonInfo { get; set; }

    [XmlElement(ElementName = "purchaseResponsibleInfo", Namespace = Ns.EPtypes)]
    public PurchaseResponsibleInfo? PurchaseResponsibleInfo { get; set; }

    [XmlElement(ElementName = "printFormInfo", Namespace = Ns.EPtypes)]
    public PrintFormInfo? PrintFormInfo { get; set; }

    [XmlElement(ElementName = "attachmentsInfo", Namespace = Ns.EPtypes)]
    public AttachmentsInfo? AttachmentsInfo { get; set; }

    [XmlElement(ElementName = "serviceSigns", Namespace = Ns.EPtypes)]
    public ServiceSigns? ServiceSigns { get; set; }

    [XmlElement(ElementName = "notificationInfo", Namespace = Ns.EPtypes)]
    public NotificationInfo? NotificationInfo { get; set; }

    [XmlElement(ElementName = "printFormFieldsInfo", Namespace = Ns.EPtypes)]
    public PrintFormFieldsInfo? PrintFormFieldsInfo { get; set; }

    [XmlElement(ElementName = "modificationInfo", Namespace = Ns.EPtypes)]
    public ModificationInfo? ModificationInfo { get; set; }
}

/// <summary>
/// Notification wrapper for EOK2020 procedures. Contains the same payload as <see cref="EpNotificationEf2020"/>.
/// </summary>
public class EpNotificationEok2020 : EpNotificationEf2020
{
}

/// <summary>
/// Notification wrapper for EZK2020 procedures. Contains the same payload as <see cref="EpNotificationEf2020"/>.
/// </summary>
public class EpNotificationEzk2020 : EpNotificationEf2020
{
}
