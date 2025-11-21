import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { NoticeListResponse, NoticeQuery, NoticeVectorQuery } from '../models/notice.models';

@Injectable({
  providedIn: 'root'
})
export class NoticesService {
  private readonly baseUrl = '/api/notices';

  constructor(private readonly http: HttpClient) {}

  getNotices(query: NoticeQuery): Observable<NoticeListResponse> {
    let params = new HttpParams()
      .set('page', query.page.toString())
      .set('pageSize', query.pageSize.toString())
      .set('expiredOnly', query.expiredOnly ? 'true' : 'false');

    if (query.filterByUserRegions) {
      params = params.set('filterByUserRegions', 'true');
    }

    if (query.search) {
      params = params.set('search', query.search);
    }

    if (query.purchaseNumber) {
      params = params.set('purchaseNumber', query.purchaseNumber);
    }

    if (query.okpd2Codes) {
      params = params.set('okpd2Codes', query.okpd2Codes);
    }

    if (query.kvrCodes) {
      params = params.set('kvrCodes', query.kvrCodes);
    }

    if (query.sortField) {
      params = params.set('sortField', query.sortField);
    }

    if (query.sortDirection) {
      params = params.set('sortDirection', query.sortDirection);
    }

    return this.http.get<NoticeListResponse>(this.baseUrl, { params });
  }

  vectorSearch(query: NoticeVectorQuery): Observable<NoticeListResponse> {
    let params = new HttpParams()
      .set('page', query.page.toString())
      .set('pageSize', query.pageSize.toString())
      .set('queryVectorId', query.queryVectorId)
      .set('similarityThresholdPercent', query.similarityThresholdPercent.toString())
      .set('expiredOnly', query.expiredOnly ? 'true' : 'false')
      .set('collectingEndLimit', query.collectingEndLimit);

    if (query.filterByUserRegions) {
      params = params.set('filterByUserRegions', 'true');
    }

    return this.http.get<NoticeListResponse>(`${this.baseUrl}/vector-search`, { params });
  }
}
