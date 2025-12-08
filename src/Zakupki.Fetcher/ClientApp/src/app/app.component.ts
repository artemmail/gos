import { Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Observable, Subscription, filter } from 'rxjs';

import { AuthService, UserInfo } from './services/AuthService.service';
import { YaMetrikaService } from './services/ya-metrika.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit, OnDestroy {
  readonly user$: Observable<UserInfo | null>;
  private readonly subscriptions = new Subscription();

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly yaMetrika: YaMetrikaService
  ) {
    this.user$ = this.auth.user$;
  }

  ngOnInit(): void {
    const navigationEnd$ = this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd)
    );

    this.subscriptions.add(
      navigationEnd$.subscribe((event) => {
        window.scrollTo({ top: 0 });
        this.yaMetrika.hit(event.urlAfterRedirects, 'YouScriptor');
      })
    );
  }

  onLogout(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => this.router.navigate(['/login'])
    });
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }
}
