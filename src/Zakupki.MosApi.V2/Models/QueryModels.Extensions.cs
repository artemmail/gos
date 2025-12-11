using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Zakupki.MosApi.V2;

public partial class GetQueryDataDto
{
    [JsonPropertyName("attachments")]
    public List<GetQueryDataAttachmentDto>? attachments { get; set; }
}

public class GetQueryDataAttachmentDto
{
    [JsonPropertyName("publishedContentId")]
    public string? publishedContentId { get; set; }

    [JsonPropertyName("fileName")]
    public string? fileName { get; set; }

    [JsonPropertyName("fileSize")]
    public long? fileSize { get; set; }

    [JsonPropertyName("description")]
    public string? description { get; set; }

    [JsonPropertyName("documentDate")]
    public DateTimeOffset? documentDate { get; set; }

    [JsonPropertyName("documentKindCode")]
    public string? documentKindCode { get; set; }

    [JsonPropertyName("documentKindName")]
    public string? documentKindName { get; set; }

    [JsonPropertyName("url")]
    public string? url { get; set; }

    [JsonPropertyName("contentHash")]
    public string? contentHash { get; set; }
}
