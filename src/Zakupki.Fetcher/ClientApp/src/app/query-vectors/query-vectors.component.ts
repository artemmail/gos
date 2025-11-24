import { Component, OnDestroy, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

import { UserQueryVectorDto } from '../models/query-vector.models';
import { QueryVectorService } from '../services/query-vector.service';
import { QueryVectorDialogComponent } from './query-vector-dialog.component';

@Component({
  selector: 'app-query-vectors',
  templateUrl: './query-vectors.component.html',
  styleUrls: ['./query-vectors.component.css']
})
export class QueryVectorsComponent implements OnInit, OnDestroy {
  displayedColumns = ['id', 'userId', 'query', 'vector', 'actions'];
  vectors: UserQueryVectorDto[] = [];
  isLoading = false;
  errorMessage = '';

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly queryVectorService: QueryVectorService,
    private readonly dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.queryVectorService.queryVectors$
      .pipe(takeUntil(this.destroy$))
      .subscribe(vectors => (this.vectors = vectors));

    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  openAddDialog(): void {
    const dialogRef = this.dialog.open(QueryVectorDialogComponent, {
      width: '600px'
    });

    dialogRef.afterClosed().subscribe((created?: boolean) => {
      if (created) {
        this.loadData();
      }
    });
  }

  delete(item: UserQueryVectorDto): void {
    if (this.isLoading) {
      return;
    }

    if (!confirm('Удалить запись?')) {
      return;
    }

    this.isLoading = true;
    this.queryVectorService
      .delete(item.id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        error: () => {
          this.errorMessage = 'Не удалось удалить запись.';
        }
      });
  }

  vectorPreview(vector?: number[] | null): string {
    if (!vector || vector.length === 0) {
      return '—';
    }

    const preview = vector.slice(0, 5).map(v => v.toFixed(4)).join(', ');
    return vector.length > 5 ? `${preview} …` : preview;
  }

  private loadData(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.queryVectorService
      .getAll()
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.isLoading = false))
      )
      .subscribe({
        error: () => {
          this.errorMessage = 'Не удалось загрузить данные.';
        }
      });
  }
}
