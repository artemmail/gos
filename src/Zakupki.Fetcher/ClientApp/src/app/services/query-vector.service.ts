import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import {
  CreateUserQueryVectorRequest,
  UserQueryVectorDto
} from '../models/query-vector.models';

@Injectable({ providedIn: 'root' })
export class QueryVectorService {
  private readonly baseUrl = '/api/queryvectors';
  private readonly queryVectorsSubject = new BehaviorSubject<UserQueryVectorDto[]>([]);

  readonly queryVectors$ = this.queryVectorsSubject.asObservable();

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<UserQueryVectorDto[]> {
    return this.http.get<UserQueryVectorDto[]>(this.baseUrl).pipe(
      tap(vectors => this.queryVectorsSubject.next(vectors))
    );
  }

  create(payload: CreateUserQueryVectorRequest): Observable<UserQueryVectorDto> {
    return this.http.post<UserQueryVectorDto>(this.baseUrl, payload).pipe(
      tap(createdVector => {
        const current = this.queryVectorsSubject.value;
        this.queryVectorsSubject.next([...current, createdVector]);
      })
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      tap(() => {
        const updated = this.queryVectorsSubject.value.filter(vector => vector.id !== id);
        this.queryVectorsSubject.next(updated);
      })
    );
  }
}
