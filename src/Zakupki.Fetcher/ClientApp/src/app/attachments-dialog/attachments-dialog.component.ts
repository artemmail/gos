import { Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import {
  AttachmentDialogData,
  AttachmentDownloadResult,
  NoticeAttachment
} from '../models/attachment.models';
import { AttachmentsService } from '../services/attachments.service';

@Component({
  selector: 'app-attachments-dialog',
  templateUrl: './attachments-dialog.component.html',
  styleUrls: ['./attachments-dialog.component.css']
})
export class AttachmentsDialogComponent implements OnInit, OnDestroy {
  displayedColumns: string[] = ['fileName', 'documentDate', 'status', 'actions'];
  attachments: NoticeAttachment[] = [];
  isLoading = false;
  isDownloadingAll = false;
  downloadingAttachmentId: string | null = null;
  errorMessage = '';
  infoMessage = '';

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly attachmentsService: AttachmentsService,
    private readonly dialogRef: MatDialogRef<AttachmentsDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public readonly data: AttachmentDialogData
  ) {}

  ngOnInit(): void {
    this.loadAttachments();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  close(): void {
    this.dialogRef.close();
  }

  loadAttachments(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.infoMessage = '';

    this.attachmentsService
      .getAttachments(this.data.noticeId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        next: attachments => {
          this.attachments = attachments;
        },
        error: () => {
          this.errorMessage = 'Не удалось загрузить список вложений.';
        }
      });
  }

  downloadAttachment(attachment: NoticeAttachment): void {
    if (!attachment.hasBinaryContent) {
      this.errorMessage = 'Файл отсутствует в базе данных. Используйте кнопку «Скачать недостающие».';
      return;
    }

    this.downloadingAttachmentId = attachment.id;
    this.errorMessage = '';
    this.infoMessage = '';

    this.attachmentsService
      .downloadAttachment(attachment.id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.downloadingAttachmentId = null))
      )
      .subscribe({
        next: blob => {
          this.saveFile(blob, attachment.fileName);
          this.infoMessage = `Скачивание файла «${attachment.fileName}» началось.`;
        },
        error: error => {
          const message = error?.error?.message || 'Не удалось скачать вложение.';
          this.errorMessage = message;
        }
      });
  }

  downloadMissing(): void {
    this.isDownloadingAll = true;
    this.errorMessage = '';
    this.infoMessage = '';

    this.attachmentsService
      .downloadMissing(this.data.noticeId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isDownloadingAll = false))
      )
      .subscribe({
        next: (result: AttachmentDownloadResult) => {
          this.infoMessage = `Загружено ${result.downloaded} из ${result.total}. Ошибок: ${result.failed}.`;
          this.loadAttachments();
        },
        error: error => {
          const message = error?.error?.message || 'Не удалось скачать вложения.';
          this.errorMessage = message;
        }
      });
  }

  get hasAttachments(): boolean {
    return this.attachments.length > 0;
  }

  get hasMissingAttachments(): boolean {
    return this.attachments.some(attachment => !attachment.hasBinaryContent);
  }

  private saveFile(blob: Blob, fileName?: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName && fileName.trim().length > 0 ? fileName : 'attachment';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }
}
