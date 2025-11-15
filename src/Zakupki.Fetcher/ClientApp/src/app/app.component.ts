import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';

import { AuthService, UserInfo } from './services/AuthService.service';
import { CompanyProfileDialogComponent } from './company-profile-dialog/company-profile-dialog.component';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  readonly user$: Observable<UserInfo | null>;

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly dialog: MatDialog
  ) {
    this.user$ = this.auth.user$;
  }

  onLogout(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => this.router.navigate(['/login'])
    });
  }

  openCompanyProfileDialog(): void {
    this.dialog.open(CompanyProfileDialogComponent, {
      width: '640px'
    });
  }
}
