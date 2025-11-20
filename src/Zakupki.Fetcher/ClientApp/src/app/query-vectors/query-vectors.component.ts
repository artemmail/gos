import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';

import { UserQueryVectorDto } from '../models/query-vector.models';
import { QueryVectorService } from '../services/query-vector.service';
import { QueryVectorDialogComponent } from './query-vector-dialog.component';

@Component({
  selector: 'app-query-vectors',
  templateUrl: './query-vectors.component.html',
  styleUrls: ['./query-vectors.component.css']
})
export class QueryVectorsComponent implements OnInit {
  displayedColumns = ['id', 'userId', 'query', 'vector', 'actions'];
  vectors: UserQueryVectorDto[] = [];
  isLoading = false;
  errorMessage = '';

  constructor(
    private readonly queryVectorService: QueryVectorService,
    private readonly dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.loadData();
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
    this.queryVectorService.delete(item.id).subscribe({
      next: () => {
        this.vectors = this.vectors.filter(v => v.id !== item.id);
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'Не удалось удалить запись.';
        this.isLoading = false;
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

    this.queryVectorService.getAll().subscribe({
      next: data => {
        this.vectors = data;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'Не удалось загрузить данные.';
        this.isLoading = false;
      }
    });
  }
}
