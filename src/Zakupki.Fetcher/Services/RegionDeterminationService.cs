using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Zakupki.Fetcher.Services;

public class RegionDeterminationService
{
    public byte DetermineRegionCode(
        IEnumerable<string?> factAddresses,
        IEnumerable<string?> innCandidates,
        byte fallbackRegion)
    {
        var regionFromAddress = ExtractRegionFromFactAddresses(factAddresses);
        if (regionFromAddress > 0)
        {
            return regionFromAddress;
        }

        foreach (var inn in innCandidates)
        {
            var region = ExtractRegionFromInn(inn);
            if (region > 0)
            {
                return region;
            }
        }

        return fallbackRegion;
    }

    public byte ExtractRegionFromInn(string? inn)
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

    private byte ExtractRegionFromFactAddresses(IEnumerable<string?> factAddresses)
    {
        byte? detected = null;

        foreach (var address in factAddresses)
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

    private byte ExtractRegionFromAddress(string? address)
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
        var detectedPosition = int.MaxValue;
        var hasConflictAtDetectedPosition = false;

        foreach (var keyword in RegionKeywordsToCode)
        {
            var position = IndexOfWord(normalizedAddress, keyword.Key);
            if (position < 0)
            {
                continue;
            }

            if (position < detectedPosition)
            {
                detected = keyword.Value;
                detectedPosition = position;
                hasConflictAtDetectedPosition = false;
                continue;
            }

            if (position == detectedPosition && detected != keyword.Value)
            {
                hasConflictAtDetectedPosition = true;
            }
        }

        if (detected is not null && !hasConflictAtDetectedPosition)
        {
            return detected.Value;
        }

        if (detected is null)
        {
            return ExtractRegionFromAddressFuzzy(normalizedAddress);
        }

        return 0;
    }

    private byte ExtractRegionFromAddressFuzzy(string normalizedAddress)
    {
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return 0;
        }

        var tokens = normalizedAddress
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            return 0;
        }

        const int maxDistance = 1;
        const int minKeywordLength = 6;

        byte? bestRegion = null;
        var bestDistance = int.MaxValue;
        var bestTokenPosition = int.MaxValue;

        foreach (var seed in RegionKeywordSeeds)
        {
            var regionCode = seed.Code;

            foreach (var rawKeyword in seed.Keywords)
            {
                if (string.IsNullOrWhiteSpace(rawKeyword))
                {
                    continue;
                }

                var keyword = rawKeyword.Trim();

                if (keyword.Contains(' '))
                {
                    continue;
                }

                if (keyword.Length < minKeywordLength)
                {
                    continue;
                }

                foreach (var token in tokens)
                {
                    if (Math.Abs(token.Length - keyword.Length) > maxDistance)
                    {
                        continue;
                    }

                    var distance = LevenshteinDistance(token, keyword);
                    if (distance > maxDistance)
                    {
                        continue;
                    }

                    var tokenPosition = normalizedAddress.IndexOf(token, StringComparison.Ordinal);

                    var isBetter =
                        distance < bestDistance ||
                        (distance == bestDistance && tokenPosition >= 0 && tokenPosition < bestTokenPosition);

                    if (isBetter)
                    {
                        bestDistance = distance;
                        bestRegion = regionCode;
                        bestTokenPosition = tokenPosition;
                    }
                }
            }
        }

        return bestRegion ?? 0;
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (ReferenceEquals(source, target))
        {
            return 0;
        }

        if (source.Length == 0)
        {
            return target.Length;
        }

        if (target.Length == 0)
        {
            return source.Length;
        }

        var n = source.Length;
        var m = target.Length;

        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;

                var deletion = d[i - 1, j] + 1;
                var insertion = d[i, j - 1] + 1;
                var substitution = d[i - 1, j - 1] + cost;

                var value = deletion;
                if (insertion < value)
                {
                    value = insertion;
                }

                if (substitution < value)
                {
                    value = substitution;
                }

                d[i, j] = value;
            }
        }

        return d[n, m];
    }

    private static int IndexOfWord(string source, string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return -1;
        }

        var startIndex = 0;
        while (true)
        {
            var position = source.IndexOf(keyword, startIndex, StringComparison.Ordinal);
            if (position < 0)
            {
                return -1;
            }

            var wordStart = position == 0 || char.IsWhiteSpace(source[position - 1]);
            var wordEnd = position + keyword.Length == source.Length ||
                          char.IsWhiteSpace(source[position + keyword.Length]);

            if (wordStart && wordEnd)
            {
                return position;
            }

            startIndex = position + keyword.Length;

            if (startIndex >= source.Length)
            {
                return -1;
            }
        }
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

    private static readonly (byte Code, string[] Keywords)[] RegionKeywordSeeds =
    {
    (01, new[] { "адыгея", "адыгейская", "майкоп" }),
    (02, new[] { "башкортостан", "башкирия", "башкирская", "уфа" }),
    (03, new[] { "бурятия", "бурятская", "улан удэ" }),
    (04, new[] { "алтай республика", "горный алтай", "алтай респ", "горно алтайск" }),
    (05, new[] { "дагестан", "дагестанская", "махачкала" }),
    (06, new[] { "ингушетия", "ингушская", "магас" }),
    (07, new[] { "кабардино балкария", "кабардино балкарская", "налчик" }),
    (08, new[] { "карачаево черкесия", "карачаево черкесская", "черкесск" }),
    (09, new[] { "карелия", "карельская", "петрозаводск" }),
    (10, new[] { "коми", "сыктывкар" }),
    (11, new[] { "марий эл", "йошкар ола" }),
    (12, new[] { "мордовия", "мордовская", "саранск" }),
    (13, new[] { "мордовия старый", "мордовская старый", "саранск" }),
    (14, new[] { "саха", "якутия", "саха якутия", "якутск" }),
    (15, new[] { "северная осетия", "алания", "осетия алания", "владикавказ" }),
    (16, new[] { "татарстан", "татарская", "казань" }),
    (17, new[] { "тыва", "тува", "кызыл" }),
    (18, new[] { "удмуртия", "удмуртская", "ижевск" }),
    (19, new[] { "хакасия", "хакасская", "абакан" }),
    (20, new[] { "чечня", "чеченская", "грозный" }),
    (21, new[] { "чувашия", "чувашская", "чебоксары" }),
    (22, new[] { "алтайский край", "алтайский", "барнаул" }),
    (23, new[] { "краснодарский край", "краснодарский", "краснодар" }),
    (24, new[] { "красноярский край", "красноярский", "красноярск" }),
    (25, new[] { "приморский край", "приморский", "владивосток" }),
    (26, new[] { "ставропольский край", "ставропольский", "ставрополь" }),
    (27, new[] { "хабаровский край", "хабаровский", "хабаровск" }),
    (28, new[] { "амурская", "благовещенск" }),
    (29, new[] { "архангельская", "архангельск" }),
    (30, new[] { "астраханская", "астрахань" }),
    (31, new[] { "белгородская", "белгород" }),
    (32, new[] { "брянская", "брянск" }),
    (33, new[] { "влаимирская", "владимир" }),
    (34, new[] { "волгоградская", "волгоград" }),
    (35, new[] { "вологодская", "вологда" }),
    (36, new[] { "воронежская", "воронеж" }),
    (37, new[] { "ивановская", "иваново" }),
    (38, new[] { "иркутская", "иркутск" }),
    (39, new[] { "калининградская", "калининград" }),
    (40, new[] { "калужская", "калуга" }),
    (41, new[] { "камчатский", "петропавловск камчатский" }),
    (42, new[] { "кемеровская", "кузбасс", "кемерово" }),
    (43, new[] { "кировская", "киров" }),
    (44, new[] { "костромская", "кострома" }),
    (45, new[] { "курганская", "курган" }),
    (46, new[] { "курская", "курск" }),
    (47, new[] { "ленинградская" }),
    (48, new[] { "липецкая", "липецк" }),
    (49, new[] { "магаданская", "магадан" }),
    (50, new[] { "московская" }),
    (51, new[] { "мурманская", "мурманск" }),
    (52, new[] { "нижегородская", "нижний новгород" }),
    (53, new[] { "новгородская", "великий новгород" }),
    (54, new[] { "новосибирская", "новосибирск" }),
    (55, new[] { "омская", "омск" }),
    (56, new[] { "оренбургская", "оренбург" }),
    (57, new[] { "орловская", "орел" }),
    (58, new[] { "пензенская", "пенза" }),
    (59, new[] { "пермский", "пермь" }),
    (60, new[] { "псковская", "псков" }),
    (61, new[] { "ростовская", "ростов на дону" }),
    (62, new[] { "рязанская", "рязань" }),
    (63, new[] { "самарская", "самара" }),
    (64, new[] { "саратовская", "саратов" }),
    (65, new[] { "сахалинская", "южно сахалинск" }),
    (66, new[] { "свердловская", "екатеринбург" }),
    (67, new[] { "смоленская", "смоленск" }),
    (68, new[] { "тамбовская", "тамбов" }),
    (69, new[] { "тверская", "тверь" }),
    (70, new[] { "томская", "томск" }),
    (71, new[] { "тульская", "тула" }),
    (72, new[] { "тюменская", "тюмень" }),
    (73, new[] { "ульяновская", "ульяновск" }),
    (74, new[] { "челябинская", "челябинск" }),
    (75, new[] { "забайкальский", "чита" }),
    (76, new[] { "ярославская", "ярославль" }),
    (77, new[] { "москва" }),
    (78, new[] { "санкт петербург", "санкт-петербург", "питер", "спб" }),
    (79, new[] { "еврейская", "биробиджан" }),
    (81, new[] { "пермский старый", "пермь" }),
    (82, new[] { "крым", "крым республика", "симферополь" }),
    (83, new[] { "ненецкий", "нарьян мар" }),
    (84, new[] { "красноярский старый", "красноярск" }),
    (85, new[] { "иркутская старый", "иркутск" }),
    (86, new[] { "ханты мансийский", "гра", "хмао", "ханты мансийск" }),
    (87, new[] { "чукотский", "анадырь" }),
    (88, new[] { "ямало ненецкий", "янао", "салехард" }),
    (89, new[] { "ямало ненецкий дубль", "салехард" }),
    (90, new[] { "запорожская", "запорожье" }),
    (92, new[] { "севастополь" }),
    (93, new[] { "херсонская", "херсон" }),
    (94, new[] { "донецкая", "днр", "донецк" }),
    (95, new[] { "луганская", "лнр", "луганск" }),
};


    private static readonly IReadOnlyDictionary<string, byte> RegionKeywordsToCode = CreateRegionKeywordDictionary();
    private static IReadOnlyDictionary<string, byte> CreateRegionKeywordDictionary()
    {
        var dictionary = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in RegionKeywordSeeds)
        {
            foreach (var keyword in seed.Keywords)
            {
                AddRegionKeyword(dictionary, keyword, seed.Code);
            }
        }

        return dictionary;
    }

    private static void AddRegionKeyword(Dictionary<string, byte> dictionary, string keyword, byte code)
    {
        var trimmed = keyword?.Trim();
        if (string.IsNullOrEmpty(trimmed) || dictionary.ContainsKey(trimmed))
        {
            return;
        }

        dictionary[trimmed] = code;
    }
}
