import { Location } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import { MosNoticeDetails } from '../models/mos-notice.models';
import { MosNoticesService } from '../services/mos-notices.service';

@Component({
  selector: 'app-mos-notice-details',
  templateUrl: './mos-notice-details.component.html',
  styleUrls: ['./mos-notice-details.component.css']
})
export class MosNoticeDetailsComponent implements OnInit, OnDestroy {
  purchaseNumber = '';
  details: MosNoticeDetails | null = null;
  formattedJson = '';
  parseError = '';
  isLoading = false;
  errorMessage = '';

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly mosNoticesService: MosNoticesService,
    private readonly location: Location
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
    return this.details?.details?.name || (this.details ? `Закупка ${this.details.purchaseNumber}` : 'Закупка mos.ru');
  }

  loadNotice(): void {
    if (!this.purchaseNumber) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';
    this.parseError = '';
    this.formattedJson = '';

    this.mosNoticesService
      .getNotice(this.purchaseNumber)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        next: response => {
          this.details = response;
          this.formattedJson = this.formatJson(response);
        },
        error: () => {
          this.errorMessage = 'Не удалось загрузить данные извещения.';
          this.details = null;
          this.formattedJson = '';
        }
      });
  }

  goBack(): void {
    this.location.back();
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
}
