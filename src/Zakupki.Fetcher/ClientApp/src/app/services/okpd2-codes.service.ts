import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { finalize, shareReplay, tap } from 'rxjs/operators';

export interface Okpd2CodeOption {
  code: string;
  name: string;
}

interface CachedOkpd2Codes {
  timestamp: number;
  codes: Okpd2CodeOption[];
}

@Injectable({ providedIn: 'root' })
export class Okpd2CodesService {
  private readonly apiUrl = '/api/okpd2-codes';
  private readonly storageKey = 'okpd2-codes-cache-v1';
  private readonly cacheTtlMs = 24 * 60 * 60 * 1000; // 24 hours

  private inFlightRequest$?: Observable<Okpd2CodeOption[]>;
  private cachedCodes: Okpd2CodeOption[] | null = null;
  private codeLabels: Record<string, string> = {};

  constructor(private readonly http: HttpClient) {
    const cached = this.tryReadFromStorage();
    if (cached) {
      this.setCache(cached);
    }
  }

  getCodes(): Observable<Okpd2CodeOption[]> {
    if (this.cachedCodes) {
      return of(this.cachedCodes);
    }

    if (!this.inFlightRequest$) {
      this.inFlightRequest$ = this.http.get<Okpd2CodeOption[]>(this.apiUrl).pipe(
        tap(codes => this.updateStorage(codes)),
        tap(codes => this.setCache(codes)),
        shareReplay(1),
        finalize(() => (this.inFlightRequest$ = undefined))
      );
    }

    return this.inFlightRequest$;
  }

  getLabel(code: string | null | undefined): string | null {
    if (!code) {
      return null;
    }

    return this.codeLabels[code.trim().toLowerCase()] ?? null;
  }

  private tryReadFromStorage(): Okpd2CodeOption[] | null {
    const raw = localStorage.getItem(this.storageKey);
    if (!raw) {
      return null;
    }

    try {
      const parsed: CachedOkpd2Codes = JSON.parse(raw);
      if (!parsed?.codes || !Array.isArray(parsed.codes)) {
        return null;
      }

      if (!parsed.timestamp || Date.now() - parsed.timestamp > this.cacheTtlMs) {
        return null;
      }

      return parsed.codes;
    } catch {
      return null;
    }
  }

  private updateStorage(codes: Okpd2CodeOption[]): void {
    const payload: CachedOkpd2Codes = {
      timestamp: Date.now(),
      codes
    };

    try {
      localStorage.setItem(this.storageKey, JSON.stringify(payload));
    } catch {
      // Ignore storage errors.
    }
  }

  private setCache(codes: Okpd2CodeOption[]): void {
    this.cachedCodes = codes;
    this.codeLabels = codes.reduce((acc, code) => {
      acc[code.code.trim().toLowerCase()] = code.name;
      return acc;
    }, {} as Record<string, string>);
  }
}
