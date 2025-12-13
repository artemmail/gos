import { Routes } from '@angular/router';

import { LoginComponent } from './LoginComponent/login.component';
import { AuthCallbackComponent } from './auth-callback/auth-callback.component';
import { NoticesComponent } from './notices/notices.component';
import { AuthGuard } from './services/auth.guard';
import { HomeRedirectGuard } from './services/home-redirect.guard';
import { NoticeDetailsComponent } from './notice-details/notice-details.component';
import { CompanyProfileComponent } from './company-profile/company-profile.component';
import { PresentationPageComponent } from './presentation-page/presentation-page.component';
import { TendersStartComponent } from './tenders-start/tenders-start.component';
import { MosNoticesComponent } from './mos-notices/mos-notices.component';
import { MosNoticeDetailsComponent } from './mos-notice-details/mos-notice-details.component';

export const appRoutes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'auth/callback', component: AuthCallbackComponent },
  { path: 'presentation', component: PresentationPageComponent },
  { path: 'tenders-start', component: TendersStartComponent },
  {
    path: '',
    component: PresentationPageComponent,
    canActivate: [HomeRedirectGuard]
  },
  {
    path: 'tenders',
    component: NoticesComponent,
    canActivate: [AuthGuard],
    data: {
      unauthRedirect: '/presentation'
    }
  },
  {
    path: 'company-profile',
    component: CompanyProfileComponent,
    canActivate: [AuthGuard]
  },
  {
    path: 'notices/:purchaseNumber',
    component: NoticeDetailsComponent
  },
  {
    path: 'notices-mos/:purchaseNumber',
    component: MosNoticeDetailsComponent
  },
  {
    path: 'notices-mos',
    component: MosNoticesComponent
  },
  {
    path: 'favorites',
    component: NoticesComponent,
    canActivate: [AuthGuard],
    data: { favorites: true }
  },
  { path: '**', redirectTo: '' }
];
