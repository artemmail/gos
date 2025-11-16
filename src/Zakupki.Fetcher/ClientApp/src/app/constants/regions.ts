const LEGACY_NAME_TO_CODE: Record<string, string> = {
  'Республика Адыгея': '01',
  'Республика Башкортостан': '02',
  'Республика Бурятия': '03',
  'Республика Алтай': '04',
  'Республика Дагестан': '05',
  'Республика Ингушетия': '06',
  'Кабардино-Балкарская Республика': '07',
  'Республика Калмыкия': '08',
  'Карачаево-Черкесская Республика': '09',
  'Республика Карелия': '10',
  'Республика Коми': '11',
  'Республика Марий Эл': '12',
  'Республика Мордовия': '13',
  'Республика Саха (Якутия)': '14',
  'Республика Северная Осетия — Алания': '15',
  'Республика Татарстан': '16',
  'Республика Тыва': '17',
  'Удмуртская Республика': '18',
  'Республика Хакасия': '19',
  'Чеченская Республика': '20',
  'Чувашская Республика': '21',

  'Алтайский край': '22',
  'Краснодарский край': '23',
  'Красноярский край': '24',
  'Приморский край': '25',
  'Ставропольский край': '26',
  'Хабаровский край': '27',
  'Амурская область': '28',
  'Архангельская область': '29',
  'Астраханская область': '30',
  'Белгородская область': '31',
  'Брянская область': '32',
  'Владимирская область': '33',
  'Волгоградская область': '34',
  'Вологодская область': '35',
  'Воронежская область': '36',
  'Ивановская область': '37',
  'Иркутская область': '38',
  'Калининградская область': '39',
  'Калужская область': '40',
  'Камчатский край': '41',
  'Кемеровская область': '42',
  'Кировская область': '43',
  'Костромская область': '44',
  'Курганская область': '45',
  'Курская область': '46',
  'Ленинградская область': '47',
  'Липецкая область': '48',
  'Магаданская область': '49',
  'Московская область': '50',
  'Мурманская область': '51',
  'Нижегородская область': '52',
  'Новгородская область': '53',
  'Новосибирская область': '54',
  'Омская область': '55',
  'Оренбургская область': '56',
  'Орловская область': '57',
  'Пензенская область': '58',
  'Пермский край': '59',
  'Псковская область': '60',
  'Ростовская область': '61',
  'Рязанская область': '62',
  'Самарская область': '63',
  'Саратовская область': '64',
  'Сахалинская область': '65',
  'Свердловская область': '66',
  'Смоленская область': '67',
  'Тамбовская область': '68',
  'Тверская область': '69',
  'Томская область': '70',
  'Тульская область': '71',
  'Тюменская область': '72',
  'Ульяновская область': '73',
  'Челябинская область': '74',
  'Забайкальский край': '75',
  'Ярославская область': '76',

  'Москва': '77',
  'Санкт-Петербург': '78',
  'Еврейская автономная область': '79',

  'Ненецкий автономный округ': '83',
  'Ханты-Мансийский автономный округ — Югра': '86',
  'Чукотский автономный округ': '87',
  'Ямало-Ненецкий автономный округ': '89',

  'Запорожская область': '90',
  'Республика Крым': '91',
  'Севастополь': '92',
  'Донецкая Народная Республика': '93',
  'Луганская Народная Республика': '94',
  'Херсонская область': '95'
};

const REGION_LABELS_BY_CODE: Record<string, string> = Object.entries(LEGACY_NAME_TO_CODE).reduce(
  (acc, [name, code]) => {
    const normalizedCode = code.padStart(2, '0');
    acc[normalizedCode] = acc[normalizedCode] ? `${acc[normalizedCode]} / ${name}` : name;
    return acc;
  },
  {} as Record<string, string>
);

export function getRegionDisplayName(code: string | null | undefined): string | null {
  if (!code) {
    return null;
  }

  const normalizedCode = code.trim().padStart(2, '0');
  return REGION_LABELS_BY_CODE[normalizedCode] ?? null;
}

export function determineRegionFromRawJson(rawJson: string | null | undefined, fallbackRegion?: string | null): string | null {
  const normalizedFallback = normalizeRegionCode(fallbackRegion);

  if (!rawJson) {
    return normalizedFallback;
  }

  try {
    const notification = JSON.parse(rawJson);
    const detected = determineRegionFromNotification(notification);
    return detected ?? normalizedFallback;
  } catch {
    return normalizedFallback;
  }
}

function determineRegionFromNotification(notification: unknown): string | null {
  if (!notification || typeof notification !== 'object') {
    return null;
  }

  const candidates: Array<string | null> = [];

  const responsibleOrgInfo = (notification as any)?.purchaseResponsibleInfo?.responsibleOrgInfo;
  const specializedOrgInfo = (notification as any)?.purchaseResponsibleInfo?.specializedOrgInfo;
  candidates.push(getInnValue(responsibleOrgInfo));
  candidates.push(getInnValue(specializedOrgInfo));

  const requirements = (notification as any)?.notificationInfo?.customerRequirementsInfo?.items;
  if (Array.isArray(requirements)) {
    for (const requirement of requirements) {
      const applicationInn = getInnValue(requirement?.applicationGuarantee?.accountBudget?.accountBudgetAdmin);
      if (applicationInn) {
        candidates.push(applicationInn);
      }

      const contractInn = getInnValue(requirement?.contractGuarantee?.accountBudget?.accountBudgetAdmin);
      if (contractInn) {
        candidates.push(contractInn);
      }
    }
  }

  for (const inn of candidates) {
    const region = extractRegionFromInn(inn);
    if (region) {
      return region;
    }
  }

  return null;
}

function getInnValue(source: any): string | null {
  if (!source || typeof source !== 'object') {
    return null;
  }

  const innValue = source.inn ?? source.INN ?? source.Inn;
  if (typeof innValue !== 'string') {
    return null;
  }

  const trimmed = innValue.trim();
  return trimmed.length > 0 ? trimmed : null;
}

function extractRegionFromInn(inn: string | null): string | null {
  if (!inn) {
    return null;
  }

  const digits = inn.replace(/\D+/g, '');
  if (digits.length < 2) {
    return null;
  }

  return digits.substring(0, 2).padStart(2, '0');
}

function normalizeRegionCode(region: string | null | undefined): string | null {
  if (typeof region !== 'string') {
    return null;
  }

  const trimmed = region.trim();
  return trimmed ? trimmed.padStart(2, '0') : null;
}
