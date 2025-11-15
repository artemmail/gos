import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

import {
  UpdateUserCompanyProfileRequest,
  UserCompanyProfile,
  UserCompanyService
} from '../services/user-company.service';

@Component({
  selector: 'app-company-profile-dialog',
  templateUrl: './company-profile-dialog.component.html',
  styleUrls: ['./company-profile-dialog.component.css']
})
export class CompanyProfileDialogComponent implements OnInit, OnDestroy {
  form: FormGroup;
  availableRegions: string[] = [];
  isLoading = false;
  isSaving = false;
  errorMessage = '';
  successMessage = '';

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly dialogRef: MatDialogRef<CompanyProfileDialogComponent>,
    private readonly fb: FormBuilder,
    private readonly userCompanyService: UserCompanyService
  ) {
    this.form = this.fb.group({
      companyInfo: ['', [Validators.maxLength(8000)]],
      regions: [[] as string[]]
    });
  }

  ngOnInit(): void {
    this.loadProfile();
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
