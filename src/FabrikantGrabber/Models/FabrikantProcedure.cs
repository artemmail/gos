using System;
using System.Collections.Generic;

namespace FabrikantGrabber.Models;

public sealed class FabrikantProcedure
{
    public string ExternalId { get; set; } = default!;
    public string ProcedureNumber { get; set; } = default!;
    public string LawSection { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string ProcedureType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public string OrganizerName { get; set; } = string.Empty;
    public string OrganizerInn { get; set; } = string.Empty;
    public string OrganizerKpp { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerInn { get; set; } = string.Empty;
    public string CustomerKpp { get; set; } = string.Empty;

    public DateTime? PublishDate { get; set; }
    public DateTime? ApplyStartDate { get; set; }
    public DateTime? ApplyEndDate { get; set; }
    public DateTime? ResultDate { get; set; }

    public decimal? Nmck { get; set; }
    public string Currency { get; set; } = "RUB";

    public string Okpd2 { get; set; } = string.Empty;
    public string Okved2 { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string DeliveryTerm { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public decimal? ApplicationSecurity { get; set; }
    public decimal? ContractSecurity { get; set; }

    public List<FabrikantLot> Lots { get; set; } = new();
    public List<DocumentationLink> Documents { get; set; } = new();

    public string RawHtml { get; set; } = string.Empty;
}
