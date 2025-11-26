import { Location } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute } from '@angular/router';
import { finalize, takeUntil } from 'rxjs/operators';
import { Subject } from 'rxjs';

import { NoticeCommonInfo, NoticeDetails } from '../models/notice.models';
import { RawJsonDialogComponent } from '../raw-json-dialog/raw-json-dialog.component';
import { RawJsonDialogData } from '../models/raw-json.models';
import { NoticesService } from '../services/notices.service';

@Component({
  selector: 'app-notice-details',
  templateUrl: './notice-details.component.html',
  styleUrls: ['./notice-details.component.css']
})
export class NoticeDetailsComponent implements OnInit, OnDestroy {
  noticeId = '';
  isLoading = false;
  errorMessage = '';
  parseError = '';
  details: NoticeDetails | null = null;
  parsedNotice: NoticeCommonInfo | null = null;
  rawJsonText = '';

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly noticesService: NoticesService,
    private readonly dialog: MatDialog,
    private readonly location: Location
  ) {}

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const noticeId = params.get('id');

        if (!noticeId) {
          this.errorMessage = 'Не указан идентификатор извещения.';
          this.details = null;
          this.parsedNotice = null;
          return;
        }

        this.noticeId = noticeId;
        this.loadNotice();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get title(): string {
    return (
      this.parsedNotice?.commonInfo?.purchaseObjectInfo ||
      this.details?.purchaseObjectInfo ||
      (this.details ? `Закупка ${this.details.purchaseNumber}` : 'Карточка закупки')
    );
  }

  loadNotice(): void {
    if (!this.noticeId) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';
    this.parseError = '';

    this.noticesService
      .getNotice(this.noticeId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        next: notice => {
          this.details = notice;
          this.rawJsonText = notice.rawJson ?? '';
          this.parsedNotice = this.parseNotice(this.rawJsonText);
        },
        error: () => {
          this.errorMessage = 'Не удалось загрузить данные извещения.';
          this.details = null;
          this.parsedNotice = null;
          this.rawJsonText = '';
        }
      });
  }

  openRawJson(): void {
    if (!this.rawJsonText) {
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

  goBack(): void {
    this.location.back();
  }

  private parseNotice(rawJson: string | null): NoticeCommonInfo | null {
    if (!rawJson) {
      this.parseError = 'Нет данных извещения.';
      return null;
    }

    try {
      return JSON.parse(rawJson) as NoticeCommonInfo;
    } catch {
      this.parseError = 'Не удалось разобрать данные извещения.';
      return null;
    }
  }
}
