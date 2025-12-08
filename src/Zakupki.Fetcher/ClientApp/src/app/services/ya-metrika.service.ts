import { Injectable } from '@angular/core';

declare const ym: (...args: any[]) => void;

@Injectable({ providedIn: 'root' })
export class YaMetrikaService {
  private readonly counterId = 105727669;

  hit(path: string, title?: string): void {
    if (typeof ym === 'function') {
      ym(this.counterId, 'hit', path, { title });
    } else {
      console.warn('Яндекс.Метрика не инициализирована');
    }
  }
}
