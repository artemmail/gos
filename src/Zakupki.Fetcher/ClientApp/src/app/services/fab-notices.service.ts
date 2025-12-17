import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { FabNoticeListResponse, FabNoticeQuery, FabrikantLot, FabrikantProcedure } from '../models/fab-notice.models';
import { MosNoticeDetails } from '../models/mos-notice.models';

@Injectable({ providedIn: 'root' })
export class FabNoticesService {
  private readonly baseUrl = '/api/notices/fab';

  constructor(private readonly http: HttpClient) { }

  getNotices(query: FabNoticeQuery): Observable<FabNoticeListResponse> {
    let params = new HttpParams()
      .set('page', query.page.toString())
      .set('pageSize', query.pageSize.toString());

    if (query.search) {
      params = params.set('search', query.search);
    }

    if (query.sortField) {
      params = params.set('sortField', query.sortField);
    }

    if (query.sortDirection) {
      params = params.set('sortDirection', query.sortDirection);
    }

    return this.http.get<FabNoticeListResponse>(this.baseUrl, { params });
  }

  getNotice(procedureNumber: string): Observable<FabrikantProcedure> {
    return this.http
      .get<MosNoticeDetails>(`/api/notices/mos/${encodeURIComponent(procedureNumber)}`)
      .pipe(map(response => this.parseNoticeResponse(response)));
  }

  refreshNotice(procedureNumber: string): Observable<FabrikantProcedure> {
    return this.http
      .post<MosNoticeDetails>(`/api/notices/fab/${encodeURIComponent(procedureNumber)}/refresh`, {})
      .pipe(map(response => this.parseNoticeResponse(response)));
  }

  private mapFabrikantProcedure(
    raw: Record<string, unknown>,
    rawJson: string,
    noticeId: string,
    purchaseNumber: string
  ): FabrikantProcedure {
    const lots = Array.isArray(raw['Lots']) ? raw['Lots'] : [];
    const documents = Array.isArray(raw['Documents']) ? raw['Documents'] : [];

    return {
      noticeId: noticeId ?? '',
      externalId: (raw['ExternalId'] as string) ?? '',
      procedureNumber: (raw['ProcedureNumber'] as string) ?? purchaseNumber ?? '',
      lawSection: (raw['LawSection'] as string) ?? '',
      title: (raw['Title'] as string) ?? '',
      procedureType: (raw['ProcedureType'] as string) ?? '',
      status: (raw['Status'] as string) ?? '',

      organizerName: (raw['OrganizerName'] as string) ?? '',
      organizerInn: (raw['OrganizerInn'] as string) ?? '',
      organizerKpp: (raw['OrganizerKpp'] as string) ?? '',
      organizerAddress: (raw['OrganizerAddress'] as string) ?? '',

      customerName: (raw['CustomerName'] as string) ?? '',
      customerFullName: (raw['CustomerFullName'] as string) ?? '',
      customerInn: (raw['CustomerInn'] as string) ?? '',
      customerKpp: (raw['CustomerKpp'] as string) ?? '',

      publishDate: (raw['PublishDate'] as string) ?? null,
      applyStartDate: (raw['ApplyStartDate'] as string) ?? null,
      applyEndDate: (raw['ApplyEndDate'] as string) ?? null,
      resultDate: (raw['ResultDate'] as string) ?? null,

      nmck: (raw['Nmck'] as number) ?? null,
      currency: (raw['Currency'] as string) ?? '',

      okpd2: (raw['Okpd2'] as string) ?? '',
      okved2: (raw['Okved2'] as string) ?? '',
      itemName: (raw['ItemName'] as string) ?? '',
      quantity: (raw['Quantity'] as number) ?? null,
      unit: (raw['Unit'] as string) ?? '',
      deliveryAddress: (raw['DeliveryAddress'] as string) ?? '',
      deliveryTerm: (raw['DeliveryTerm'] as string) ?? '',
      paymentTerms: (raw['PaymentTerms'] as string) ?? '',
      applicationSecurity: (raw['ApplicationSecurity'] as number) ?? null,
      contractSecurity: (raw['ContractSecurity'] as number) ?? null,

      lots: (lots as Record<string, unknown>[]).map(lot => this.mapFabrikantLot(lot)),
      documents: (documents as Record<string, unknown>[]).map(doc => ({
        url: (doc['Url'] as string) ?? (doc['URL'] as string) ?? '',
        fileName: (doc['FileName'] as string) ?? ''
      })),

      rawJson: rawJson ?? '',
      rawHtml: (raw['RawHtml'] as string) ?? ''
    };
  }

  private mapFabrikantLot(lot: Record<string, unknown>): FabrikantLot {
    return {
      number: (lot['Number'] as string) ?? '',
      name: (lot['Name'] as string) ?? '',
      startPrice: (lot['StartPrice'] as number) ?? null,
      currency: (lot['Currency'] as string) ?? '',
      quantity: (lot['Quantity'] as number) ?? null,
      unit: (lot['Unit'] as string) ?? '',
      status: (lot['Status'] as string) ?? '',
      deliveryAddress: (lot['DeliveryAddress'] as string) ?? '',
      deliveryTerm: (lot['DeliveryTerm'] as string) ?? '',
      paymentTerms: (lot['PaymentTerms'] as string) ?? '',
      additionalFields: (lot['AdditionalFields'] as Record<string, string>) ?? {},
      rawRow: (lot['RawRow'] as string) ?? ''
    };
  }

  private parseNoticeResponse(response: MosNoticeDetails): FabrikantProcedure {
    if (!response?.rawJson) {
      throw new Error('Ответ не содержит данных извещения Fabrikant.');
    }

    try {
      const rawNotice = JSON.parse(response.rawJson) as Record<string, unknown>;
      return this.mapFabrikantProcedure(
        rawNotice,
        response.rawJson,
        response.id,
        response.purchaseNumber
      );
    } catch (error) {
      throw new Error('Не удалось обработать данные извещения Fabrikant.');
    }
  }
}
