import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { finalize, shareReplay, tap } from 'rxjs/operators';

export interface RegionOption {
  code: string;
  name: string;
}

interface CachedRegions {
  timestamp: number;
  regions: RegionOption[];
}

@Injectable({ providedIn: 'root' })
export class RegionsService {
  private readonly apiUrl = '/api/regions';
  private readonly storageKey = 'regions-cache-v1';
  private readonly cacheTtlMs = 24 * 60 * 60 * 1000; // 24 hours

  private inFlightRequest$?: Observable<RegionOption[]>;
  private cachedRegions: RegionOption[] | null = null;
  private regionLabelsByCode: Record<string, string> = {};

  constructor(private readonly http: HttpClient) {
    const cached = this.tryReadFromStorage();
    if (cached) {
      this.setCache(cached);
    }
  }

  getRegions(): Observable<RegionOption[]> {
    if (this.cachedRegions) {
      return of(this.cachedRegions);
    }

    if (!this.inFlightRequest$) {
      this.inFlightRequest$ = this.http.get<RegionOption[]>(this.apiUrl).pipe(
        tap(regions => this.updateStorage(regions)),
        tap(regions => this.setCache(regions)),
        shareReplay(1),
        finalize(() => (this.inFlightRequest$ = undefined))
      );
    }

    return this.inFlightRequest$;
  }

  getRegionLabel(code: string | null | undefined): string | null {
    if (!code) {
      return null;
    }

    const normalized = code.trim().padStart(2, '0');
    return this.regionLabelsByCode[normalized] ?? null;
  }

  private tryReadFromStorage(): RegionOption[] | null {
    const raw = localStorage.getItem(this.storageKey);
    if (!raw) {
      return null;
    }

    try {
      const parsed: CachedRegions = JSON.parse(raw);
      if (!parsed?.regions || !Array.isArray(parsed.regions)) {
        return null;
      }

      if (!parsed.timestamp || Date.now() - parsed.timestamp > this.cacheTtlMs) {
        return null;
      }

      return parsed.regions;
    } catch {
      return null;
    }
  }

  private updateStorage(regions: RegionOption[]): void {
    const payload: CachedRegions = {
      timestamp: Date.now(),
      regions
    };

    try {
      localStorage.setItem(this.storageKey, JSON.stringify(payload));
    } catch {
      // Ignore write errors (e.g., storage quota exceeded).
    }
  }

  private setCache(regions: RegionOption[]): void {
    this.cachedRegions = regions;
    this.regionLabelsByCode = regions.reduce((acc, region) => {
      const normalizedCode = region.code.trim().padStart(2, '0');
      acc[normalizedCode] = acc[normalizedCode]
        ? `${acc[normalizedCode]} / ${region.name}`
        : region.name;
      return acc;
    }, {} as Record<string, string>);
  }
}
