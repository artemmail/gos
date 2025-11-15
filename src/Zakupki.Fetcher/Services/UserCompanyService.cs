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
    private static readonly string[] _availableRegions = new[]
    {
        "Республика Адыгея",
        "Республика Алтай",
        "Республика Башкортостан",
        "Республика Бурятия",
        "Республика Дагестан",
        "Республика Ингушетия",
        "Кабардино-Балкарская Республика",
        "Республика Калмыкия",
        "Карачаево-Черкесская Республика",
        "Республика Карелия",
        "Республика Коми",
        "Республика Марий Эл",
        "Республика Мордовия",
        "Республика Саха (Якутия)",
        "Республика Северная Осетия — Алания",
        "Республика Татарстан",
        "Республика Тыва",
        "Удмуртская Республика",
        "Республика Хакасия",
        "Чувашская Республика",
        "Алтайский край",
        "Забайкальский край",
        "Камчатский край",
        "Краснодарский край",
        "Красноярский край",
        "Пермский край",
        "Приморский край",
        "Ставропольский край",
        "Хабаровский край",
        "Амурская область",
        "Архангельская область",
        "Астраханская область",
        "Белгородская область",
        "Брянская область",
        "Владимирская область",
        "Волгоградская область",
        "Вологодская область",
        "Воронежская область",
        "Ивановская область",
        "Иркутская область",
        "Калининградская область",
        "Калужская область",
        "Кемеровская область",
        "Кировская область",
        "Костромская область",
        "Курганская область",
        "Курская область",
        "Ленинградская область",
        "Липецкая область",
        "Магаданская область",
        "Московская область",
        "Мурманская область",
        "Нижегородская область",
        "Новгородская область",
        "Новосибирская область",
        "Омская область",
        "Оренбургская область",
        "Орловская область",
        "Пензенская область",
        "Псковская область",
        "Ростовская область",
        "Рязанская область",
        "Самарская область",
        "Саратовская область",
        "Сахалинская область",
        "Свердловская область",
        "Смоленская область",
        "Тамбовская область",
        "Тверская область",
        "Томская область",
        "Тульская область",
        "Тюменская область",
        "Ульяновская область",
        "Челябинская область",
        "Ярославская область",
        "Москва",
        "Санкт-Петербург",
        "Севастополь",
        "Еврейская автономная область",
        "Ненецкий автономный округ",
        "Ханты-Мансийский автономный округ — Югра",
        "Чукотский автономный округ",
        "Ямало-Ненецкий автономный округ"
    };

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

        var regionSet = new HashSet<string>(user.Regions.Select(r => r.Region), StringComparer.OrdinalIgnoreCase);
        var orderedRegions = _availableRegions.Where(regionSet.Contains).ToArray();

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
        var normalizedSet = new HashSet<string>(
            normalizedInput
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var selectedRegions = _availableRegions
            .Where(region => normalizedSet.Contains(region))
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

    public IReadOnlyCollection<string> GetAvailableRegions() => _availableRegions;

    public sealed record UserCompanyProfile(string CompanyInfo, IReadOnlyCollection<string> Regions);
}
