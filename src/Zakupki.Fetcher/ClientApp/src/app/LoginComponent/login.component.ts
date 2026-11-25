import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

@Component({
  standalone: true,
  selector: 'app-login',
  imports: [CommonModule, RouterModule, MatIconModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
  authError = false;
  authErrorMessage = '';
  isRedirecting = false;
  private returnUrl = '/tenders';

  constructor(private readonly route: ActivatedRoute) {}

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const requested = params.get('returnUrl');
    if (requested?.startsWith('/') && !requested.startsWith('//') && !requested.includes('\\')) {
      this.returnUrl = requested;
    }
    if (params.has('error')) {
      this.authError = true;
      this.authErrorMessage = 'Не удалось завершить вход. Попробуйте ещё раз или выберите другой способ.';
    }
  }

  loginWithGoogle(): void { this.redirectToProvider('google'); }
  loginWithYandex(): void { this.redirectToProvider('yandex'); }

  private redirectToProvider(provider: 'google' | 'yandex'): void {
    if (this.isRedirecting) return;
    this.isRedirecting = true;
    const callbackUrl = new URL('/auth/callback', window.location.origin);
    callbackUrl.searchParams.set('returnUrl', this.returnUrl);
    window.location.href = `/api/account/signin-${provider}?returnUrl=${encodeURIComponent(callbackUrl.toString())}`;
  }
}
