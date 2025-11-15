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
  isConvertingAll = false;
  downloadingAttachmentId: string | null = null;
  downloadingMarkdownAttachmentId: string | null = null;
  errorMessage = '';
  infoMessage = '';

  private readonly supportedExtensions = ['.doc', '.docx', '.pdf', '.html', '.htm'];

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

  loadAttachments(preserveInfo = false): void {
    this.isLoading = true;
    this.errorMessage = '';
    if (!preserveInfo) {
      this.infoMessage = '';
    }

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
          this.loadAttachments(true);
        },
        error: error => {
          const message = error?.error?.message || 'Не удалось скачать вложения.';
          this.errorMessage = message;
        }
      });
  }

  convertAllToMarkdown(): void {
    this.isConvertingAll = true;
    this.errorMessage = '';
    this.infoMessage = '';

    this.attachmentsService
      .convertAllToMarkdown(this.data.noticeId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isConvertingAll = false))
      )
      .subscribe({
        next: result => {
          const skipped = result.missingContent + result.unsupported;
          this.infoMessage = `Конвертация завершена. Успешно: ${result.converted} из ${result.total}. Пропущено: ${skipped}. Ошибок: ${result.failed}.`;
          this.loadAttachments(true);
        },
        error: error => {
          const message = error?.error?.message || 'Не удалось выполнить конвертацию файлов.';
          this.errorMessage = message;
        }
      });
  }

  downloadMarkdown(attachment: NoticeAttachment): void {
    if (!attachment.hasMarkdownContent) {
      this.errorMessage = 'Для этого вложения отсутствует Markdown-версия. Сначала выполните конвертацию.';
      return;
    }

    this.downloadingMarkdownAttachmentId = attachment.id;
    this.errorMessage = '';
    this.infoMessage = '';

    this.attachmentsService
      .downloadMarkdown(attachment.id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.downloadingMarkdownAttachmentId = null))
      )
      .subscribe({
        next: blob => {
          this.saveFile(blob, this.getMarkdownFileName(attachment.fileName));
          this.infoMessage = `Скачивание Markdown-версии «${attachment.fileName}» началось.`;
        },
        error: error => {
          const message = error?.error?.message || 'Не удалось скачать Markdown-версию вложения.';
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

  get hasConvertibleAttachments(): boolean {
    return this.attachments.some(attachment => this.canConvertAttachment(attachment));
  }

  canConvertAttachment(attachment: NoticeAttachment): boolean {
    return (
      attachment.hasBinaryContent &&
      this.isSupportedExtension(attachment.fileName)
    );
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

  private isSupportedExtension(fileName?: string | null): boolean {
    if (!fileName) {
      return false;
    }

    const extension = this.getExtension(fileName);
    return extension.length > 0 && this.supportedExtensions.includes(extension);
  }

  private getExtension(fileName: string): string {
    const dotIndex = fileName.lastIndexOf('.');
    if (dotIndex === -1) {
      return '';
    }

    return fileName.substring(dotIndex).toLowerCase();
  }

  private getMarkdownFileName(fileName?: string | null): string {
    if (!fileName || fileName.trim().length === 0) {
      return 'attachment.md';
    }

    const baseName = fileName.replace(/\.[^/.]+$/, '');
    return `${baseName}.md`;
  }
}
