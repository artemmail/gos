import { Component } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';

import { QueryVectorService } from '../services/query-vector.service';

@Component({
  selector: 'app-query-vector-dialog',
  templateUrl: './query-vector-dialog.component.html',
  styleUrls: ['./query-vector-dialog.component.css']
})
export class QueryVectorDialogComponent {
  form: FormGroup;
  isSubmitting = false;
  errorMessage = '';

  constructor(
    private readonly dialogRef: MatDialogRef<QueryVectorDialogComponent, boolean>,
    private readonly fb: FormBuilder,
    private readonly queryVectorService: QueryVectorService
  ) {
    this.form = this.fb.group({
      query: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(4000)]]
    });
  }

  get queryControl(): AbstractControl | null {
    return this.form.get('query');
  }

  submit(): void {
    if (this.form.invalid || this.isSubmitting) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';

    this.queryVectorService.create({ query: this.form.value.query }).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.errorMessage = 'Не удалось отправить запрос в очередь.';
        this.isSubmitting = false;
      }
    });
  }

  close(): void {
    this.dialogRef.close();
  }
}
