import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';

import { AuthService } from './AuthService.service';

@Injectable({ providedIn: 'root' })
export class HomeRedirectGuard implements CanActivate {
  constructor(
    private readonly auth: AuthService,
    private readonly router: Router
  ) {}

  canActivate(): Observable<boolean | UrlTree> {
    return this.auth.user$.pipe(
      take(1),
      map(user => {
        if (user) {
          return this.router.createUrlTree(['/tenders']);
        }

        return true;
      })
    );
  }
}
