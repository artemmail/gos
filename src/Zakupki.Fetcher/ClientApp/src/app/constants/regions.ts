export function determineRegionFromRawJson(
  rawJson: string | null | undefined,
  fallbackRegion?: string | null
): string | null {
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
