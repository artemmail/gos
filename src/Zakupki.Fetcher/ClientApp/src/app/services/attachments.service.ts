import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AttachmentDownloadResult,
  AttachmentMarkdownConversionResult,
  NoticeAttachment
} from '../models/attachment.models';

@Injectable({
  providedIn: 'root'
})
export class AttachmentsService {
  private readonly baseUrl = '/api/notices';

  constructor(private readonly http: HttpClient) {}

  getAttachments(noticeId: string): Observable<NoticeAttachment[]> {
    return this.http.get<NoticeAttachment[]>(`${this.baseUrl}/${noticeId}/attachments`);
  }

  downloadAttachment(attachmentId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/attachments/${attachmentId}/content`, {
      responseType: 'blob'
    });
  }

  downloadMissing(noticeId: string): Observable<AttachmentDownloadResult> {
    return this.http.post<AttachmentDownloadResult>(`${this.baseUrl}/${noticeId}/attachments/download-missing`, {});
  }

  convertAllToMarkdown(noticeId: string): Observable<AttachmentMarkdownConversionResult> {
    return this.http.post<AttachmentMarkdownConversionResult>(
      `${this.baseUrl}/${noticeId}/attachments/convert-to-markdown`,
      {}
    );
  }

  downloadMarkdown(attachmentId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/attachments/${attachmentId}/markdown`, {
      responseType: 'blob'
    });
  }
}
