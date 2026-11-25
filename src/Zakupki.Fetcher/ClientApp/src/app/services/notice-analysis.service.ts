import { Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
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

  describeAnalysisError(error: string | null | undefined): string {
    const message = error?.trim();
    if (!message) return 'Не удалось завершить анализ. Попробуйте ещё раз.';
    if (message.includes('скачать документы') || message.includes('подготовить вложения')) {
      return 'Не удалось загрузить документы закупки с ЕИС. Откройте «Документы», нажмите «Загрузить документы» и повторите анализ.';
    }
    if (message.includes('преобразовать документы') || message.includes('конвертацию')) {
      return 'Документы скачаны, но их не удалось преобразовать в текст. Откройте «Документы», выполните конвертацию и повторите анализ.';
    }
    if (/timeout|timed out|истекло время ожидания/i.test(message)) {
      return 'Сервис анализа отвечает слишком долго. Повторите попытку позже.';
    }
    if (/SSL connection|unexpected EOF|certificate chain|TLS handshake/i.test(message)) {
      return 'Не удалось установить защищённое соединение с сервисом ИИ. Повторите анализ через несколько минут.';
    }
    if (message.includes('Сервис анализа вернул ошибку')) {
      return 'Сервис ИИ временно недоступен. Повторите попытку позже или обратитесь к администратору.';
    }
    return message;
  }

  describeRequestError(error: any): string {
    const message = error?.error?.message;
    if (typeof message === 'string' && message.trim()) return this.describeAnalysisError(message);
    if (error?.status === 0) return 'Нет связи с сервером. Проверьте подключение и повторите попытку.';
    if (error?.status === 401) return 'Сессия закончилась. Войдите снова и повторите анализ.';
    return 'Не удалось запустить анализ. Повторите попытку позже.';
  }

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

  downloadReport(noticeId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.baseUrl}/${noticeId}/analysis/report`, {
      responseType: 'blob',
      observe: 'response'
    });
  }
}
