import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateUserQueryVectorRequest,
  UserQueryVectorDto
} from '../models/query-vector.models';

@Injectable({ providedIn: 'root' })
export class QueryVectorService {
  private readonly baseUrl = '/api/queryvectors';

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<UserQueryVectorDto[]> {
    return this.http.get<UserQueryVectorDto[]>(this.baseUrl);
  }

  create(payload: CreateUserQueryVectorRequest): Observable<UserQueryVectorDto> {
    return this.http.post<UserQueryVectorDto>(this.baseUrl, payload);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
