import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { MosNoticeDetails, MosNoticeListResponse, MosNoticeQuery } from '../models/mos-notice.models';

@Injectable({ providedIn: 'root' })
export class MosNoticesService {
  private readonly baseUrl = '/api/notices/mos';

  constructor(private readonly http: HttpClient) { }

  getNotices(query: MosNoticeQuery): Observable<MosNoticeListResponse> {
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

    return this.http.get<MosNoticeListResponse>(this.baseUrl, { params });
  }

  getNotice(purchaseNumber: string): Observable<MosNoticeDetails> {
    return this.http.get<MosNoticeDetails>(`${this.baseUrl}/${encodeURIComponent(purchaseNumber)}`);
  }
}
