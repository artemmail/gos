import { AfterViewInit, Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort, MatSortable } from '@angular/material/sort';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import { MosNoticesService } from '../services/mos-notices.service';
import { MosNoticeListItem } from '../models/mos-notice.models';

@Component({
  selector: 'app-mos-notices',
  templateUrl: './mos-notices.component.html',
  styleUrls: ['./mos-notices.component.css']
})
export class MosNoticesComponent implements OnInit, AfterViewInit, OnDestroy {
  displayedColumns: string[] = [
    'purchaseNumber',
    'name',
    'federalLawName',
    'maxPrice',
    'publishDate',
    'collectingEnd',
    'customer',
    'region'
  ];

  notices: MosNoticeListItem[] = [];
  totalCount = 0;
  pageSize = 20;
  isLoading = false;
  errorMessage = '';

  filtersForm = new FormGroup({
    search: new FormControl<string>('', { nonNullable: true })
  });

  private readonly destroy$ = new Subject<void>();

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    private readonly mosNoticesService: MosNoticesService,
    private readonly snackBar: MatSnackBar
  ) { }

  ngOnInit(): void {
    // no-op
  }

  ngAfterViewInit(): void {
    const sortState: MatSortable = { id: 'publishDate', start: 'desc', disableClear: true };
    this.sort.sort(sortState);

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

  loadNotices(): void {
    const sortField = this.sort?.active ?? 'publishDate';
    const sortDirection = this.sort?.direction ?? 'desc';
    const pageIndex = this.paginator?.pageIndex ?? 0;
    const pageSize = this.paginator?.pageSize ?? this.pageSize;
    const search = this.filtersForm.controls.search.value.trim();

    this.isLoading = true;
    this.errorMessage = '';

    this.mosNoticesService
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
        next: response => {
          this.notices = response.items;
          this.totalCount = response.totalCount;
        },
        error: () => {
          this.errorMessage = 'Не удалось загрузить список закупок mos.ru.';
          this.notices = [];
          this.snackBar.open('Не удалось загрузить список закупок mos.ru.', 'Закрыть', { duration: 4000 });
        }
      });
  }

  clearFilters(): void {
    this.filtersForm.reset({ search: '' });
    this.paginator?.firstPage();
    this.loadNotices();
  }

  get hasNoData(): boolean {
    return !this.isLoading && this.notices.length === 0 && !this.errorMessage;
  }

  getCustomerDisplay(item: MosNoticeListItem): string {
    if (item.customerInn && item.customerName) {
      return `${item.customerName} (ИНН ${item.customerInn})`;
    }

    return item.customerName ?? item.customerInn ?? '—';
  }
}
