import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { take } from 'rxjs/operators';

import { AuthService } from '../services/AuthService.service';

@Component({
  selector: 'app-auth-callback',
  template: `<p>Logging in...</p>`,
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
      const returnUrl = params.get('returnUrl') || '/';

      if (token) {
        this.auth.setAccessToken(token);
        this.router.navigateByUrl(returnUrl);
        return;
      }

      this.router.navigate(['/login'], { queryParams: { error: 'missing_token' } });
    });
  }
}
