using System;
using System.Collections.Generic;
using System.Globalization;
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
        CreateRegion(01, "Респ. Адыгея"),
        CreateRegion(02, "Респ. Башкортостан"),
        CreateRegion(03, "Респ. Бурятия"),
        CreateRegion(04, "Респ. Алтай"),
        CreateRegion(05, "Респ. Дагестан"),
        CreateRegion(06, "Респ. Ингушетия"),
        CreateRegion(07, "Кабардино-Балкарская Респ."),
        CreateRegion(08, "Карачаево-Черкесская Респ."),
        CreateRegion(09, "Респ. Карелия"),
        CreateRegion(10, "Респ. Коми"),
        CreateRegion(11, "Респ. Марий Эл"),
        CreateRegion(12, "Респ. Мордовия"),
        CreateRegion(13, "Респ. Мордовия (старый код, дублирует 12)"),
        CreateRegion(14, "Респ. Саха (Якутия)"),
        CreateRegion(15, "Респ. Северная Осетия — Алания"),
        CreateRegion(16, "Респ. Татарстан"),
        CreateRegion(17, "Респ. Тыва"),
        CreateRegion(18, "Удмуртская Респ."),
        CreateRegion(19, "Респ. Хакасия"),
        CreateRegion(20, "Чеченская Респ."),
        CreateRegion(21, "Чувашская Респ."),
        CreateRegion(22, "Алтайский край"),
        CreateRegion(23, "Краснодарский край"),
        CreateRegion(24, "Красноярский край"),
        CreateRegion(25, "Приморский край"),
        CreateRegion(26, "Ставропольский край"),
        CreateRegion(27, "Хабаровский край"),
        CreateRegion(28, "Амурская обл."),
        CreateRegion(29, "Архангельская обл."),
        CreateRegion(30, "Астраханская обл."),
        CreateRegion(31, "Белгородская обл."),
        CreateRegion(32, "Брянская обл."),
        CreateRegion(33, "Владимирская обл."),
        CreateRegion(34, "Волгоградская обл."),
        CreateRegion(35, "Вологодская обл."),
        CreateRegion(36, "Воронежская обл."),
        CreateRegion(37, "Ивановская обл."),
        CreateRegion(38, "Иркутская обл."),
        CreateRegion(39, "Калининградская обл."),
        CreateRegion(40, "Калужская обл."),
        CreateRegion(41, "Камчатский край"),
        CreateRegion(42, "Кемеровская обл. — Кузбасс"),
        CreateRegion(43, "Кировская обл."),
        CreateRegion(44, "Костромская обл."),
        CreateRegion(45, "Курганская обл."),
        CreateRegion(46, "Курская обл."),
        CreateRegion(47, "Ленинградская обл."),
        CreateRegion(48, "Липецкая обл."),
        CreateRegion(49, "Магаданская обл."),
        CreateRegion(50, "Московская обл."),
        CreateRegion(51, "Мурманская обл."),
        CreateRegion(52, "Нижегородская обл."),
        CreateRegion(53, "Новгородская обл."),
        CreateRegion(54, "Новосибирская обл."),
        CreateRegion(55, "Омская обл."),
        CreateRegion(56, "Оренбургская обл."),
        CreateRegion(57, "Орловская обл."),
        CreateRegion(58, "Пензенская обл."),
        CreateRegion(59, "Пермский край"),
        CreateRegion(60, "Псковская обл."),
        CreateRegion(61, "Ростовская обл."),
        CreateRegion(62, "Рязанская обл."),
        CreateRegion(63, "Самарская обл."),
        CreateRegion(64, "Саратовская обл."),
        CreateRegion(65, "Сахалинская обл."),
        CreateRegion(66, "Свердловская обл."),
        CreateRegion(67, "Смоленская обл."),
        CreateRegion(68, "Тамбовская обл."),
        CreateRegion(69, "Тверская обл."),
        CreateRegion(70, "Томская обл."),
        CreateRegion(71, "Тульская обл."),
        CreateRegion(72, "Тюменская обл."),
        CreateRegion(73, "Ульяновская обл."),
        CreateRegion(74, "Челябинская обл."),
        CreateRegion(75, "Забайкальский край"),
        CreateRegion(76, "Ярославская обл."),
        CreateRegion(77, "г. Москва"),
        CreateRegion(78, "г. Санкт-Петербург"),
        CreateRegion(79, "Еврейская авт. обл."),
        CreateRegion(80, "Забайкальский край (старый код, дублирует 75)"),
        CreateRegion(81, "Пермский край (старый код, дублирует 59)"),
        CreateRegion(82, "Респ. Крым"),
        CreateRegion(83, "Ненецкий авт. округ"),
        CreateRegion(84, "Красноярский край (старый код, дублирует 24)"),
        CreateRegion(85, "Иркутская обл. (старый код)"),
        CreateRegion(86, "Ханты-Мансийский авт. округ — Югра"),
        CreateRegion(87, "Чукотский авт. округ"),
        CreateRegion(88, "Ямало-Ненецкий авт. округ"),
        CreateRegion(89, "Ямало-Ненецкий авт. округ (дублирующий)"),
        CreateRegion(90, "Запорожская область (новые территории)"),
        CreateRegion(91, "Республика Крым (старый код до 2014)"),
        CreateRegion(92, "Севастополь"),
        CreateRegion(93, "Херсонская область (новые территории)"),
        CreateRegion(94, "Донецкая Народная Республика (ДНР)"),
        CreateRegion(95, "Луганская Народная Республика (ЛНР)"),
        CreateRegion(96, "Не используется"),
        CreateRegion(97, "Москва (доп. код)"),
        CreateRegion(98, "Санкт-Петербург (доп. код)"),
        CreateRegion(99, "Москва (доп. код)")
    };

    private static readonly Dictionary<byte, RegionOption> _regionsByCode = _availableRegions
        .ToDictionary(region => region.Code);

    private static RegionOption CreateRegion(byte code, string name) => new((code), name);

    private static byte ParseCode(string code) =>
        byte.Parse(code, NumberStyles.None, CultureInfo.InvariantCulture);

    public static string FormatRegionCode(byte region) =>
        region.ToString("D2", CultureInfo.InvariantCulture);

    private static readonly Dictionary<string, RegionOption> _regionsByName = CreateRegionsByNameDictionary();

    private static readonly Dictionary<string, byte> _legacyNameToCode = CreateLegacyNameToCodeDictionary();

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

    private static Dictionary<string, byte> CreateLegacyNameToCodeDictionary()
    {
        return new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["Республика Адыгея"] = ParseCode("01"),
            ["Республика Башкортостан"] = ParseCode("02"),
            ["Республика Бурятия"] = ParseCode("03"),
            ["Республика Алтай"] = ParseCode("04"),
            ["Республика Дагестан"] = ParseCode("05"),
            ["Республика Ингушетия"] = ParseCode("06"),
            ["Кабардино-Балкарская Республика"] = ParseCode("07"),
            ["Республика Калмыкия"] = ParseCode("08"),
            ["Карачаево-Черкесская Республика"] = ParseCode("09"),
            ["Республика Карелия"] = ParseCode("10"),
            ["Республика Коми"] = ParseCode("11"),
            ["Республика Марий Эл"] = ParseCode("12"),
            ["Республика Мордовия"] = ParseCode("13"),
            ["Республика Саха (Якутия)"] = ParseCode("14"),
            ["Республика Северная Осетия — Алания"] = ParseCode("15"),
            ["Республика Татарстан"] = ParseCode("16"),
            ["Республика Тыва"] = ParseCode("17"),
            ["Удмуртская Республика"] = ParseCode("18"),
            ["Республика Хакасия"] = ParseCode("19"),
            ["Чеченская Республика"] = ParseCode("20"),
            ["Чувашская Республика"] = ParseCode("21"),

            ["Алтайский край"] = ParseCode("22"),
            ["Краснодарский край"] = ParseCode("23"),
            ["Красноярский край"] = ParseCode("24"),
            ["Приморский край"] = ParseCode("25"),
            ["Ставропольский край"] = ParseCode("26"),
            ["Хабаровский край"] = ParseCode("27"),
            ["Амурская область"] = ParseCode("28"),
            ["Архангельская область"] = ParseCode("29"),
            ["Астраханская область"] = ParseCode("30"),
            ["Белгородская область"] = ParseCode("31"),
            ["Брянская область"] = ParseCode("32"),
            ["Владимирская область"] = ParseCode("33"),
            ["Волгоградская область"] = ParseCode("34"),
            ["Вологодская область"] = ParseCode("35"),
            ["Воронежская область"] = ParseCode("36"),
            ["Ивановская область"] = ParseCode("37"),
            ["Иркутская область"] = ParseCode("38"),
            ["Калининградская область"] = ParseCode("39"),
            ["Калужская область"] = ParseCode("40"),
            ["Камчатский край"] = ParseCode("41"),
            ["Кемеровская область"] = ParseCode("42"),
            ["Кировская область"] = ParseCode("43"),
            ["Костромская область"] = ParseCode("44"),
            ["Курганская область"] = ParseCode("45"),
            ["Курская область"] = ParseCode("46"),
            ["Ленинградская область"] = ParseCode("47"),
            ["Липецкая область"] = ParseCode("48"),
            ["Магаданская область"] = ParseCode("49"),
            ["Московская область"] = ParseCode("50"),
            ["Мурманская область"] = ParseCode("51"),
            ["Нижегородская область"] = ParseCode("52"),
            ["Новгородская область"] = ParseCode("53"),
            ["Новосибирская область"] = ParseCode("54"),
            ["Омская область"] = ParseCode("55"),
            ["Оренбургская область"] = ParseCode("56"),
            ["Орловская область"] = ParseCode("57"),
            ["Пензенская область"] = ParseCode("58"),
            ["Пермский край"] = ParseCode("59"),
            ["Псковская область"] = ParseCode("60"),
            ["Ростовская область"] = ParseCode("61"),
            ["Рязанская область"] = ParseCode("62"),
            ["Самарская область"] = ParseCode("63"),
            ["Саратовская область"] = ParseCode("64"),
            ["Сахалинская область"] = ParseCode("65"),
            ["Свердловская область"] = ParseCode("66"),
            ["Смоленская область"] = ParseCode("67"),
            ["Тамбовская область"] = ParseCode("68"),
            ["Тверская область"] = ParseCode("69"),
            ["Томская область"] = ParseCode("70"),
            ["Тульская область"] = ParseCode("71"),
            ["Тюменская область"] = ParseCode("72"),
            ["Ульяновская область"] = ParseCode("73"),
            ["Челябинская область"] = ParseCode("74"),
            ["Забайкальский край"] = ParseCode("75"),
            ["Ярославская область"] = ParseCode("76"),

            ["Москва"] = ParseCode("77"),
            ["Санкт-Петербург"] = ParseCode("78"),
            ["Еврейская автономная область"] = ParseCode("79"),

            ["Ненецкий автономный округ"] = ParseCode("83"),
            ["Ханты-Мансийский автономный округ — Югра"] = ParseCode("86"),
            ["Чукотский автономный округ"] = ParseCode("87"),
            ["Ямало-Ненецкий автономный округ"] = ParseCode("89"),

            // Новые субъекты в кодировке ФНС
            ["Запорожская область"] = ParseCode("90"),
            ["Республика Крым"] = ParseCode("91"),
            ["Севастополь"] = ParseCode("92"),
            ["Донецкая Народная Республика"] = ParseCode("93"),
            ["Луганская Народная Республика"] = ParseCode("94"),
            ["Херсонская область"] = ParseCode("95"),

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

    public static string? ResolveRegionName(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return null;
        }

        var code = MapRegionToCode(region);

        if (code.HasValue &&
            _regionsByCode.TryGetValue(code.Value, out var regionOption))
        {
            return regionOption.Name;
        }

        return region.Trim();
    }

    public static string ResolveRegionName(byte regionCode)
    {
        if (_regionsByCode.TryGetValue(regionCode, out var regionOption))
        {
            return regionOption.Name;
        }

        return regionCode.ToString(CultureInfo.InvariantCulture);
    }

    private readonly NoticeDbContext _dbContext;
    private readonly Okpd2CodeService _okpd2CodeService;

    public UserCompanyService(NoticeDbContext dbContext, Okpd2CodeService okpd2CodeService)
    {
        _dbContext = dbContext;
        _okpd2CodeService = okpd2CodeService;
    }

    public async Task<UserCompanyProfile> GetProfileAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Regions)
            .Include(u => u.Okpd2Codes)
            .ThenInclude(c => c.Okpd2Code)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException($"User with id '{userId}' was not found.");
        }

        var regionCodes = new HashSet<byte>(
            user.Regions
                .Select(r => r.Region));

        var orderedRegions = _availableRegions
            .Where(region => regionCodes.Contains(region.Code))
            .Select(region => region.Code)
            .ToArray();

        var okpd2Codes = user.Okpd2Codes
            .Select(c => c.Okpd2Code?.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code)
            .ToArray();

        return new UserCompanyProfile(user.CompanyInfo ?? string.Empty, orderedRegions, okpd2Codes);
    }

    public async Task<UserCompanyProfile> UpdateProfileAsync(
        string userId,
        string? companyInfo,
        IReadOnlyCollection<byte>? regions,
        IReadOnlyCollection<string>? okpd2Codes,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Regions)
            .Include(u => u.Okpd2Codes)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException($"User with id '{userId}' was not found.");
        }

        var trimmedInfo = (companyInfo ?? string.Empty).Trim();
        user.CompanyInfo = trimmedInfo;

        var normalizedInput = regions ?? Array.Empty<byte>();
        var selectedRegionCodes = new HashSet<byte>(
            normalizedInput.Where(r => _regionsByCode.ContainsKey(r)));

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

        var okpd2ByCode = await _okpd2CodeService.GetCodesByCodeAsync(cancellationToken);
        var normalizedOkpd2Codes = (okpd2Codes ?? Array.Empty<string>())
            .Select(code => code?.Trim())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .ToArray();

        var selectedOkpd2Codes = new List<Okpd2Code>();
        var seenCodes = new HashSet<int>();

        foreach (var code in normalizedOkpd2Codes)
        {
            if (okpd2ByCode.TryGetValue(code, out var okpd2) && seenCodes.Add(okpd2.Id))
            {
                selectedOkpd2Codes.Add(okpd2);
            }
        }

        _dbContext.ApplicationUserOkpd2Codes.RemoveRange(user.Okpd2Codes);
        user.Okpd2Codes.Clear();

        foreach (var okpd2 in selectedOkpd2Codes)
        {
            user.Okpd2Codes.Add(new ApplicationUserOkpd2Code
            {
                Okpd2CodeId = okpd2.Id,
                UserId = userId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserCompanyProfile(
            user.CompanyInfo ?? string.Empty,
            selectedRegions,
            selectedOkpd2Codes
                .Select(c => c.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .OrderBy(code => code)
                .ToArray());
    }

    public IReadOnlyCollection<RegionOption> GetAvailableRegions() => _availableRegions;

    public Task<IReadOnlyList<Okpd2Code>> GetAvailableOkpd2CodesAsync(CancellationToken cancellationToken) =>
        _okpd2CodeService.GetCodesAsync(cancellationToken);

    private static byte? MapRegionToCode(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return null;
        }

        var trimmed = region.Trim();

        if (byte.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric) &&
            _regionsByCode.ContainsKey(numeric))
        {
            return numeric;
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

    public sealed record UserCompanyProfile(
        string CompanyInfo,
        IReadOnlyCollection<byte> Regions,
        IReadOnlyCollection<string> Okpd2Codes);

    public sealed record RegionOption(byte Code, string Name);
}
