import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { AttachmentDownloadResult, NoticeAttachment } from '../models/attachment.models';

@Injectable({
  providedIn: 'root'
})
export class AttachmentsService {
  private readonly baseUrl = '/api/notices';

  constructor(private readonly http: HttpClient) {}

  getAttachments(noticeId: string): Observable<NoticeAttachment[]> {
    return this.http.get<NoticeAttachment[]>(`${this.baseUrl}/${noticeId}/attachments`);
  }

  downloadAttachment(attachmentId: string): Observable<NoticeAttachment> {
    return this.http.post<NoticeAttachment>(`${this.baseUrl}/attachments/${attachmentId}/download`, {});
  }

  downloadMissing(noticeId: string): Observable<AttachmentDownloadResult> {
    return this.http.post<AttachmentDownloadResult>(`${this.baseUrl}/${noticeId}/attachments/download-missing`, {});
  }
}
