import { LOCALE_ID, NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule, provideHttpClient, withInterceptors } from '@angular/common/http';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { registerLocaleData } from '@angular/common';
import localeRu from '@angular/common/locales/ru';

import { AppComponent } from './app.component';
import { MaterialModule } from './material.module';
import { AttachmentsDialogComponent } from './attachments-dialog/attachments-dialog.component';
import { RawJsonDialogComponent } from './raw-json-dialog/raw-json-dialog.component';
import { NoticesComponent } from './notices/notices.component';
import { AuthCallbackComponent } from './auth-callback/auth-callback.component';
import { appRoutes } from './app.routes';
import { authInterceptor } from './services/AuthInterceptor';
import { CompanyProfileComponent } from './company-profile/company-profile.component';
import { NoticeAnalysisDialogComponent } from './notice-analysis-dialog/notice-analysis-dialog.component';
import { QueryVectorsComponent } from './query-vectors/query-vectors.component';
import { QueryVectorDialogComponent } from './query-vectors/query-vector-dialog.component';
import { NoticeCommonInfoComponent } from './notice-common-info/notice-common-info.component';
import { NoticeDetailsComponent } from './notice-details/notice-details.component';
import { PresentationPageComponent } from './presentation-page/presentation-page.component';
import { TendersStartComponent } from './tenders-start/tenders-start.component';
import { MosNoticesComponent } from './mos-notices/mos-notices.component';

registerLocaleData(localeRu);

@NgModule({
  declarations: [
    AppComponent,
    AttachmentsDialogComponent,
    RawJsonDialogComponent,
    NoticesComponent,
    AuthCallbackComponent,
    CompanyProfileComponent,
    NoticeAnalysisDialogComponent,
    QueryVectorsComponent,
    QueryVectorDialogComponent,
    NoticeCommonInfoComponent,
    NoticeDetailsComponent,
    PresentationPageComponent,
    TendersStartComponent,
    MosNoticesComponent
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
    provideHttpClient(withInterceptors([authInterceptor])),
    { provide: LOCALE_ID, useValue: 'ru-RU' }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
