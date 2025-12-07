using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FabrikantGrabber.Models;

public sealed class FabrikantLot
{
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? StartPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string DeliveryTerm { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;

    public Dictionary<string, string> AdditionalFields { get; set; } = new();

    [JsonIgnore]
    public bool HasData =>
        !string.IsNullOrWhiteSpace(Number) ||
        !string.IsNullOrWhiteSpace(Name) ||
        StartPrice.HasValue ||
        Quantity.HasValue ||
        !string.IsNullOrWhiteSpace(Status) ||
        AdditionalFields.Count > 0;

    public string RawRow { get; set; } = string.Empty;
}
