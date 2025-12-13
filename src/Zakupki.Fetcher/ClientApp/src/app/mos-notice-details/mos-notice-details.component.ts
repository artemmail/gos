import { Location } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import { MosNoticeDetails } from '../models/mos-notice.models';
import { MosNoticesService } from '../services/mos-notices.service';
import { UndocumentedAuctionDto } from '../models/undocumented-auction.models';
import { RawJsonDialogData } from '../models/raw-json.models';
import { RawJsonDialogComponent } from '../raw-json-dialog/raw-json-dialog.component';
import { AttachmentsDialogComponent } from '../attachments-dialog/attachments-dialog.component';
import { AttachmentDialogData } from '../models/attachment.models';

@Component({
  selector: 'app-mos-notice-details',
  templateUrl: './mos-notice-details.component.html',
  styleUrls: ['./mos-notice-details.component.css']
})
export class MosNoticeDetailsComponent implements OnInit, OnDestroy {
  noticeId = '';
  purchaseNumber = '';
  details: MosNoticeDetails | null = null;
  auctionDetails: UndocumentedAuctionDto | null = null;
  formattedJson = '';
  rawJsonText = '';
  parseError = '';
  isLoading = false;
  errorMessage = '';
  private readonly mosDateTimePattern = /^(\d{2})\.(\d{2})\.(\d{4}) (\d{2}):(\d{2}):(\d{2})$/;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly mosNoticesService: MosNoticesService,
    private readonly location: Location,
    private readonly dialog: MatDialog
  ) { }

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const purchaseNumber = params.get('purchaseNumber');

        if (!purchaseNumber) {
          this.errorMessage = 'Не указан номер извещения.';
          this.details = null;
          this.formattedJson = '';
          return;
        }

        this.purchaseNumber = purchaseNumber;
        this.loadNotice();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get title(): string {
    if (this.auctionDetails?.name) {
      return this.auctionDetails.name;
    }

    if (this.details) {
      return `Закупка ${this.details.purchaseNumber}`;
    }

    return 'Закупка mos.ru';
  }

  loadNotice(): void {
    if (!this.purchaseNumber) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';
    this.parseError = '';
    this.auctionDetails = null;
    this.noticeId = '';
    this.formattedJson = '';
    this.rawJsonText = '';

    this.mosNoticesService
      .getNotice(this.purchaseNumber)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        next: response => {
          this.details = response;
          this.noticeId = response.id;
          this.rawJsonText = response.rawJson ?? '';
          this.auctionDetails = this.parseDetails(response);
          this.formattedJson = this.formatJson(response);
        },
        error: () => {
          this.errorMessage = 'Не удалось загрузить данные извещения.';
          this.details = null;
          this.noticeId = '';
          this.formattedJson = '';
          this.rawJsonText = '';
        }
      });
  }

  goBack(): void {
    this.location.back();
  }

  parseDateTime(dateTime: string | null | undefined): Date | null {
    if (!dateTime) {
      return null;
    }

    const nativeDate = new Date(dateTime);
    if (!Number.isNaN(nativeDate.getTime())) {
      return nativeDate;
    }

    const matches = this.mosDateTimePattern.exec(dateTime);
    if (!matches) {
      return null;
    }

    const [, day, month, year, hours, minutes, seconds] = matches;
    const parsed = new Date(
      Number(year),
      Number(month) - 1,
      Number(day),
      Number(hours),
      Number(minutes),
      Number(seconds)
    );

    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  private parseDetails(details: MosNoticeDetails): UndocumentedAuctionDto | null {
    if (details.details) {
      return details.details;
    }

    if (details.rawJson) {
      try {
        return JSON.parse(details.rawJson) as UndocumentedAuctionDto;
      } catch {
        this.parseError = 'Не удалось разобрать JSON. Показан исходный текст.';
        return null;
      }
    }

    this.parseError = 'Нет данных извещения.';
    return null;
  }

  private formatJson(details: MosNoticeDetails): string {
    if (details.details) {
      return JSON.stringify(details.details, null, 2);
    }

    if (details.rawJson) {
      try {
        return JSON.stringify(JSON.parse(details.rawJson), null, 2);
      } catch {
        this.parseError = 'Не удалось разобрать JSON. Показан исходный текст.';
        return details.rawJson;
      }
    }

    this.parseError = 'Нет данных извещения.';
    return '';
  }

  getFileDownloadUrl(fileId: number | null | undefined): string | null {
    if (fileId == null) {
      return null;
    }

    const encodedId = encodeURIComponent(fileId.toString());
    return `https://zakupki.mos.ru/newapi/api/FileStorage/Download?id=${encodedId}`;
  }

  openRawJson(): void {
    if (!this.rawJsonText || !this.details) {
      return;
    }

    const data: RawJsonDialogData = {
      purchaseNumber: this.details.purchaseNumber,
      title: this.title,
      rawJson: this.rawJsonText
    };

    this.dialog.open(RawJsonDialogComponent, {
      width: '800px',
      maxWidth: '95vw',
      data
    });
  }

  openAttachments(): void {
    if (!this.noticeId || !this.details) {
      return;
    }

    const data: AttachmentDialogData = {
      noticeId: this.noticeId,
      purchaseNumber: this.details.purchaseNumber,
      title: this.title
    };

    this.dialog.open(AttachmentsDialogComponent, {
      width: '900px',
      data
    });
  }
}
