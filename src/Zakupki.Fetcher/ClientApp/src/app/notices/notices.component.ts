import { AfterViewInit, Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
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
import { FavoritesService } from '../services/favorites.service';
import { QueryVectorService } from '../services/query-vector.service';
import { UserQueryVectorDto } from '../models/query-vector.models';
import { NoticeVectorQuery } from '../models/notice.models';
import { RegionsService } from '../services/regions.service';
import { AuthService } from '../services/AuthService.service';

@Component({
  selector: 'app-notices',
  templateUrl: './notices.component.html',
  styleUrls: ['./notices.component.css']
})
export class NoticesComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly defaultSearchMode: SearchMode = 'direct';
  displayedColumns: string[] = [
    'favorite',
    'purchaseNumber',
    'similarity',
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
  similarityOptions = [40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90];
  searchModeControl = new FormControl<SearchMode>(this.defaultSearchMode, { nonNullable: true });
  expiredOnlyControl = new FormControl<boolean>(false, { nonNullable: true });
  profileRegionsControl = new FormControl<boolean>(false, { nonNullable: true });
  profileOkpd2Control = new FormControl<boolean>(false, { nonNullable: true });

  filtersForm = new FormGroup({
    search: new FormControl<string>('', { nonNullable: true }),
    purchaseNumber: new FormControl<string>('', { nonNullable: true })
  });

  favoriteSearchForm = new FormGroup({
    queryVectorId: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    similarityThreshold: new FormControl<number>(60, { nonNullable: true })
  });

  vectorSearchInProgress = false;
  queryVectors: UserQueryVectorDto[] = [];
  queryVectorsLoading = false;
  vectorSearchCriteria: Omit<NoticeVectorQuery, 'page' | 'pageSize'> | null = null;
  isAuthenticated = false;

  private readonly destroy$ = new Subject<void>();

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    private readonly noticesService: NoticesService,
    private readonly dialog: MatDialog,
    private readonly analysisService: NoticeAnalysisService,
    private readonly snackBar: MatSnackBar,
    private readonly favoritesService: FavoritesService,
    private readonly queryVectorService: QueryVectorService,
    private readonly route: ActivatedRoute,
    private readonly regionsService: RegionsService,
    private readonly authService: AuthService
  ) {
    this.isFavoritesPage = this.route.snapshot.data?.['favorites'] === true;
  }

  ngOnInit(): void {
    this.preloadRegions();
    this.queryVectorService.queryVectors$
      .pipe(takeUntil(this.destroy$))
      .subscribe(vectors => this.updateQueryVectors(vectors));

    this.loadQueryVectors();
    this.authService.user$
      .pipe(takeUntil(this.destroy$))
      .subscribe(user => {
        this.isAuthenticated = !!user;

        if (!this.isAuthenticated && this.profileRegionsControl.value) {
          this.profileRegionsControl.setValue(false, { emitEvent: false });
        }

        if (!this.isAuthenticated && this.profileOkpd2Control.value) {
          this.profileOkpd2Control.setValue(false, { emitEvent: false });
        }
      });
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

  private preloadRegions(): void {
    this.regionsService
      .getRegions()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        error: () => {
          this.snackBar.open('Не удалось загрузить список регионов. Будут показаны коды.', 'Закрыть', {
            duration: 4000
          });
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadQueryVectors(): void {
    this.queryVectorsLoading = true;
    this.queryVectorService
      .getAll()
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.queryVectorsLoading = false))
      )
      .subscribe({
        error: () => {
          this.snackBar.open('Не удалось загрузить сохранённые запросы.', 'Закрыть', { duration: 4000 });
        }
      });
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
    const expiredOnly = this.expiredOnlyControl.value;
    const filterByUserRegions = this.profileRegionsControl.value;
    const filterByUserOkpd2Codes = this.profileOkpd2Control.value;
    const vectorCriteria = this.vectorSearchCriteria;

    this.isLoading = true;
    this.errorMessage = '';

    const request$ = vectorCriteria
      ? this.noticesService.vectorSearch({
          page: pageIndex + 1,
          pageSize,
          queryVectorId: vectorCriteria.queryVectorId,
          similarityThresholdPercent: vectorCriteria.similarityThresholdPercent,
          expiredOnly: vectorCriteria.expiredOnly,
          collectingEndLimit: vectorCriteria.collectingEndLimit,
          filterByUserRegions: vectorCriteria.filterByUserRegions,
          filterByUserOkpd2Codes: vectorCriteria.filterByUserOkpd2Codes
        })
      : this.isFavoritesPage
        ? this.favoritesService.getFavorites({
            page: pageIndex + 1,
            pageSize,
            expiredOnly,
            filterByUserRegions,
            search: search || undefined,
            purchaseNumber,
            filterByUserOkpd2Codes,
            sortField,
            sortDirection
          })
        : this.noticesService.getNotices({
            page: pageIndex + 1,
            pageSize,
            expiredOnly,
            filterByUserRegions,
            search: search || undefined,
            purchaseNumber,
            filterByUserOkpd2Codes,
            sortField,
            sortDirection
          });

    request$
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => {
          this.isLoading = false;
          if (vectorCriteria) {
            this.vectorSearchInProgress = false;
          }
        })
      )
      .subscribe({
        next: (response: NoticeListResponse) => {
          this.notices = response.items;
          this.totalCount = response.totalCount;
          this.pageSize = response.pageSize;
        },
        error: error => {
          this.errorMessage = error?.error?.message ?? 'Не удалось загрузить данные. Попробуйте позже.';
        }
      });
  }

  openAttachments(notice: NoticeListItem): void {
    const data: AttachmentDialogData = {
      noticeId: notice.id,
      purchaseNumber: notice.purchaseNumber,
      title: this.getNoticeTitle(notice)
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
      title: this.getNoticeTitle(notice),
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
    this.resetVectorCriteria();

    if (this.paginator && this.paginator.pageIndex !== 0) {
      this.paginator.firstPage();
      return;
    }

    this.loadNotices();
  }

  resetFilters(): void {
    this.filtersForm.reset({
      search: '',
      purchaseNumber: ''
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

  private getNoticeTitle(notice: NoticeListItem): string {
    return notice.purchaseObjectInfo || `Закупка ${notice.purchaseNumber}`;
  }

  private updateNoticeAnalysis(notice: NoticeListItem, response: NoticeAnalysisResponse): void {
    notice.analysisStatus = response.status ?? null;
    notice.hasAnalysisAnswer = response.hasAnswer;
    notice.analysisUpdatedAt = response.updatedAt ?? null;

    if (response.status === 'Completed' && (response.result || response.structuredResult)) {
      const data: NoticeAnalysisDialogData = {
        noticeId: notice.id,
        purchaseNumber: notice.purchaseNumber,
        title: this.getNoticeTitle(notice),
        result: response.result ?? null,
        completedAt: response.completedAt ?? null,
        prompt: response.prompt ?? null,
        structuredResult: response.structuredResult ?? null,
        decisionScore: response.decisionScore ?? null,
        recommended: response.recommended ?? null
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

  getRegionLabel(notice: NoticeListItem): string {
    const label = this.regionsService.getRegionLabel(notice.region);

    if (label) {
      return label;
    }

    return notice.region ?? '—';
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

  runVectorSearch(): void {
    if (this.vectorSearchInProgress) {
      return;
    }

    if (this.favoriteSearchForm.invalid) {
      this.favoriteSearchForm.markAllAsTouched();
      return;
    }

    const queryVectorId = this.favoriteSearchForm.controls.queryVectorId.value;
    const selectedVector = this.queryVectors.find(v => v.id === queryVectorId);

    if (!selectedVector) {
      this.snackBar.open('Выберите запрос из списка.', 'Закрыть', { duration: 4000 });
      return;
    }

    const expiredOnly = this.expiredOnlyControl.value;
    const filterByUserRegions = this.profileRegionsControl.value;
    const filterByUserOkpd2Codes = this.profileOkpd2Control.value;
    const similarityThresholdPercent = this.favoriteSearchForm.controls.similarityThreshold.value;

    this.vectorSearchCriteria = {
      queryVectorId,
      similarityThresholdPercent,
      expiredOnly,
      collectingEndLimit: new Date().toISOString(),
      filterByUserRegions,
      filterByUserOkpd2Codes
    };

    this.vectorSearchInProgress = true;

    if (this.paginator && this.paginator.pageIndex !== 0) {
      this.paginator.firstPage();
      return;
    }

    this.loadNotices();
  }

  clearVectorSearch(): void {
    this.resetVectorCriteria();

    if (this.paginator && this.paginator.pageIndex !== 0) {
      this.paginator.firstPage();
      return;
    }

    this.loadNotices();
  }

  private resetVectorCriteria(): void {
    this.vectorSearchCriteria = null;
    this.vectorSearchInProgress = false;
  }

  get isDirectSearch(): boolean {
    return this.searchModeControl.value === 'direct';
  }

  get isSemanticSearch(): boolean {
    return this.searchModeControl.value === 'semantic';
  }

  onSearchModeChange(mode: SearchMode): void {
    const previousMode = this.searchModeControl.value;
    this.searchModeControl.setValue(mode);

    if (previousMode === 'semantic' && mode === 'direct' && this.vectorSearchCriteria) {
      this.clearVectorSearch();
    }
  }

  onSemanticModeToggle(enabled: boolean): void {
    this.onSearchModeChange(enabled ? 'semantic' : 'direct');
  }

  onSubmitSearch(): void {
    if (this.isDirectSearch) {
      this.applyFilters();
      return;
    }

    this.runVectorSearch();
  }

  onResetSearch(): void {
    this.expiredOnlyControl.setValue(false);
    this.profileRegionsControl.setValue(false);

    if (this.isDirectSearch) {
      this.resetFilters();
      return;
    }

    this.resetFavoriteSearchForm();
  }

  private resetFavoriteSearchForm(): void {
    const defaultQuery = this.queryVectors[0]?.id ?? '';

    this.favoriteSearchForm.reset({
      queryVectorId: defaultQuery,
      similarityThreshold: 60
    });

    this.favoriteSearchForm.markAsPristine();
    this.favoriteSearchForm.markAsUntouched();

    this.clearVectorSearch();
  }

  private updateQueryVectors(vectors: UserQueryVectorDto[]): void {
    this.queryVectors = vectors;

    const selectedId = this.favoriteSearchForm.controls.queryVectorId.value;
    const hasSelected = !!selectedId && vectors.some(vector => vector.id === selectedId);

    if (!hasSelected) {
      const defaultId = vectors[0]?.id ?? '';
      this.favoriteSearchForm.controls.queryVectorId.setValue(defaultId, { emitEvent: false });
    }

    if (this.vectorSearchCriteria && !vectors.some(vector => vector.id === this.vectorSearchCriteria?.queryVectorId)) {
      this.clearVectorSearch();
    }
  }
}

type SearchMode = 'direct' | 'semantic';
