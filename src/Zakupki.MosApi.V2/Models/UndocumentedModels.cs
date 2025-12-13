using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Zakupki.MosApi.V2
{
    public class UndocumentedAuctionDto
    {
        [JsonPropertyName("name")]
        public string? name { get; set; }

        [JsonPropertyName("startDate")]
        public string? startDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? endDate { get; set; }

        [JsonPropertyName("startCost")]
        public double? startCost { get; set; }

        [JsonPropertyName("federalLawName")]
        public string? federalLawName { get; set; }

        [JsonPropertyName("auctionRegion")]
        public List<UndocumentedAuctionRegionDto>? auctionRegion { get; set; }

        [JsonPropertyName("files")]
        public List<UndocumentedAuctionFileDto>? files { get; set; }

        [JsonPropertyName("customer")]
        public UndocumentedCompanyDto? customer { get; set; }

        [JsonPropertyName("id")]
        public int? id { get; set; }
    }

    public class UndocumentedAuctionResult
    {
        public UndocumentedAuctionResult(UndocumentedAuctionDto? auction, string rawJson)
        {
            Auction = auction;
            RawJson = rawJson;
        }

        public UndocumentedAuctionDto? Auction { get; }

        public string RawJson { get; }
    }

    public class UndocumentedAuctionFileDto
    {
        [JsonPropertyName("id")]
        public long? id { get; set; }

        [JsonPropertyName("name")]
        public string? name { get; set; }
    }

    public class UndocumentedAuctionRegionDto
    {
        [JsonPropertyName("id")]
        public int? id { get; set; }
    }

    public class UndocumentedCompanyDto
    {
        [JsonPropertyName("inn")]
        public string? inn { get; set; }

        [JsonPropertyName("name")]
        public string? name { get; set; }

        [JsonPropertyName("id")]
        public int? id { get; set; }
    }
}
