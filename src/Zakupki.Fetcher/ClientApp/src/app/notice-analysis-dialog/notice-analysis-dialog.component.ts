import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { NoticeAnalysisDialogData } from './notice-analysis-dialog.models';

@Component({
  selector: 'app-notice-analysis-dialog',
  templateUrl: './notice-analysis-dialog.component.html',
  styleUrls: ['./notice-analysis-dialog.component.css']
})
export class NoticeAnalysisDialogComponent {
  copyStatus: 'idle' | 'success' | 'error' = 'idle';

  constructor(
    private readonly dialogRef: MatDialogRef<NoticeAnalysisDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public readonly data: NoticeAnalysisDialogData
  ) {}

  close(): void {
    this.dialogRef.close();
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
}
