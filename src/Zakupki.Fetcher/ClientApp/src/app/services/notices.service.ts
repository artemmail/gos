import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { NoticeListResponse, NoticeQuery } from '../models/notice.models';

@Injectable({
  providedIn: 'root'
})
export class NoticesService {
  private readonly baseUrl = '/api/notices';

  constructor(private readonly http: HttpClient) {}

  getNotices(query: NoticeQuery): Observable<NoticeListResponse> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.search) {
      params = params.set('search', query.search);
    }

    if (query.sortField) {
      params = params.set('sortField', query.sortField);
    }

    if (query.sortDirection) {
      params = params.set('sortDirection', query.sortDirection);
    }

    return this.http.get<NoticeListResponse>(this.baseUrl, { params });
  }
}
