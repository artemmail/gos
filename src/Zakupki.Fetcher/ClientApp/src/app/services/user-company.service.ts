import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface RegionOption {
  code: number;
  name: string;
}

export interface UserCompanyProfile {
  companyInfo: string;
  regions: number[];
  availableRegions: RegionOption[];
}

export interface UpdateUserCompanyProfileRequest {
  companyInfo: string;
  regions: number[];
}

@Injectable({ providedIn: 'root' })
export class UserCompanyService {
  private readonly apiUrl = '/api/user-company';

  constructor(private readonly http: HttpClient) {}

  getProfile(): Observable<UserCompanyProfile> {
    return this.http.get<UserCompanyProfile>(this.apiUrl);
  }

  updateProfile(request: UpdateUserCompanyProfileRequest): Observable<UserCompanyProfile> {
    return this.http.put<UserCompanyProfile>(this.apiUrl, request);
  }
}
