import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { FavoriteSearchEnqueueRequest } from '../models/favorite-search.models';

@Injectable({
  providedIn: 'root'
})
export class FavoriteSearchService {
  private readonly baseUrl = '/api/notices/favorite-search';

  constructor(private readonly http: HttpClient) {}

  enqueue(request: FavoriteSearchEnqueueRequest): Observable<void> {
    return this.http.post<void>(this.baseUrl, request);
  }
}
