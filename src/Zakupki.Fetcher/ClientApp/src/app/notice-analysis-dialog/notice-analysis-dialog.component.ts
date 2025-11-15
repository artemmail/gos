import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { NoticeAnalysisDialogData } from './notice-analysis-dialog.models';

@Component({
  selector: 'app-notice-analysis-dialog',
  templateUrl: './notice-analysis-dialog.component.html',
  styleUrls: ['./notice-analysis-dialog.component.css']
})
export class NoticeAnalysisDialogComponent {
  constructor(
    private readonly dialogRef: MatDialogRef<NoticeAnalysisDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public readonly data: NoticeAnalysisDialogData
  ) {}

  close(): void {
    this.dialogRef.close();
  }
}
