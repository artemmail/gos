using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Zakupki.EF2020;

namespace Zakupki.Fetcher.Services;

internal static class RegionDetection
{
    private static readonly JsonSerializerOptions DeserializationOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static byte DetermineRegionCode(EpNotificationEf2020 notification, byte fallbackRegion)
    {
        var regionFromAddress = ExtractRegionFromFactAddresses(notification);
        if (regionFromAddress > 0)
        {
            return regionFromAddress;
        }

        foreach (var inn in EnumerateInnCandidates(notification))
        {
            var region = ExtractRegionFromInn(inn);
            if (region > 0)
            {
                return region;
            }
        }

        return fallbackRegion;
    }

    public static byte DetermineRegionCode(string? rawJson, byte fallbackRegion)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return fallbackRegion;
        }

        try
        {
            var notification = JsonSerializer.Deserialize<EpNotificationEf2020>(rawJson, DeserializationOptions);
            return notification is null
                ? fallbackRegion
                : DetermineRegionCode(notification, fallbackRegion);
        }
        catch (JsonException)
        {
            return fallbackRegion;
        }
    }

    private static byte ExtractRegionFromFactAddresses(EpNotificationEf2020 notification)
    {
        byte? detected = null;

        foreach (var address in EnumerateFactAddresses(notification))
        {
            var region = ExtractRegionFromAddress(address);
            if (region == 0)
            {
                continue;
            }

            if (detected is null)
            {
                detected = region;
            }
            else if (detected.Value != region)
            {
                return 0;
            }
        }

        return detected ?? 0;
    }

    private static IEnumerable<string?> EnumerateInnCandidates(EpNotificationEf2020 notification)
    {
        yield return notification.PurchaseResponsibleInfo?.ResponsibleOrgInfo?.INN;
        yield return notification.PurchaseResponsibleInfo?.SpecializedOrgInfo?.INN;

        var customerRequirements = notification.NotificationInfo?.CustomerRequirementsInfo?.Items;
        if (customerRequirements is not { Count: > 0 })
        {
            yield break;
        }

        foreach (var requirement in customerRequirements)
        {
            var applicationInn = requirement.ApplicationGuarantee?.AccountBudget?.AccountBudgetAdmin?.Inn;
            if (!string.IsNullOrWhiteSpace(applicationInn))
            {
                yield return applicationInn;
            }

            var contractInn = requirement.ContractGuarantee?.AccountBudget?.AccountBudgetAdmin?.Inn;
            if (!string.IsNullOrWhiteSpace(contractInn))
            {
                yield return contractInn;
            }
        }
    }

    private static IEnumerable<string?> EnumerateFactAddresses(EpNotificationEf2020 notification)
    {
        yield return notification.PurchaseResponsibleInfo?.ResponsibleOrgInfo?.FactAddress;
        yield return notification.PurchaseResponsibleInfo?.SpecializedOrgInfo?.FactAddress;
        yield return notification.PurchaseResponsibleInfo?.ResponsibleInfo?.OrgFactAddress;
    }

    private static byte ExtractRegionFromAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return 0;
        }

        var normalizedAddress = NormalizeRegionText(address);
        if (string.IsNullOrEmpty(normalizedAddress))
        {
            return 0;
        }

        byte? detected = null;

        foreach (var keyword in RegionKeywordsToCode)
        {
            if (!normalizedAddress.Contains(keyword.Key, StringComparison.Ordinal))
            {
                continue;
            }

            if (detected is null)
            {
                detected = keyword.Value;
            }
            else if (detected.Value != keyword.Value)
            {
                return 0;
            }
        }

        return detected ?? 0;
    }

    private static byte ExtractRegionFromInn(string? inn)
    {
        if (string.IsNullOrWhiteSpace(inn))
        {
            return 0;
        }

        var trimmed = inn.Trim();
        if (trimmed.Length < 2)
        {
            return 0;
        }

        var digits = trimmed.Where(char.IsDigit).ToArray();
        if (digits.Length < 2)
        {
            return 0;
        }

        return byte.Parse(new string(digits, 0, 2));
    }

    private static string NormalizeRegionText(string source)
    {
        var withoutParentheses = Regex.Replace(source, "\\s*\\([^)]*\\)", " ", RegexOptions.CultureInvariant);
        var lower = withoutParentheses.ToLowerInvariant();
        var normalizedDashes = lower.Replace('—', ' ').Replace('-', ' ');
        var withoutCommonWords = Regex.Replace(
            normalizedDashes,
            "\\b(обл\\.?|область|респ\\.?|республика|г\\.?|город)\\b",
            " ",
            RegexOptions.CultureInvariant);
        var withoutPunctuation = Regex.Replace(withoutCommonWords, "[.,]", " ", RegexOptions.CultureInvariant);
        return Regex.Replace(withoutPunctuation, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private static readonly IReadOnlyDictionary<string, byte> RegionKeywordsToCode = CreateRegionKeywordDictionary();

    private static readonly (byte Code, string[] Keywords)[] RegionKeywordSeeds =
    {
        (01, new[] { "адыгея", "адыгейская" }),
        (02, new[] { "башкортостан", "башкирия", "башкирская" }),
        (03, new[] { "бурятия", "бурятская" }),
        (04, new[] { "алтай республика", "горный алтай", "алтай респ" }),
        (05, new[] { "дагестан", "дагестанская" }),
        (06, new[] { "ингушетия", "ингушская" }),
        (07, new[] { "кабардино балкария", "кабардино балкарская" }),
        (08, new[] { "карачаево черкесия", "карачаево черкесская" }),
        (09, new[] { "карелия", "карельская" }),
        (10, new[] { "коми" }),
        (11, new[] { "марий эл" }),
        (12, new[] { "мордовия", "мордовская" }),
        (13, new[] { "мордовия старый", "мордовская старый" }),
        (14, new[] { "саха", "якутия", "саха якутия" }),
        (15, new[] { "северная осетия", "алания", "осетия алания" }),
        (16, new[] { "татарстан", "татарская" }),
        (17, new[] { "тыва", "тува" }),
        (18, new[] { "удмуртия", "удмуртская" }),
        (19, new[] { "хакасия", "хакасская" }),
        (20, new[] { "чечня", "чеченская" }),
        (21, new[] { "чувашия", "чувашская" }),
        (22, new[] { "алтайский край", "алтайский" }),
        (23, new[] { "краснодарский край", "краснодарский" }),
        (24, new[] { "красноярский край", "красноярский" }),
        (25, new[] { "приморский край", "приморский" }),
        (26, new[] { "ставропольский край", "ставропольский" }),
        (27, new[] { "хабаровский край", "хабаровский" }),
        (28, new[] { "амурская" }),
        (29, new[] { "архангельская" }),
        (30, new[] { "астраханская" }),
        (31, new[] { "белгородская" }),
        (32, new[] { "брянская" }),
        (33, new[] { "владимирская" }),
        (34, new[] { "волгоградская" }),
        (35, new[] { "вологодская" }),
        (36, new[] { "воронежская" }),
        (37, new[] { "ивановская" }),
        (38, new[] { "иркутская" }),
        (39, new[] { "калининградская" }),
        (40, new[] { "калужская" }),
        (41, new[] { "камчатский" }),
        (42, new[] { "кемеровская", "кузбасс" }),
        (43, new[] { "кировская" }),
        (44, new[] { "костромская" }),
        (45, new[] { "курганская" }),
        (46, new[] { "курская" }),
        (47, new[] { "ленинградская", "санкт петербургская область", "санкт-петербургская область" }),
        (48, new[] { "липецкая" }),
        (49, new[] { "магаданская" }),
        (50, new[] { "московская" }),
        (51, new[] { "мурманская" }),
        (52, new[] { "нижегородская", "нижегородский" }),
        (53, new[] { "новгородская" }),
        (54, new[] { "новосибирская" }),
        (55, new[] { "омская" }),
        (56, new[] { "оренбургская" }),
        (57, new[] { "орловская" }),
        (58, new[] { "пензенская" }),
        (59, new[] { "пермский" }),
        (60, new[] { "псковская" }),
        (61, new[] { "ростовская" }),
        (62, new[] { "рязанская" }),
        (63, new[] { "самарская" }),
        (64, new[] { "саратовская" }),
        (65, new[] { "сахалинская" }),
        (66, new[] { "свердловская" }),
        (67, new[] { "смоленская" }),
        (68, new[] { "тамбовская" }),
        (69, new[] { "тверская" }),
        (70, new[] { "томская" }),
        (71, new[] { "тульская" }),
        (72, new[] { "тюменская" }),
        (73, new[] { "ульяновская" }),
        (74, new[] { "челябинская" }),
        (75, new[] { "забайкальский" }),
        (76, new[] { "ярославская" }),
        (77, new[] { "москва" }),
        (78, new[] { "санкт петербург", "санкт-петербург" }),
        (79, new[] { "еврейская" }),
        (80, Array.Empty<string>()),
        (81, new[] { "хакасская" }),
        (82, new[] { "крым" }),
        (83, new[] { "ненецкий" }),
        (84, new[] { "красноярский таймырский" }),
        (85, new[] { "иркутская ульяновская", "иркутская (улыбка)" }),
        (86, new[] { "ханты мансийский", "ханты-мансийский" }),
        (87, new[] { "чукотский" }),
        (88, new[] { "ямало ненецкий", "ямало-ненецкий" }),
        (89, new[] { "московская старая", "московская старая область" }),
        (92, new[] { "севастополь" }),
        (94, new[] { "байконур" }),
        (95, new[] { "чеченская старая" }),
        (97, new[] { "москва новая" }),
        (99, new[] { "москва и московская область", "москве и московской области" })
    };

    private static IReadOnlyDictionary<string, byte> CreateRegionKeywordDictionary()
    {
        var keywords = new Dictionary<string, byte>(StringComparer.Ordinal);

        foreach (var seed in RegionKeywordSeeds)
        {
            foreach (var keyword in seed.Keywords)
            {
                keywords.Add(keyword, seed.Code);
            }
        }

        return keywords;
    }
}
