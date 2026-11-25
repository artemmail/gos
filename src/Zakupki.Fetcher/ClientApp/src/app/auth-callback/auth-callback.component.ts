import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { take } from 'rxjs/operators';

import { AuthService } from '../services/AuthService.service';

@Component({
  selector: 'app-auth-callback',
  template: `<div class="loading-container" role="status"><mat-spinner diameter="32"></mat-spinner>Завершаем вход…</div>`,
})
export class AuthCallbackComponent implements OnInit {
  constructor(
    private route: ActivatedRoute,
    private auth: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const token = params.get('token');
      const requested = params.get('returnUrl') || '/tenders';
      const returnUrl = requested.startsWith('/') && !requested.startsWith('//') && !requested.includes('\\') ? requested : '/tenders';

      if (token) {
        this.auth.setAccessToken(token);
        this.router.navigateByUrl(returnUrl, { replaceUrl: true });
        return;
      }

      this.router.navigate(['/login'], { queryParams: { error: 'missing_token', returnUrl }, replaceUrl: true });
    });
  }
}
