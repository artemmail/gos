import { Location } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import { FabNoticesService } from '../services/fab-notices.service';
import { FabrikantProcedure } from '../models/fab-notice.models';
import { RawJsonDialogComponent } from '../raw-json-dialog/raw-json-dialog.component';
import { RawJsonDialogData } from '../models/raw-json.models';
import { AttachmentsDialogComponent } from '../attachments-dialog/attachments-dialog.component';
import { AttachmentDialogData } from '../models/attachment.models';

@Component({
  selector: 'app-fab-notice-details',
  templateUrl: './fab-notice-details.component.html',
  styleUrls: ['./fab-notice-details.component.css']
})
export class FabNoticeDetailsComponent implements OnInit, OnDestroy {
  procedureNumber = '';
  details: FabrikantProcedure | null = null;
  isLoading = false;
  errorMessage = '';

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly fabNoticesService: FabNoticesService,
    private readonly location: Location,
    private readonly dialog: MatDialog
  ) { }

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const procedureNumber = params.get('purchaseNumber');

        if (!procedureNumber) {
          this.errorMessage = 'Не указан номер извещения.';
          this.details = null;
          return;
        }

        this.procedureNumber = procedureNumber;
        this.loadNotice();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get title(): string {
    if (this.details?.title) {
      return this.details.title;
    }

    if (this.details) {
      return `Закупка ${this.details.procedureNumber}`;
    }

    return 'Закупка Fabrikant';
  }

  loadNotice(): void {
    if (!this.procedureNumber) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';
    this.details = null;

    this.fabNoticesService
      .getNotice(this.procedureNumber)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        next: response => {
          this.details = response;
        },
        error: () => {
          this.errorMessage = 'Не удалось загрузить данные извещения Fabrikant.';
          this.details = null;
        }
      });
  }

  get fabrikLink(): string | null {
    if (!this.details?.externalId) {
      return null;
    }

    return `https://www.fabrikant.ru/v2/trades/procedure/view/${this.details.externalId}`;
  }

  goBack(): void {
    this.location.back();
  }

  openRawJson(): void {
    if (!this.details?.rawJson) {
      return;
    }

    const data: RawJsonDialogData = {
      purchaseNumber: this.details.procedureNumber || this.procedureNumber,
      title: this.title,
      rawJson: this.details.rawJson
    };

    this.dialog.open(RawJsonDialogComponent, {
      width: '800px',
      maxWidth: '95vw',
      data
    });
  }

  openAttachments(): void {
    if (!this.details?.noticeId) {
      return;
    }

    const data: AttachmentDialogData = {
      noticeId: this.details.noticeId,
      purchaseNumber: this.details.procedureNumber || this.procedureNumber,
      title: this.title
    };

    this.dialog.open(AttachmentsDialogComponent, {
      width: '900px',
      data
    });
  }
}
