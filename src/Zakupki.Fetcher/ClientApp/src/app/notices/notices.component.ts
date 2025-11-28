import { AfterViewInit, Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort, MatSortable, SortDirection } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import { NoticesService } from '../services/notices.service';
import { NoticeListItem, NoticeListResponse } from '../models/notice.models';
import { NoticeAnalysisService, NoticeAnalysisResponse } from '../services/notice-analysis.service';
import { NoticeAnalysisDialogComponent } from '../notice-analysis-dialog/notice-analysis-dialog.component';
import { NoticeAnalysisDialogData } from '../notice-analysis-dialog/notice-analysis-dialog.models';
import { FavoritesService } from '../services/favorites.service';
import { QueryVectorService } from '../services/query-vector.service';
import { UserQueryVectorDto } from '../models/query-vector.models';
import { NoticeVectorQuery } from '../models/notice.models';
import { RegionsService } from '../services/regions.service';
import { AuthService } from '../services/AuthService.service';
import { NoticeAnalysisNotificationsService } from '../services/notice-analysis-notifications.service';

@Component({
  selector: 'app-notices',
  templateUrl: './notices.component.html',
  styleUrls: ['./notices.component.css']
})
export class NoticesComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly defaultSearchMode: SearchMode = 'direct';
  private readonly filtersStorageKey = 'notices-filters-v1';
  displayedColumns: string[] = [
    'favorite',
    'purchaseNumber',
    'similarity',
    'region',
    'purchaseObjectInfo',
    'okpd2Code',
    'okpd2Name',
    'maxPrice',
    'publishDate',
    'collectingEnd',
    'analysisStatus',
    'analysis'
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
  cachedSortState: SortState | null = null;
  readonly starIcons = Array.from({ length: 5 });

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
    private readonly authService: AuthService,
    private readonly analysisNotifications: NoticeAnalysisNotificationsService
  ) {
    this.isFavoritesPage = this.route.snapshot.data?.['favorites'] === true;
  }

  ngOnInit(): void {
    this.restoreFiltersFromCache();
    this.setupFilterPersistence();
    this.preloadRegions();
    this.analysisNotifications.analysisUpdates$
      .pipe(takeUntil(this.destroy$))
      .subscribe(response => this.handleAnalysisUpdate(response));

    this.queryVectorService.queryVectors$
      .pipe(takeUntil(this.destroy$))
      .subscribe(vectors => this.updateQueryVectors(vectors));

    this.loadQueryVectors();
    this.authService.user$
      .pipe(takeUntil(this.destroy$))
      .subscribe(user => {
        this.isAuthenticated = !!user;
        if (this.isAuthenticated) {
          this.analysisNotifications.connect();
        } else {
          this.analysisNotifications.disconnect();
        }

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
        this.saveFiltersToCache();
        this.loadNotices();
      });

    this.applyCachedSort();

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
    this.analysisNotifications.disconnect();
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
          filterByUserOkpd2Codes: vectorCriteria.filterByUserOkpd2Codes,
          sortField,
          sortDirection
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
    if (this.isAnalysisCompleted(notice) && notice.hasAnalysisAnswer) {
      if (notice.recommended === false) {
        return 'Не подходит';
      }

      if (notice.recommended === true) {
        return this.formatDecisionScore(notice.decisionScore);
      }

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
    if (this.isAnalysisCompleted(notice) && notice.hasAnalysisAnswer) {
      if (notice.recommended === false) {
        return 'analysis-status analysis-rejected';
      }

      if (notice.recommended === true) {
        return 'analysis-status analysis-ready analysis-score';
      }

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

  isAnalysisPositive(notice: NoticeListItem): boolean {
    return this.isAnalysisCompleted(notice) && notice.hasAnalysisAnswer && notice.recommended === true;
  }

  isAnalysisNegative(notice: NoticeListItem): boolean {
    return this.isAnalysisCompleted(notice) && notice.hasAnalysisAnswer && notice.recommended === false;
  }

  private isAnalysisCompleted(notice: NoticeListItem): boolean {
    return notice.analysisStatus === 'Completed' || notice.hasAnalysisAnswer;
  }

  formatDecisionScore(score: number | null): string {
    if (score === null || score === undefined || Number.isNaN(score)) {
      return '—';
    }

    const clamped = Math.min(Math.max(score, 0), 1);
    const scaled = Math.round(clamped * 1000) / 10;
    return scaled.toFixed(1);
  }

  hasDecisionScore(notice: NoticeListItem): boolean {
    return this.isAnalysisCompleted(notice) && notice.decisionScore !== null && notice.decisionScore !== undefined;
  }

  getDecisionScoreFillPercentage(notice: NoticeListItem): number {
    if (!this.hasDecisionScore(notice) || Number.isNaN(notice.decisionScore!)) {
      return 0;
    }

    const clamped = Math.min(Math.max(notice.decisionScore ?? 0, 0), 1);
    return clamped * 100;
  }

  applyFilters(): void {
    this.resetVectorCriteria();

    this.saveFiltersToCache();

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

    this.saveFiltersToCache();

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

  private handleAnalysisUpdate(response: NoticeAnalysisResponse): void {
    const notice = this.notices.find(item => item.id === response.noticeId);

    if (!notice) {
      return;
    }

    this.updateNoticeAnalysis(notice, response);
  }

  private updateNoticeAnalysis(notice: NoticeListItem, response: NoticeAnalysisResponse): void {
    notice.analysisStatus = response.status ?? null;
    notice.hasAnalysisAnswer = response.hasAnswer;
    notice.analysisUpdatedAt = response.updatedAt ?? null;
    notice.recommended = response.recommended ?? null;
    notice.decisionScore = response.decisionScore ?? null;

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
    this.saveFiltersToCache();

    if (this.paginator && this.paginator.pageIndex !== 0) {
      this.paginator.firstPage();
      return;
    }

    this.loadNotices();
  }

  clearVectorSearch(): void {
    this.resetVectorCriteria();
    this.saveFiltersToCache();

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
    this.saveFiltersToCache();

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
    this.saveFiltersToCache();

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
    this.saveFiltersToCache();
  }

  displayVectorQuery = (id: string): string => {
    const vector = this.queryVectors.find(item => item.id === id);

    return vector?.query ?? '';
  };

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

  private get storageKey(): string {
    return `${this.filtersStorageKey}${this.isFavoritesPage ? '-favorites' : ''}`;
  }

  private setupFilterPersistence(): void {
    this.filtersForm.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.saveFiltersToCache());
    this.expiredOnlyControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.saveFiltersToCache());
    this.profileRegionsControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.saveFiltersToCache());
    this.profileOkpd2Control.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.saveFiltersToCache());
    this.searchModeControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.saveFiltersToCache());
    this.favoriteSearchForm.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.saveFiltersToCache());
  }

  private saveFiltersToCache(): void {
    const sortField = this.sort?.active ?? 'publishDate';
    const sortDirection = (this.sort?.direction as SortDirection | '') || 'desc';

    const state: FilterState = {
      search: this.filtersForm.controls.search.value,
      purchaseNumber: this.filtersForm.controls.purchaseNumber.value,
      expiredOnly: this.expiredOnlyControl.value,
      profileRegions: this.profileRegionsControl.value,
      profileOkpd2: this.profileOkpd2Control.value,
      searchMode: this.searchModeControl.value,
      favoriteQueryVectorId: this.favoriteSearchForm.controls.queryVectorId.value,
      favoriteSimilarityThreshold: this.favoriteSearchForm.controls.similarityThreshold.value,
      vectorCriteria: this.vectorSearchCriteria,
      sortField,
      sortDirection
    };

    try {
      localStorage.setItem(this.storageKey, JSON.stringify(state));
    } catch (error) {
      console.error('Не удалось сохранить параметры фильтрации в кеш.', error);
    }
  }

  private restoreFiltersFromCache(): void {
    try {
      const rawState = localStorage.getItem(this.storageKey);

      if (!rawState) {
        return;
      }

      const state = JSON.parse(rawState) as Partial<FilterState>;

      this.searchModeControl.setValue(state.searchMode ?? this.defaultSearchMode, { emitEvent: false });
      this.expiredOnlyControl.setValue(state.expiredOnly ?? false, { emitEvent: false });
      this.profileRegionsControl.setValue(state.profileRegions ?? false, { emitEvent: false });
      this.profileOkpd2Control.setValue(state.profileOkpd2 ?? false, { emitEvent: false });

      this.filtersForm.setValue(
        {
          search: state.search ?? '',
          purchaseNumber: state.purchaseNumber ?? ''
        },
        { emitEvent: false }
      );

      const cachedQueryId = state.favoriteQueryVectorId ?? '';
      const cachedSimilarity = state.favoriteSimilarityThreshold ?? 60;
      this.favoriteSearchForm.setValue(
        {
          queryVectorId: cachedQueryId,
          similarityThreshold: cachedSimilarity
        },
        { emitEvent: false }
      );

      this.vectorSearchCriteria = state.vectorCriteria ?? null;

      if (state.sortField && state.sortDirection) {
        this.cachedSortState = {
          active: state.sortField,
          direction: state.sortDirection
        };
      }
    } catch (error) {
      console.error('Не удалось восстановить параметры фильтрации из кеша.', error);
    }
  }

  private applyCachedSort(): void {
    if (!this.cachedSortState || !this.sort) {
      return;
    }

    const sortable = this.sort.sortables.get(this.cachedSortState.active) as MatSortable | undefined;

    if (!sortable) {
      return;
    }

    this.sort.sort({
      id: this.cachedSortState.active,
      start: this.cachedSortState.direction,
      disableClear: sortable.disableClear
    });
  }
}

type SearchMode = 'direct' | 'semantic';

type FilterState = {
  search: string;
  purchaseNumber: string;
  expiredOnly: boolean;
  profileRegions: boolean;
  profileOkpd2: boolean;
  searchMode: SearchMode;
  favoriteQueryVectorId: string;
  favoriteSimilarityThreshold: number;
  vectorCriteria: Omit<NoticeVectorQuery, 'page' | 'pageSize'> | null;
  sortField: string;
  sortDirection: SortDirection;
};

type SortState = {
  active: string;
  direction: SortDirection;
};
