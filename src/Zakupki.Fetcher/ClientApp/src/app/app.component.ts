import { AfterViewInit, Component, OnDestroy, ViewChild } from '@angular/core';
import { FormControl } from '@angular/forms';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, finalize, takeUntil } from 'rxjs/operators';

import { NoticesService } from './services/notices.service';
import { NoticeListItem, NoticeListResponse } from './models/notice.models';
import { AttachmentsDialogComponent } from './attachments-dialog/attachments-dialog.component';
import { AttachmentDialogData } from './models/attachment.models';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements AfterViewInit, OnDestroy {
  displayedColumns: string[] = [
    'purchaseNumber',
    'entryName',
    'purchaseObjectInfo',
    'maxPrice',
    'publishDate',
    'etpName',
    'documentType',
    'source',
    'updatedAt',
    'attachments'
  ];

  notices: NoticeListItem[] = [];
  totalCount = 0;
  pageSize = 20;
  isLoading = false;
  errorMessage = '';

  searchControl = new FormControl('');

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

    this.searchControl.valueChanges
      .pipe(
        debounceTime(400),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        this.paginator.firstPage();
        this.loadNotices();
      });

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
    const search = this.searchControl.value?.trim();

    this.isLoading = true;
    this.errorMessage = '';

    this.noticesService
      .getNotices({
        page: pageIndex + 1,
        pageSize,
        search: search || undefined,
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
}
