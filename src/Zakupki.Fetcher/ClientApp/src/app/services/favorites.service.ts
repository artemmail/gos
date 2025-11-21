import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { NoticeListResponse, NoticeQuery } from '../models/notice.models';

@Injectable({
  providedIn: 'root'
})
export class FavoritesService {
  private readonly baseUrl = '/api/notices';

  constructor(private readonly http: HttpClient) {}

  getFavorites(query: NoticeQuery): Observable<NoticeListResponse> {
    return this.http.get<NoticeListResponse>(`${this.baseUrl}/favorites`, {
      params: this.buildQueryParams(query)
    });
  }

  addFavorite(noticeId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${noticeId}/favorite`, {});
  }

  removeFavorite(noticeId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${noticeId}/favorite`);
  }

  private buildQueryParams(query: NoticeQuery): HttpParams {
    let params = new HttpParams()
      .set('page', query.page.toString())
      .set('pageSize', query.pageSize.toString())
      .set('expiredOnly', query.expiredOnly ? 'true' : 'false');

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

    return params;
  }
}
