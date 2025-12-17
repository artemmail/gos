import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import { FabrikantProcedure } from '../models/fabrikant.models';

@Component({
  selector: 'app-fab-notices',
  templateUrl: './fab-notices.component.html',
  styleUrls: ['./fab-notices.component.css']
})
export class FabNoticesComponent implements OnInit, OnDestroy {
  readonly displayedColumns = ['procedureNumber', 'title', 'status', 'publishDate', 'applyEndDate', 'nmck'];
  procedures: FabrikantProcedure[] = [];
  isLoading = false;
  errorMessage = '';
  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly http: HttpClient,
    private readonly route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const sourceParam = params.get('source');
        const source = sourceParam ? Number(sourceParam) : undefined;

        if (source === 3) {
          this.loadProcedures(source);
          return;
        }

        this.procedures = [];
        this.errorMessage = 'Для просмотра данных Фабрикант укажите source=3.';
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadProcedures(source?: number): void {
    this.isLoading = true;
    this.errorMessage = '';

    let params = new HttpParams();

    if (typeof source === 'number') {
      params = params.set('source', source.toString());
    }

    this.http
      .get<FabrikantProcedure[]>('/notices-fab', { params })
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        next: procedures => (this.procedures = procedures),
        error: () => {
          this.errorMessage = 'Не удалось загрузить данные о процедурах Фабрикант.';
        }
      });
  }
}
