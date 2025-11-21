import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

import {
  RegionOption,
  UpdateUserCompanyProfileRequest,
  UserCompanyProfile,
  UserCompanyService
} from '../services/user-company.service';
import { QueryVectorService } from '../services/query-vector.service';
import { UserQueryVectorDto } from '../models/query-vector.models';
import { QueryVectorDialogComponent } from '../query-vectors/query-vector-dialog.component';

@Component({
  selector: 'app-company-profile-dialog',
  templateUrl: './company-profile-dialog.component.html',
  styleUrls: ['./company-profile-dialog.component.css']
})
export class CompanyProfileDialogComponent implements OnInit, OnDestroy {
  form: FormGroup;
  availableRegions: RegionOption[] = [];
  queryVectors: UserQueryVectorDto[] = [];
  vectorColumns = ['query', 'vector', 'actions'];
  isLoading = false;
  isSaving = false;
  isVectorsLoading = false;
  isVectorActionInProgress = false;
  errorMessage = '';
  successMessage = '';
  vectorsErrorMessage = '';

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly dialogRef: MatDialogRef<CompanyProfileDialogComponent>,
    private readonly fb: FormBuilder,
    private readonly userCompanyService: UserCompanyService,
    private readonly queryVectorService: QueryVectorService,
    private readonly dialog: MatDialog
  ) {
    this.form = this.fb.group({
      companyInfo: ['', [Validators.maxLength(8000)]],
      regions: [[] as string[]]
    });
  }

  ngOnInit(): void {
    this.loadProfile();
    this.loadVectors();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  close(): void {
    this.dialogRef.close();
  }

  save(): void {
    if (this.form.invalid || this.isSaving) {
      this.form.markAllAsTouched();
      return;
    }

    const payload: UpdateUserCompanyProfileRequest = {
      companyInfo: this.form.value.companyInfo ?? '',
      regions: this.form.value.regions ?? []
    };

    this.isSaving = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.userCompanyService
      .updateProfile(payload)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: profile => this.handleProfileLoaded(profile, true),
        error: () => {
          this.errorMessage = 'Не удалось сохранить данные компании.';
          this.isSaving = false;
        }
      });
  }

  openAddVectorDialog(): void {
    if (this.isVectorActionInProgress) {
      return;
    }

    const dialogRef = this.dialog.open(QueryVectorDialogComponent, { width: '600px' });

    dialogRef
      .afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe(created => {
        if (created) {
          this.loadVectors();
        }
      });
  }

  deleteVector(item: UserQueryVectorDto): void {
    if (this.isVectorActionInProgress || this.isVectorsLoading) {
      return;
    }

    if (!confirm('Удалить запрос?')) {
      return;
    }

    this.isVectorActionInProgress = true;
    this.vectorsErrorMessage = '';

    this.queryVectorService
      .delete(item.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.queryVectors = this.queryVectors.filter(vector => vector.id !== item.id);
          this.isVectorActionInProgress = false;
        },
        error: () => {
          this.vectorsErrorMessage = 'Не удалось удалить векторный запрос.';
          this.isVectorActionInProgress = false;
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

  get canSave(): boolean {
    return (
      !this.isSaving &&
      !this.isLoading &&
      !this.isVectorsLoading &&
      this.queryVectors.length > 0 &&
      this.form.valid
    );
  }

  private loadProfile(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.userCompanyService
      .getProfile()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: profile => this.handleProfileLoaded(profile, false),
        error: () => {
          this.errorMessage = 'Не удалось загрузить данные компании.';
          this.isLoading = false;
        }
      });
  }

  private loadVectors(): void {
    this.isVectorsLoading = true;
    this.vectorsErrorMessage = '';

    this.queryVectorService
      .getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: vectors => {
          this.queryVectors = vectors;
          this.isVectorsLoading = false;
        },
        error: () => {
          this.vectorsErrorMessage = 'Не удалось загрузить векторные запросы.';
          this.isVectorsLoading = false;
        }
      });
  }

  private handleProfileLoaded(profile: UserCompanyProfile, fromSave: boolean): void {
    this.availableRegions = profile.availableRegions ?? [];
    this.form.setValue({
      companyInfo: profile.companyInfo ?? '',
      regions: profile.regions ?? []
    });

    this.isLoading = false;
    this.isSaving = false;

    if (fromSave) {
      this.successMessage = 'Данные успешно сохранены.';
    }
  }
}
