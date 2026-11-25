import { Injectable } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

@Injectable()
export class RussianPaginatorIntl extends MatPaginatorIntl {
  override itemsPerPageLabel = 'На странице';
  override nextPageLabel = 'Следующая страница';
  override previousPageLabel = 'Предыдущая страница';
  override firstPageLabel = 'Первая страница';
  override lastPageLabel = 'Последняя страница';
  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (!length || !pageSize) return `0 из ${length}`;
    return `${(page * pageSize + 1).toLocaleString('ru-RU')}–${Math.min((page + 1) * pageSize, length).toLocaleString('ru-RU')} из ${length.toLocaleString('ru-RU')}`;
  };
}
