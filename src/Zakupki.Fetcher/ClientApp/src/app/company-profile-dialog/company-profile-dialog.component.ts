import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';

import {
  RegionOption,
  UpdateUserCompanyProfileRequest,
  UserCompanyProfile,
  UserCompanyService
} from '../services/user-company.service';
import { QueryVectorService } from '../services/query-vector.service';
import { UserQueryVectorDto } from '../models/query-vector.models';
import { QueryVectorDialogComponent } from '../query-vectors/query-vector-dialog.component';
import { Okpd2CodeOption, Okpd2CodesService } from '../services/okpd2-codes.service';

@Component({
  selector: 'app-company-profile-dialog',
  templateUrl: './company-profile-dialog.component.html',
  styleUrls: ['./company-profile-dialog.component.css']
})
export class CompanyProfileDialogComponent implements OnInit, OnDestroy {
  form: FormGroup;
  availableRegions: RegionOption[] = [];
  availableOkpd2Codes: Okpd2CodeOption[] = [];
  queryVectors: UserQueryVectorDto[] = [];
  vectorColumns = ['query', 'vector', 'actions'];
  isLoading = false;
  isSaving = false;
  isVectorsLoading = false;
  isVectorActionInProgress = false;
  errorMessage = '';
  successMessage = '';
  vectorsErrorMessage = '';
  filteredRegions: RegionOption[] = [];
  regionSearchControl = new FormControl('');
  filteredOkpd2Codes: Okpd2CodeOption[] = [];
  okpd2SearchControl = new FormControl('');

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly dialogRef: MatDialogRef<CompanyProfileDialogComponent>,
    private readonly fb: FormBuilder,
    private readonly userCompanyService: UserCompanyService,
    private readonly queryVectorService: QueryVectorService,
    private readonly dialog: MatDialog,
    private readonly okpd2CodesService: Okpd2CodesService
  ) {
    this.form = this.fb.group({
      companyInfo: ['', [Validators.maxLength(8000)]],
      regions: [[] as number[]],
      okpd2Codes: [[] as string[]]
    });
  }

  ngOnInit(): void {
    this.loadProfile();
    this.loadVectors();
    this.loadOkpd2Codes();

    this.regionSearchControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(value => {
        this.filteredRegions = this.filterRegions(value);
      });

    this.okpd2SearchControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(value => {
        this.filteredOkpd2Codes = this.filterOkpd2Codes(value);
      });
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
      regions: this.form.value.regions ?? [],
      okpd2Codes: this.form.value.okpd2Codes ?? []
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
      this.form.valid
    );
  }

  refreshVectors(): void {
    if (this.isVectorsLoading || this.isVectorActionInProgress) {
      return;
    }

    this.loadVectors();
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

  private loadOkpd2Codes(): void {
    this.okpd2CodesService
      .getCodes()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: codes => {
          this.availableOkpd2Codes = codes;
          this.filteredOkpd2Codes = this.filterOkpd2Codes(this.okpd2SearchControl.value);
        },
        error: () => {
          // Keep the UI functional even if codes fail to load.
          this.availableOkpd2Codes = [];
          this.filteredOkpd2Codes = [];
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
      regions: profile.regions ?? [],
      okpd2Codes: profile.okpd2Codes ?? []
    });

    this.filteredRegions = this.filterRegions(this.regionSearchControl.value);
    this.filteredOkpd2Codes = this.filterOkpd2Codes(this.okpd2SearchControl.value);

    this.isLoading = false;
    this.isSaving = false;

    if (fromSave) {
      this.successMessage = 'Данные успешно сохранены.';
    }
  }

  handleRegionSelected(event: MatAutocompleteSelectedEvent): void {
    const selected = event.option.value as RegionOption;
    this.addRegion(selected.code);
    this.regionSearchControl.setValue('');
  }

  handleOkpd2Selected(event: MatAutocompleteSelectedEvent): void {
    const selected = event.option.value as Okpd2CodeOption;
    this.addOkpd2Code(selected.code);
    this.okpd2SearchControl.setValue('');
  }

  removeRegion(code: number): void {
    const regions = this.form.value.regions ?? [];
    this.form.patchValue({ regions: regions.filter((regionCode: number) => regionCode !== code) });
  }

  removeOkpd2Code(code: string): void {
    const okpd2Codes = this.form.value.okpd2Codes ?? [];
    this.form.patchValue({ okpd2Codes: okpd2Codes.filter((okpd2Code: string) => okpd2Code !== code) });
  }

  getRegionName(code: number): string | undefined {
    return this.availableRegions.find(region => region.code === code)?.name;
  }

  getOkpd2Name(code: string): string | undefined {
    return this.availableOkpd2Codes.find(item => item.code === code)?.name;
  }

  private addRegion(code: number): void {
    const regions = this.form.value.regions ?? [];
    if (!regions.includes(code)) {
      this.form.patchValue({ regions: [...regions, code] });
    }
  }

  private addOkpd2Code(code: string): void {
    const okpd2Codes = this.form.value.okpd2Codes ?? [];
    if (!okpd2Codes.includes(code)) {
      this.form.patchValue({ okpd2Codes: [...okpd2Codes, code] });
    }
  }

  private filterRegions(value: string | RegionOption | null): RegionOption[] {
    const query = (typeof value === 'string' ? value : value?.name ?? '').trim().toLowerCase();

    if (!query) {
      return this.availableRegions;
    }

    return this.availableRegions.filter(
      region =>
        region.name.toLowerCase().includes(query) || region.code.toString().includes(query)
    );
  }

  private filterOkpd2Codes(value: string | Okpd2CodeOption | null): Okpd2CodeOption[] {
    const query = (typeof value === 'string' ? value : value?.name ?? '').trim().toLowerCase();

    if (!query) {
      return this.availableOkpd2Codes.slice(0, 50);
    }

    return this.availableOkpd2Codes
      .filter(code => code.name.toLowerCase().includes(query) || code.code.toLowerCase().includes(query))
      .slice(0, 50);
  }
}
