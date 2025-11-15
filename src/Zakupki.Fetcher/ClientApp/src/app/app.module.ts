import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule, provideHttpClient, withInterceptors } from '@angular/common/http';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { AppComponent } from './app.component';
import { MaterialModule } from './material.module';
import { AttachmentsDialogComponent } from './attachments-dialog/attachments-dialog.component';
import { RawJsonDialogComponent } from './raw-json-dialog/raw-json-dialog.component';
import { NoticesComponent } from './notices/notices.component';
import { AuthCallbackComponent } from './auth-callback/auth-callback.component';
import { appRoutes } from './app.routes';
import { authInterceptor } from './services/AuthInterceptor';

@NgModule({
  declarations: [
    AppComponent,
    AttachmentsDialogComponent,
    RawJsonDialogComponent,
    NoticesComponent,
    AuthCallbackComponent
  ],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    HttpClientModule,
    ReactiveFormsModule,
    MaterialModule,
    RouterModule.forRoot(appRoutes)
  ],
  providers: [
    provideHttpClient(withInterceptors([authInterceptor]))
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
