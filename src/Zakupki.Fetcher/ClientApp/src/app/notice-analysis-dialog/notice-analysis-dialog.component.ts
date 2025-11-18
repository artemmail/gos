import { Component, Inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { finalize } from 'rxjs/operators';

import { NoticeAnalysisDialogData } from './notice-analysis-dialog.models';
import { TenderAnalysisResult, TenderScores } from '../models/notice-analysis.models';
import { NoticeAnalysisService } from '../services/notice-analysis.service';

interface ScoreSectionView {
  title: string;
  score: number | null;
  shortComment: string;
  detailedComment: string;
}

@Component({
  selector: 'app-notice-analysis-dialog',
  templateUrl: './notice-analysis-dialog.component.html',
  styleUrls: ['./notice-analysis-dialog.component.css']
})
export class NoticeAnalysisDialogComponent {
  copyStatus: 'idle' | 'success' | 'error' = 'idle';
  structuredResult: TenderAnalysisResult | null;
  scoreSections: ScoreSectionView[] = [];
  decisionScoreTen: number | null = null;
  recommendation: boolean | null = null;
  summaryText: string | null = null;
  reportInProgress = false;

  constructor(
    private readonly dialogRef: MatDialogRef<NoticeAnalysisDialogComponent>,
    private readonly noticeAnalysisService: NoticeAnalysisService,
    private readonly snackBar: MatSnackBar,
    @Inject(MAT_DIALOG_DATA) public readonly data: NoticeAnalysisDialogData
  ) {
    this.structuredResult = this.resolveStructuredResult(data);
    this.scoreSections = this.buildScoreSections(this.structuredResult?.scores ?? null);

    const decisionScore = data.decisionScore ?? this.structuredResult?.decisionScore ?? null;
    this.decisionScoreTen = this.toTenScale(decisionScore);
    this.recommendation = data.recommended ?? this.structuredResult?.recommended ?? null;

    const summary = this.structuredResult?.summary?.trim();
    this.summaryText = summary && summary.length > 0 ? summary : null;
  }

  close(): void {
    this.dialogRef.close();
  }

  get hasStructuredResult(): boolean {
    return !!this.structuredResult;
  }

  get canDownloadReport(): boolean {
    return !!this.data.noticeId && this.hasStructuredResult;
  }

  async copyPrompt(prompt: string | null | undefined): Promise<void> {
    if (!prompt) {
      return;
    }

    const copied = await this.writeToClipboard(prompt);
    this.copyStatus = copied ? 'success' : 'error';

    if (copied) {
      setTimeout(() => {
        if (this.copyStatus === 'success') {
          this.copyStatus = 'idle';
        }
      }, 3000);
    }
  }

  downloadReport(): void {
    if (!this.data.noticeId || this.reportInProgress) {
      return;
    }

    this.reportInProgress = true;
    this.noticeAnalysisService
      .downloadReport(this.data.noticeId)
      .pipe(finalize(() => (this.reportInProgress = false)))
      .subscribe({
        next: response => this.handleReportResponse(response),
        error: error => {
          const message = error?.error?.message ?? 'Не удалось сформировать файл отчёта.';
          this.snackBar.open(message, 'Закрыть', { duration: 5000 });
        }
      });
  }

  private handleReportResponse(response: HttpResponse<Blob>): void {
    const blob = response.body;
    if (!blob) {
      this.snackBar.open('Ответ сервера не содержит файл отчёта.', 'Закрыть', { duration: 5000 });
      return;
    }

    const fileName = this.extractFileName(response) ?? this.buildFallbackFileName();
    this.triggerDownload(blob, fileName);
  }

  private extractFileName(response: HttpResponse<Blob>): string | null {
    const disposition = response.headers.get('content-disposition');
    if (!disposition) {
      return null;
    }

    const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
    if (utf8Match?.[1]) {
      try {
        return decodeURIComponent(utf8Match[1]);
      } catch {
        return utf8Match[1];
      }
    }

    const asciiMatch = /filename="?([^";]+)"?/i.exec(disposition);
    return asciiMatch?.[1] ?? null;
  }

  private buildFallbackFileName(): string {
    const suffix = this.data.purchaseNumber?.trim()?.length ? `_${this.data.purchaseNumber.trim()}` : '';
    return `Анализ${suffix}.docx`;
  }

  private triggerDownload(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }

  private async writeToClipboard(text: string): Promise<boolean> {
    if (navigator?.clipboard?.writeText) {
      try {
        await navigator.clipboard.writeText(text);
        return true;
      } catch (error) {
        // ignore and fallback
      }
    }

    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.focus();
    textarea.select();

    let copied = false;
    try {
      copied = document.execCommand('copy');
    } catch (error) {
      copied = false;
    }

    document.body.removeChild(textarea);
    return copied;
  }

  private resolveStructuredResult(data: NoticeAnalysisDialogData): TenderAnalysisResult | null {
    if (data.structuredResult) {
      return data.structuredResult;
    }

    if (!data.result) {
      return null;
    }

    try {
      return JSON.parse(data.result) as TenderAnalysisResult;
    } catch {
      return null;
    }
  }

  private buildScoreSections(scores: TenderScores | null): ScoreSectionView[] {
    if (!scores) {
      return [];
    }

    const config: Array<{ key: keyof TenderScores; title: string }> = [
      { key: 'profitability', title: 'Рентабельность' },
      { key: 'attractiveness', title: 'Привлекательность' },
      { key: 'risk', title: 'Риски' }
    ];

    return config
      .map(({ key, title }) => {
        const section = scores[key];
        if (!section) {
          return null;
        }

        const score = this.toTenScale(section.score ?? null);
        const shortComment = section.shortComment?.trim() ?? '';
        const detailedComment = section.detailedComment?.trim() ?? '';

        if (score === null && !shortComment && !detailedComment) {
          return null;
        }

        return {
          title,
          score,
          shortComment,
          detailedComment
        } satisfies ScoreSectionView;
      })
      .filter((section): section is ScoreSectionView => section !== null);
  }

  private toTenScale(score: number | null | undefined): number | null {
    if (typeof score !== 'number' || Number.isNaN(score)) {
      return null;
    }

    const clamped = Math.min(Math.max(score, 0), 1);
    return Math.round(clamped * 100) / 10;
  }
}
