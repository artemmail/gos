import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { TenderAnalysisResult } from '../models/notice-analysis.models';

export type NoticeAnalysisStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Failed';

export interface NoticeAnalysisResponse {
  noticeId: string;
  status: NoticeAnalysisStatus | null;
  hasAnswer: boolean;
  result?: string | null;
  error?: string | null;
  updatedAt: string | null;
  completedAt?: string | null;
  prompt?: string | null;
  structuredResult?: TenderAnalysisResult | null;
  decisionScore?: number | null;
  recommended?: boolean | null;
}

@Injectable({ providedIn: 'root' })
export class NoticeAnalysisService {
  private readonly baseUrl = '/api/notices';

  constructor(private readonly http: HttpClient) {}

  analyze(noticeId: string, force = false): Observable<NoticeAnalysisResponse> {
    let params = new HttpParams();

    if (force) {
      params = params.set('force', 'true');
    }

    return this.http.post<NoticeAnalysisResponse>(`${this.baseUrl}/${noticeId}/analysis`, {}, { params });
  }

  getStatus(noticeId: string): Observable<NoticeAnalysisResponse> {
    return this.http.get<NoticeAnalysisResponse>(`${this.baseUrl}/${noticeId}/analysis`);
  }
}
