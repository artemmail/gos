using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Zakupki.Fetcher.Services
{
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
                    // Конфликтующие регионы в разных адресах – считаем неопределённым
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

            // 1) Сначала точный поиск по RegionKeywordsToCode
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
                }
                else if (position == detectedPosition && detected != keyword.Value)
                {
                    hasConflictAtDetectedPosition = true;
                }
            }

            // Если точное совпадение однозначное – возвращаем его
            if (detected is not null && !hasConflictAtDetectedPosition)
            {
                return detected.Value;
            }

            // Если точного совпадения нет – подключаем fuzzy Levenshtein
            if (detected is null)
            {
                return ExtractRegionFromAddressFuzzy(normalizedAddress);
            }

            // Если была коллизия нескольких регионов – оставляем 0
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

            // Разрешаем до 2 "ошибок" (вставка/удаление/замена),
            // чтобы ловить "белгородскя" ~ "белгородская" и "калининградул" ~ "калининград"
            const int maxDistance = 2;
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

                    // Fuzzy только по однословным ключам типа "белгородская", "калининград"
                    if (keyword.Contains(' '))
                    {
                        continue;
                    }

                    // Отсекаем короткие ключи (уфа, томск и т.п.), чтобы меньше ловить шум
                    if (keyword.Length < minKeywordLength)
                    {
                        continue;
                    }

                    foreach (var token in tokens)
                    {
                        // Быстрый фильтр по длине
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
    (03, new[] { "бурятия", "бурятская", "улан удэ", "улан-удэ" }),
    (04, new[] { "алтай республика", "республика алтай", "горный алтай", "алтай респ", "горно алтайск", "горно-алтайск" }),
    (05, new[] { "дагестан", "дагестанская", "махачкала" }),
    (06, new[] { "ингушетия", "ингушская", "магас" }),
    (07, new[] { "кабардино балкария", "кабардино-балкария", "кабардино балкарская", "кабардино-балкарская", "налчик" }),

    // КАЛМЫКИЯ ВОТ ЗДЕСЬ
    (08, new[] { "калмыкия", "калмыцкая", "элиста" }),

    (09, new[] { "карачаево черкесия", "карачаево-черкесия", "карачаево черкесская", "карачаево-черкесская", "черкесск" }),
    (10, new[] { "карелия", "карельская", "петрозаводск" }),
    (11, new[] { "коми", "сыктывкар" }),
    (12, new[] { "марий эл", "йошкар ола", "йошкар-ола" }),
    (13, new[] { "мордовия", "мордовская", "саранск" }),
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

    (28, new[] { "амурская", "амурская область", "благовещенск" }),
    (29, new[] { "архангельская", "архангельская область", "архангельск" }),
    (30, new[] { "астраханская", "астраханская область", "астрахань" }),
    (31, new[] { "белгородская", "белгородская область", "белгород" }),
    (32, new[] { "брянская", "брянская область", "брянск" }),
    (33, new[] { "владимирская", "владимирская область", "владимир" }),
    (34, new[] { "волгоградская", "волгоградская область", "волгоград" }),
    (35, new[] { "вологодская", "вологодская область", "вологда" }),
    (36, new[] { "воронежская", "воронежская область", "воронеж" }),
    (37, new[] { "ивановская", "ивановская область", "иваново" }),
    (38, new[] { "иркутская", "иркутская область", "иркутск" }),
    (39, new[] { "калининградская", "калининградская область", "калининград" }),
    (40, new[] { "калужская", "калужская область", "калуга" }),
    (41, new[] { "камчатский край", "камчатский", "петропавловск камчатский", "петропавловск-камчатский" }),
    (42, new[] { "кемеровская", "кемеровская область", "кузбасс", "кемерово" }),
    (43, new[] { "кировская", "кировская область", "киров" }),
    (44, new[] { "костромская", "костромская область", "кострома" }),
    (45, new[] { "курганская", "курганская область", "курган" }),
    (46, new[] { "курская", "курская область", "курск" }),

    // Ленинградская – без Питера, чтобы не конфликтовать с 78
    (47, new[] { "ленинградская", "ленинградская область" }),

    (48, new[] { "липецкая", "липецкая область", "липецк" }),
    (49, new[] { "магаданская", "магаданская область", "магадан" }),

    // Московская – без "москва", чтобы не путать с 77
    (50, new[] { "московская", "московская область" }),

    (51, new[] { "мурманская", "мурманская область", "мурманск" }),
    (52, new[] { "нижегородская", "нижегородская область", "нижний новгород" }),
    (53, new[] { "новгородская", "новгородская область", "великий новгород" }),
    (54, new[] { "новосибирская", "новосибирская область", "новосибирск" }),
    (55, new[] { "омская", "омская область", "омск" }),
    (56, new[] { "оренбургская", "оренбургская область", "оренбург" }),
    (57, new[] { "орловская", "орловская область", "орел" }),
    (58, new[] { "пензенская", "пензенская область", "пенза" }),
    (59, new[] { "пермский край", "пермский", "пермь" }),
    (60, new[] { "псковская", "псковская область", "псков" }),
    (61, new[] { "ростовская", "ростовская область", "ростов на дону", "ростов-на-дону" }),
    (62, new[] { "рязанская", "рязанская область", "рязань" }),
    (63, new[] { "самарская", "самарская область", "самара" }),
    (64, new[] { "саратовская", "саратовская область", "саратов" }),
    (65, new[] { "сахалинская", "сахалинская область", "южно сахалинск", "южно-сахалинск" }),
    (66, new[] { "свердловская", "свердловская область", "екатеринбург" }),
    (67, new[] { "смоленская", "смоленская область", "смоленск" }),
    (68, new[] { "тамбовская", "тамбовская область", "тамбов" }),
    (69, new[] { "тверская", "тверская область", "тверь" }),
    (70, new[] { "томская", "томская область", "томск" }),
    (71, new[] { "тульская", "тульская область", "тула" }),
    (72, new[] { "тюменская", "тюменская область", "тюмень" }),
    (73, new[] { "ульяновская", "ульяновская область", "ульяновск" }),
    (74, new[] { "челябинская", "челябинская область", "челябинск" }),
    (75, new[] { "забайкальский край", "забайкальский", "чита" }),
    (76, new[] { "ярославская", "ярославская область", "ярославль" }),

    (77, new[] { "москва" }),
    (78, new[] { "санкт петербург", "санкт-петербург", "питер", "спб" }),
    (79, new[] { "еврейская", "еврейская автономная", "еврейская автономная область", "биробиджан" }),

    (83, new[] { "ненецкий", "ненецкий автономный округ", "ненецкий автономный", "нарьян мар", "нарьян-мар" }),
    (86, new[] { "ханты мансийский", "ханты-мансийский", "югра", "хмао", "ханты мансийск", "ханты-мансийск" }),
    (87, new[] { "чукотский", "чукотский автономный округ", "анадырь" }),
    (89, new[] { "ямало ненецкий", "ямало-ненецкий", "янао", "салехард" }),

    // Новые территории по ФНС
    (90, new[] { "запорожская", "запорожская область", "запорожье" }),
    (91, new[] { "крым", "крым республика", "республика крым", "симферополь" }),
    (92, new[] { "севастополь" }),
    (93, new[] { "донецкая", "донецкая народная республика", "днр", "донецк" }),
    (94, new[] { "луганская", "луганская народная республика", "лнр", "луганск" }),
    (95, new[] { "херсонская", "херсон" }),

    // При желании можно ещё добавить:
    // (99, new[] { "байконур", "иные территории" }),
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
}
