import { AfterViewInit, Component, OnDestroy, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import { NoticesService } from './services/notices.service';
import { NoticeListItem, NoticeListResponse } from './models/notice.models';
import { AttachmentsDialogComponent } from './attachments-dialog/attachments-dialog.component';
import { AttachmentDialogData } from './models/attachment.models';
import { RawJsonDialogComponent } from './raw-json-dialog/raw-json-dialog.component';
import { RawJsonDialogData } from './models/raw-json.models';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements AfterViewInit, OnDestroy {
  displayedColumns: string[] = [
    'purchaseNumber',
    'purchaseObjectInfo',
    'okpd2Code',
    'okpd2Name',
    'kvrCode',
    'kvrName',
    'maxPrice',
    'publishDate',
    'etpName',
    'documentType',
    'source',
    'updatedAt',
    'rawJson',
    'attachments'
  ];

  notices: NoticeListItem[] = [];
  totalCount = 0;
  pageSize = 20;
  isLoading = false;
  errorMessage = '';

  filtersForm = new FormGroup({
    search: new FormControl<string>('', { nonNullable: true }),
    purchaseNumber: new FormControl<string>('', { nonNullable: true }),
    okpd2Codes: new FormControl<string>('', { nonNullable: true }),
    kvrCodes: new FormControl<string>('', { nonNullable: true })
  });

  private readonly destroy$ = new Subject<void>();

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    private readonly noticesService: NoticesService,
    private readonly dialog: MatDialog
  ) {}

  ngAfterViewInit(): void {
    this.sort.sortChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.paginator.firstPage();
        this.loadNotices();
      });

    this.paginator.page
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.loadNotices());

    Promise.resolve().then(() => this.loadNotices());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get hasNoData(): boolean {
    return !this.isLoading && this.notices.length === 0 && !this.errorMessage;
  }

  loadNotices(): void {
    const sortField = this.sort?.active ? this.sort.active : 'publishDate';
    const sortDirection = this.sort?.direction ? this.sort.direction : 'desc';
    const pageIndex = this.paginator?.pageIndex ?? 0;
    const pageSize = this.paginator?.pageSize ?? this.pageSize;
    const search = this.getTrimmedValue(this.filtersForm.controls.search);
    const purchaseNumber = this.getTrimmedValue(this.filtersForm.controls.purchaseNumber);
    const okpd2Codes = this.getNormalizedCodes(this.filtersForm.controls.okpd2Codes);
    const kvrCodes = this.getNormalizedCodes(this.filtersForm.controls.kvrCodes);

    this.isLoading = true;
    this.errorMessage = '';

    this.noticesService
      .getNotices({
        page: pageIndex + 1,
        pageSize,
        search: search || undefined,
        purchaseNumber,
        okpd2Codes,
        kvrCodes,
        sortField,
        sortDirection
      })
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        next: (response: NoticeListResponse) => {
          this.notices = response.items;
          this.totalCount = response.totalCount;
          this.pageSize = response.pageSize;
        },
        error: () => {
          this.errorMessage = 'Не удалось загрузить данные. Попробуйте позже.';
        }
      });
  }

  openAttachments(notice: NoticeListItem): void {
    const data: AttachmentDialogData = {
      noticeId: notice.id,
      purchaseNumber: notice.purchaseNumber,
      entryName: notice.entryName
    };

    this.dialog.open(AttachmentsDialogComponent, {
      width: '900px',
      data
    });
  }

  openRawJson(notice: NoticeListItem): void {
    const rawJson = notice.rawJson;

    if (!rawJson) {
      return;
    }

    const data: RawJsonDialogData = {
      purchaseNumber: notice.purchaseNumber,
      entryName: notice.entryName,
      rawJson
    };

    this.dialog.open(RawJsonDialogComponent, {
      width: '800px',
      maxWidth: '95vw',
      data
    });
  }

  applyFilters(): void {
    if (this.paginator && this.paginator.pageIndex !== 0) {
      this.paginator.firstPage();
      return;
    }

    this.loadNotices();
  }

  resetFilters(): void {
    this.filtersForm.reset({
      search: '',
      purchaseNumber: '',
      okpd2Codes: '',
      kvrCodes: ''
    });
    this.filtersForm.markAsPristine();
    this.filtersForm.markAsUntouched();

    if (this.paginator && this.paginator.pageIndex !== 0) {
      this.paginator.firstPage();
      return;
    }

    this.loadNotices();
  }

  private getTrimmedValue(control: FormControl<string>): string | undefined {
    const value = control.value.trim();
    return value ? value : undefined;
  }

  private getNormalizedCodes(control: FormControl<string>): string | undefined {
    const codes = control.value
      .split(/[\s,;]+/)
      .map(code => code.trim())
      .filter(code => code.length > 0);

    return codes.length > 0 ? codes.join(',') : undefined;
  }
}
