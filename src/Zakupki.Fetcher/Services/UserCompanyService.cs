using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Services;

public sealed class UserCompanyService
{
    private static readonly RegionOption[] _availableRegions =
    {
        new("01", "Респ. Адыгея"),
        new("02", "Респ. Башкортостан"),
        new("03", "Респ. Бурятия"),
        new("04", "Респ. Алтай"),
        new("05", "Респ. Дагестан"),
        new("06", "Респ. Ингушетия"),
        new("07", "Кабардино-Балкарская Респ."),
        new("08", "Карачаево-Черкесская Респ."),
        new("09", "Респ. Карелия"),
        new("10", "Респ. Коми"),
        new("11", "Респ. Марий Эл"),
        new("12", "Респ. Мордовия"),
        new("13", "Респ. Мордовия (старый код, дублирует 12)"),
        new("14", "Респ. Саха (Якутия)"),
        new("15", "Респ. Северная Осетия — Алания"),
        new("16", "Респ. Татарстан"),
        new("17", "Респ. Тыва"),
        new("18", "Удмуртская Респ."),
        new("19", "Респ. Хакасия"),
        new("20", "Чеченская Респ."),
        new("21", "Чувашская Респ."),
        new("22", "Алтайский край"),
        new("23", "Краснодарский край"),
        new("24", "Красноярский край"),
        new("25", "Приморский край"),
        new("26", "Ставропольский край"),
        new("27", "Хабаровский край"),
        new("28", "Амурская обл."),
        new("29", "Архангельская обл."),
        new("30", "Астраханская обл."),
        new("31", "Белгородская обл."),
        new("32", "Брянская обл."),
        new("33", "Владимирская обл."),
        new("34", "Волгоградская обл."),
        new("35", "Вологодская обл."),
        new("36", "Воронежская обл."),
        new("37", "Ивановская обл."),
        new("38", "Иркутская обл."),
        new("39", "Калининградская обл."),
        new("40", "Калужская обл."),
        new("41", "Камчатский край"),
        new("42", "Кемеровская обл. — Кузбасс"),
        new("43", "Кировская обл."),
        new("44", "Костромская обл."),
        new("45", "Курганская обл."),
        new("46", "Курская обл."),
        new("47", "Ленинградская обл."),
        new("48", "Липецкая обл."),
        new("49", "Магаданская обл."),
        new("50", "Московская обл."),
        new("51", "Мурманская обл."),
        new("52", "Нижегородская обл."),
        new("53", "Новгородская обл."),
        new("54", "Новосибирская обл."),
        new("55", "Омская обл."),
        new("56", "Оренбургская обл."),
        new("57", "Орловская обл."),
        new("58", "Пензенская обл."),
        new("59", "Пермский край"),
        new("60", "Псковская обл."),
        new("61", "Ростовская обл."),
        new("62", "Рязанская обл."),
        new("63", "Самарская обл."),
        new("64", "Саратовская обл."),
        new("65", "Сахалинская обл."),
        new("66", "Свердловская обл."),
        new("67", "Смоленская обл."),
        new("68", "Тамбовская обл."),
        new("69", "Тверская обл."),
        new("70", "Томская обл."),
        new("71", "Тульская обл."),
        new("72", "Тюменская обл."),
        new("73", "Ульяновская обл."),
        new("74", "Челябинская обл."),
        new("75", "Забайкальский край"),
        new("76", "Ярославская обл."),
        new("77", "г. Москва"),
        new("78", "г. Санкт-Петербург"),
        new("79", "Еврейская авт. обл."),
        new("80", "Забайкальский край (старый код, дублирует 75)"),
        new("81", "Пермский край (старый код, дублирует 59)"),
        new("82", "Респ. Крым"),
        new("83", "Ненецкий авт. округ"),
        new("84", "Красноярский край (старый код, дублирует 24)"),
        new("85", "Иркутская обл. (старый код)"),
        new("86", "Ханты-Мансийский авт. округ — Югра"),
        new("87", "Чукотский авт. округ"),
        new("88", "Ямало-Ненецкий авт. округ"),
        new("89", "Ямало-Ненецкий авт. округ (дублирующий)"),
        new("90", "Запорожская область (новые территории)"),
        new("91", "Республика Крым (старый код до 2014)"),
        new("92", "Севастополь"),
        new("93", "Херсонская область (новые территории)"),
        new("94", "Донецкая Народная Республика (ДНР)"),
        new("95", "Луганская Народная Республика (ЛНР)"),
        new("96", "Не используется"),
        new("97", "Москва (доп. код)"),
        new("98", "Санкт-Петербург (доп. код)"),
        new("99", "Москва (доп. код)")
    };

    private static readonly Dictionary<string, RegionOption> _regionsByCode = _availableRegions
        .ToDictionary(region => region.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, RegionOption> _regionsByName = CreateRegionsByNameDictionary();

    private static readonly Dictionary<string, string> _legacyNameToCode = CreateLegacyNameToCodeDictionary();

    private static Dictionary<string, RegionOption> CreateRegionsByNameDictionary()
    {
        var dictionary = new Dictionary<string, RegionOption>(StringComparer.OrdinalIgnoreCase);

        foreach (var region in _availableRegions)
        {
            var key = NormalizeRegionName(region.Name);
            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = region;
            }
        }

        return dictionary;
    }

    private static Dictionary<string, string> CreateLegacyNameToCodeDictionary()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Республика Адыгея"] = "01",
            ["Республика Башкортостан"] = "02",
            ["Республика Бурятия"] = "03",
            ["Республика Алтай"] = "04",
            ["Республика Дагестан"] = "05",
            ["Республика Ингушетия"] = "06",
            ["Кабардино-Балкарская Республика"] = "07",
            ["Республика Калмыкия"] = "08",
            ["Карачаево-Черкесская Республика"] = "09",
            ["Республика Карелия"] = "10",
            ["Республика Коми"] = "11",
            ["Республика Марий Эл"] = "12",
            ["Республика Мордовия"] = "13",
            ["Республика Саха (Якутия)"] = "14",
            ["Республика Северная Осетия — Алания"] = "15",
            ["Республика Татарстан"] = "16",
            ["Республика Тыва"] = "17",
            ["Удмуртская Республика"] = "18",
            ["Республика Хакасия"] = "19",
            ["Чеченская Республика"] = "20",
            ["Чувашская Республика"] = "21",

            ["Алтайский край"] = "22",
            ["Краснодарский край"] = "23",
            ["Красноярский край"] = "24",
            ["Приморский край"] = "25",
            ["Ставропольский край"] = "26",
            ["Хабаровский край"] = "27",
            ["Амурская область"] = "28",
            ["Архангельская область"] = "29",
            ["Астраханская область"] = "30",
            ["Белгородская область"] = "31",
            ["Брянская область"] = "32",
            ["Владимирская область"] = "33",
            ["Волгоградская область"] = "34",
            ["Вологодская область"] = "35",
            ["Воронежская область"] = "36",
            ["Ивановская область"] = "37",
            ["Иркутская область"] = "38",
            ["Калининградская область"] = "39",
            ["Калужская область"] = "40",
            ["Камчатский край"] = "41",
            ["Кемеровская область"] = "42",
            ["Кировская область"] = "43",
            ["Костромская область"] = "44",
            ["Курганская область"] = "45",
            ["Курская область"] = "46",
            ["Ленинградская область"] = "47",
            ["Липецкая область"] = "48",
            ["Магаданская область"] = "49",
            ["Московская область"] = "50",
            ["Мурманская область"] = "51",
            ["Нижегородская область"] = "52",
            ["Новгородская область"] = "53",
            ["Новосибирская область"] = "54",
            ["Омская область"] = "55",
            ["Оренбургская область"] = "56",
            ["Орловская область"] = "57",
            ["Пензенская область"] = "58",
            ["Пермский край"] = "59",
            ["Псковская область"] = "60",
            ["Ростовская область"] = "61",
            ["Рязанская область"] = "62",
            ["Самарская область"] = "63",
            ["Саратовская область"] = "64",
            ["Сахалинская область"] = "65",
            ["Свердловская область"] = "66",
            ["Смоленская область"] = "67",
            ["Тамбовская область"] = "68",
            ["Тверская область"] = "69",
            ["Томская область"] = "70",
            ["Тульская область"] = "71",
            ["Тюменская область"] = "72",
            ["Ульяновская область"] = "73",
            ["Челябинская область"] = "74",
            ["Забайкальский край"] = "75",
            ["Ярославская область"] = "76",

            ["Москва"] = "77",
            ["Санкт-Петербург"] = "78",
            ["Еврейская автономная область"] = "79",

            ["Ненецкий автономный округ"] = "83",
            ["Ханты-Мансийский автономный округ — Югра"] = "86",
            ["Чукотский автономный округ"] = "87",
            ["Ямало-Ненецкий автономный округ"] = "89",

            // Новые субъекты в кодировке ФНС
            ["Запорожская область"] = "90",
            ["Республика Крым"] = "91",
            ["Севастополь"] = "92",
            ["Донецкая Народная Республика"] = "93",
            ["Луганская Народная Республика"] = "94",
            ["Херсонская область"] = "95",

            // По желанию можно добавить:
            // ["Иные территории, включая город и космодром Байконур"] = "99"
        };
    }

    private static string NormalizeRegionName(string region)
    {
        var normalized = region.Trim();
        normalized = normalized.Replace(" - ", " — ");
        normalized = normalized.Replace('-', '—');
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized;
    }

    private readonly NoticeDbContext _dbContext;

    public UserCompanyService(NoticeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserCompanyProfile> GetProfileAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Regions)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException($"User with id '{userId}' was not found.");
        }

        var regionCodes = new HashSet<string>(
            user.Regions
                .Select(r => r.Region)
                .Select(MapRegionToCode)
                .Where(code => code is not null)
                .Select(code => code!),
            StringComparer.OrdinalIgnoreCase);

        var orderedRegions = _availableRegions
            .Where(region => regionCodes.Contains(region.Code))
            .Select(region => region.Code)
            .ToArray();

        return new UserCompanyProfile(user.CompanyInfo ?? string.Empty, orderedRegions);
    }

    public async Task<UserCompanyProfile> UpdateProfileAsync(
        string userId,
        string? companyInfo,
        IReadOnlyCollection<string>? regions,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Regions)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException($"User with id '{userId}' was not found.");
        }

        var trimmedInfo = (companyInfo ?? string.Empty).Trim();
        user.CompanyInfo = trimmedInfo;

        var normalizedInput = regions ?? Array.Empty<string>();
        var selectedRegionCodes = new HashSet<string>(
            normalizedInput
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(MapRegionToCode)
                .Where(code => code is not null)
                .Select(code => code!),
            StringComparer.OrdinalIgnoreCase);

        var selectedRegions = _availableRegions
            .Where(region => selectedRegionCodes.Contains(region.Code))
            .Select(region => region.Code)
            .ToList();

        _dbContext.ApplicationUserRegions.RemoveRange(user.Regions);
        user.Regions.Clear();

        foreach (var region in selectedRegions)
        {
            user.Regions.Add(new ApplicationUserRegion
            {
                Region = region,
                UserId = userId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserCompanyProfile(user.CompanyInfo ?? string.Empty, selectedRegions);
    }

    public IReadOnlyCollection<RegionOption> GetAvailableRegions() => _availableRegions;

    private static string? MapRegionToCode(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return null;
        }

        var trimmed = region.Trim();

        if (_regionsByCode.TryGetValue(trimmed, out var byCode))
        {
            return byCode.Code;
        }

        if (_regionsByName.TryGetValue(NormalizeRegionName(trimmed), out var byName))
        {
            return byName.Code;
        }

        if (_legacyNameToCode.TryGetValue(trimmed, out var legacyCode))
        {
            return legacyCode;
        }

        var normalized = NormalizeRegionName(trimmed);
        if (_legacyNameToCode.TryGetValue(normalized, out var normalizedLegacyCode))
        {
            return normalizedLegacyCode;
        }

        return null;
    }

    public sealed record UserCompanyProfile(string CompanyInfo, IReadOnlyCollection<string> Regions);

    public sealed record RegionOption(string Code, string Name);
}
