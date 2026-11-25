import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Title } from '@angular/platform-browser';
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
  menuOpen = false;
  currentPage = 'Поиск закупок';
  @ViewChild('menuButton') menuButton?: ElementRef<HTMLButtonElement>;
  readonly user$: Observable<UserInfo | null>;
  private readonly subscriptions = new Subscription();

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly yaMetrika: YaMetrikaService,
    private readonly title: Title
  ) {
    this.user$ = this.auth.user$;
  }

  ngOnInit(): void {
    const navigationEnd$ = this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd)
    );

    this.subscriptions.add(
      navigationEnd$.subscribe((event) => {
        const fragment = this.router.parseUrl(event.urlAfterRedirects).fragment;
        if (fragment) requestAnimationFrame(() => document.getElementById(fragment)?.scrollIntoView());
        else window.scrollTo({ top: 0 });
        this.closeMenu();
        const path = event.urlAfterRedirects.split(/[?#]/)[0];
        const titles: Record<string, string> = {
          '/tenders': 'Поиск закупок', '/favorites': 'Избранное', '/company-profile': 'Моя компания',
          '/notices-fab': 'Фабрикант', '/login': 'Вход', '/presentation': 'О сервисе'
        };
        this.currentPage = path.startsWith('/notices/') ? 'Карточка закупки' : titles[path] || 'Тендеры с пониманием вашего бизнеса';
        this.title.setTitle(`${this.currentPage} — Артем Тендер`);
        this.yaMetrika.hit(event.urlAfterRedirects, this.title.getTitle());
      })
    );
  }

  onLogout(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => this.router.navigate(['/login'])
    });
  }

  @HostListener('document:keydown.escape')
  closeMenu(): void {
    if (this.menuOpen) {
      this.menuOpen = false;
      this.menuButton?.nativeElement.focus();
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }
}
