import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { FabNoticeListResponse, FabNoticeQuery, FabrikantProcedure } from '../models/fab-notice.models';

@Injectable({ providedIn: 'root' })
export class FabNoticesService {
  private readonly baseUrl = '/api/notices/fab';

  constructor(private readonly http: HttpClient) { }

  getNotices(query: FabNoticeQuery): Observable<FabNoticeListResponse> {
    let params = new HttpParams()
      .set('page', query.page.toString())
      .set('pageSize', query.pageSize.toString());

    if (query.search) {
      params = params.set('search', query.search);
    }

    if (query.sortField) {
      params = params.set('sortField', query.sortField);
    }

    if (query.sortDirection) {
      params = params.set('sortDirection', query.sortDirection);
    }

    return this.http.get<FabNoticeListResponse>(this.baseUrl, { params });
  }

  getNotice(procedureNumber: string): Observable<FabrikantProcedure> {
    return this.http.get<FabrikantProcedure>(`${this.baseUrl}/${encodeURIComponent(procedureNumber)}`);
  }
}
