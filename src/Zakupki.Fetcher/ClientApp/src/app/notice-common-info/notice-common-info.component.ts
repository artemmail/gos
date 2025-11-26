import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

import { NoticeCommonInfo } from '../models/notice.models';

@Component({
  selector: 'app-notice-common-info',
  templateUrl: './notice-common-info.component.html',
  styleUrls: ['./notice-common-info.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NoticeCommonInfoComponent {
  @Input() notice!: NoticeCommonInfo;

  /** "2025-11-24+03:00" -> "24.11.2025" */
  formatRawDate(raw?: string | null): string | null {
    if (!raw) {
      return null;
    }
    const datePart = raw.split('+')[0];
    const parts = datePart.split('-').map(x => +x);
    if (parts.length !== 3 || !parts[0] || !parts[1] || !parts[2]) {
      return raw;
    }
    const [year, month, day] = parts;
    return `${day.toString().padStart(2, '0')}.` +
           `${month.toString().padStart(2, '0')}.` +
           `${year}`;
  }

  /** ISO string -> "dd.MM.yyyy HH:mm" */
  formatDateTime(value?: string | null): string | null {
    if (!value) {
      return null;
    }
    const d = new Date(value);
    if (isNaN(d.getTime())) {
      return value;
    }
    const dd = d.getDate().toString().padStart(2, '0');
    const mm = (d.getMonth() + 1).toString().padStart(2, '0');
    const yyyy = d.getFullYear();
    const hh = d.getHours().toString().padStart(2, '0');
    const min = d.getMinutes().toString().padStart(2, '0');
    return `${dd}.${mm}.${yyyy} ${hh}:${min}`;
  }

  /** bytes -> "54,3 КБ" */
  formatFileSize(bytes?: number | null): string | null {
    if (bytes == null) {
      return null;
    }
    if (bytes < 1024) {
      return `${bytes} Б`;
    }
    const kb = bytes / 1024;
    return `${kb.toFixed(1)} КБ`;
  }
}
