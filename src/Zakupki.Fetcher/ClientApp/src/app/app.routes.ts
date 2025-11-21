import { Routes } from '@angular/router';

import { LoginComponent } from './LoginComponent/login.component';
import { AuthCallbackComponent } from './auth-callback/auth-callback.component';
import { NoticesComponent } from './notices/notices.component';
import { AuthGuard } from './services/auth.guard';

export const appRoutes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'auth/callback', component: AuthCallbackComponent },
  {
    path: '',
    component: NoticesComponent,
    canActivate: [AuthGuard]
  },
  {
    path: 'favorites',
    component: NoticesComponent,
    canActivate: [AuthGuard],
    data: { favorites: true }
  },
  { path: '**', redirectTo: '' }
];
