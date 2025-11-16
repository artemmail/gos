import { AfterViewInit, Component, OnDestroy, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import { NoticesService } from '../services/notices.service';
import { NoticeListItem, NoticeListResponse } from '../models/notice.models';
import { AttachmentsDialogComponent } from '../attachments-dialog/attachments-dialog.component';
import { AttachmentDialogData } from '../models/attachment.models';
import { RawJsonDialogComponent } from '../raw-json-dialog/raw-json-dialog.component';
import { RawJsonDialogData } from '../models/raw-json.models';
import { NoticeAnalysisService, NoticeAnalysisResponse } from '../services/notice-analysis.service';
import { NoticeAnalysisDialogComponent } from '../notice-analysis-dialog/notice-analysis-dialog.component';
import { NoticeAnalysisDialogData } from '../notice-analysis-dialog/notice-analysis-dialog.models';
import { determineRegionFromRawJson, getRegionDisplayName } from '../constants/regions';
import { FavoritesService } from '../services/favorites.service';

@Component({
  selector: 'app-notices',
  templateUrl: './notices.component.html',
  styleUrls: ['./notices.component.css']
})
export class NoticesComponent implements AfterViewInit, OnDestroy {
  displayedColumns: string[] = [
    'favorite',
    'purchaseNumber',
    'region',
    'purchaseObjectInfo',
    'okpd2Code',
    'okpd2Name',
    'kvrCode',
    'kvrName',
    'maxPrice',
    'publishDate',
    'collectingEnd',
    'submissionProcedureDateRaw',
    'etpName',
    'documentType',
    'source',
    'updatedAt',
    'analysisStatus',
    'analysis',
    'rawJson',
    'attachments'
  ];

  notices: NoticeListItem[] = [];
  totalCount = 0;
  pageSize = 20;
  isLoading = false;
  errorMessage = '';
  analysisProgress: Record<string, boolean> = {};
  favoriteProgress: Record<string, boolean> = {};
  isFavoritesPage = false;

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
    private readonly dialog: MatDialog,
    private readonly analysisService: NoticeAnalysisService,
    private readonly snackBar: MatSnackBar,
    private readonly favoritesService: FavoritesService,
    private readonly route: ActivatedRoute
  ) {
    this.isFavoritesPage = this.route.snapshot.data?.['favorites'] === true;
  }

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

    const request$ = this.isFavoritesPage
      ? this.favoritesService.getFavorites({
          page: pageIndex + 1,
          pageSize,
          search: search || undefined,
          purchaseNumber,
          okpd2Codes,
          kvrCodes,
          sortField,
          sortDirection
        })
      : this.noticesService.getNotices({
          page: pageIndex + 1,
          pageSize,
          search: search || undefined,
          purchaseNumber,
          okpd2Codes,
          kvrCodes,
          sortField,
          sortDirection
        });

    request$
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        next: (response: NoticeListResponse) => {
          this.notices = response.items.map(item => ({
            ...item,
            computedRegion: determineRegionFromRawJson(item.rawJson, item.region)
          }));
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

  runAnalysis(notice: NoticeListItem, force = false): void {
    if (this.analysisProgress[notice.id]) {
      return;
    }

    this.analysisProgress[notice.id] = true;

    this.analysisService
      .analyze(notice.id, force)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => {
          this.analysisProgress[notice.id] = false;
        })
      )
      .subscribe({
        next: (response: NoticeAnalysisResponse) => {
          this.updateNoticeAnalysis(notice, response);
        },
        error: error => {
          const message = error?.error?.message ?? 'Не удалось запустить анализ.';
          this.snackBar.open(message, 'Закрыть', { duration: 6000 });
        }
      });
  }

  getAnalysisStatusLabel(notice: NoticeListItem): string {
    if ((notice.analysisStatus === 'Completed' || notice.hasAnalysisAnswer) && notice.hasAnalysisAnswer) {
      return 'Есть ответ';
    }

    if (notice.analysisStatus === 'InProgress' || this.analysisProgress[notice.id]) {
      return 'Анализ выполняется';
    }

    if (notice.analysisStatus === 'Failed') {
      return 'Ошибка';
    }

    return 'Нет ответа';
  }

  getAnalysisStatusClass(notice: NoticeListItem): string {
    if ((notice.analysisStatus === 'Completed' || notice.hasAnalysisAnswer) && notice.hasAnalysisAnswer) {
      return 'analysis-status analysis-ready';
    }

    if (notice.analysisStatus === 'Failed') {
      return 'analysis-status analysis-failed';
    }

    if (notice.analysisStatus === 'InProgress' || this.analysisProgress[notice.id]) {
      return 'analysis-status analysis-progress';
    }

    return 'analysis-status analysis-empty';
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

  isCollectingEndExpired(collectingEnd: string | null): boolean {
    if (!collectingEnd) {
      return false;
    }

    const collectingEndDate = new Date(collectingEnd);
    return !Number.isNaN(collectingEndDate.getTime()) && collectingEndDate.getTime() < Date.now();
  }

  private updateNoticeAnalysis(notice: NoticeListItem, response: NoticeAnalysisResponse): void {
    notice.analysisStatus = response.status ?? null;
    notice.hasAnalysisAnswer = response.hasAnswer;
    notice.analysisUpdatedAt = response.updatedAt ?? null;

    if (response.status === 'Completed' && response.result) {
      const data: NoticeAnalysisDialogData = {
        purchaseNumber: notice.purchaseNumber,
        entryName: notice.entryName,
        result: response.result,
        completedAt: response.completedAt ?? null
      };

      this.dialog.open(NoticeAnalysisDialogComponent, {
        width: '720px',
        data
      });
      return;
    }

    if (response.status === 'Failed') {
      const message = response.error ?? 'Не удалось выполнить анализ.';
      this.snackBar.open(message, 'Закрыть', { duration: 6000 });
      return;
    }

    if (response.status === 'InProgress') {
      this.snackBar.open('Анализ выполняется. Обновите статус позже.', 'Закрыть', { duration: 4000 });
    }
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

  getRegionLabel(notice: NoticeListItem): string {
    const regionCode = notice.computedRegion ?? notice.region;
    const label = getRegionDisplayName(regionCode);

    if (label) {
      return label;
    }

    return regionCode ?? '—';
  }

  toggleFavorite(notice: NoticeListItem): void {
    if (this.favoriteProgress[notice.id]) {
      return;
    }

    this.favoriteProgress[notice.id] = true;

    const request$ = notice.isFavorite
      ? this.favoritesService.removeFavorite(notice.id)
      : this.favoritesService.addFavorite(notice.id);

    request$
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.favoriteProgress[notice.id] = false))
      )
      .subscribe({
        next: () => {
          notice.isFavorite = !notice.isFavorite;
          const message = notice.isFavorite ? 'Добавлено в избранное' : 'Удалено из избранного';
          this.snackBar.open(message, 'Закрыть', { duration: 3000 });

          if (this.isFavoritesPage && !notice.isFavorite) {
            this.loadNotices();
          }
        },
        error: () => {
          const message = notice.isFavorite
            ? 'Не удалось удалить из избранного.'
            : 'Не удалось добавить в избранное.';
          this.snackBar.open(message, 'Закрыть', { duration: 4000 });
        }
      });
  }

  get pageTitle(): string {
    return this.isFavoritesPage ? 'Избранные извещения' : 'Реестр извещений';
  }

  get pageSubtitle(): string {
    return this.isFavoritesPage
      ? 'Здесь отображаются сохранённые записи из общего реестра'
      : 'Поиск, сортировка и навигация по таблице данных о закупках';
  }
}
