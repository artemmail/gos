import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface RegionOption {
  code: string;
  name: string;
}

export interface UserCompanyProfile {
  companyInfo: string;
  regions: string[];
  availableRegions: RegionOption[];
}

export interface UpdateUserCompanyProfileRequest {
  companyInfo: string;
  regions: string[];
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
