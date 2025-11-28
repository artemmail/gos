import { Injectable, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';

import { NoticeAnalysisResponse } from './notice-analysis.service';
import { AuthService } from './AuthService.service';

@Injectable({ providedIn: 'root' })
export class NoticeAnalysisNotificationsService implements OnDestroy {
  private connection: HubConnection | null = null;
  private readonly analysisUpdatesSubject = new Subject<NoticeAnalysisResponse>();
  readonly analysisUpdates$: Observable<NoticeAnalysisResponse> = this.analysisUpdatesSubject.asObservable();
  private isConnecting = false;

  constructor(private readonly authService: AuthService) {}

  connect(): void {
    if (this.connection || this.isConnecting) {
      return;
    }

    this.isConnecting = true;

    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/notice-analysis', {
        accessTokenFactory: () => this.authService.getAccessToken() ?? '',
        transport: undefined
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.connection.on('AnalysisUpdated', response => {
      this.analysisUpdatesSubject.next(response as NoticeAnalysisResponse);
    });

    this.connection.start().catch(() => {
      this.disposeConnection();
    }).finally(() => {
      this.isConnecting = false;
    });
  }

  disconnect(): void {
    if (!this.connection) {
      return;
    }

    this.connection.stop().finally(() => this.disposeConnection());
  }

  ngOnDestroy(): void {
    this.disconnect();
    this.analysisUpdatesSubject.complete();
  }

  private disposeConnection(): void {
    this.connection = null;
  }
}
