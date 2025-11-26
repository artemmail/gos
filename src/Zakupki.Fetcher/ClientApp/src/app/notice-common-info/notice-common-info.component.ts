import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

import { NoticeAttachment } from '../models/attachment.models';
import { NoticeCommonInfo } from '../models/notice.models';

@Component({
  selector: 'app-notice-common-info',
  templateUrl: './notice-common-info.component.html',
  styleUrls: ['./notice-common-info.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NoticeCommonInfoComponent {
  @Input() notice!: NoticeCommonInfo;
  @Input() attachments: NoticeAttachment[] | null = null;

  get displayAttachments(): Array<{
    fileName: string;
    url: string | null;
    docKindName: string | null;
    docDescription: string | null;
    docDate: string | null;
    fileSize: number | null;
  }> {
    const fromNotice = this.notice?.attachmentsInfo?.items?.map(item => ({
      fileName: item.fileName,
      url: item.url ?? null,
      docKindName: item.docKindInfo?.name ?? null,
      docDescription: item.docDescription ?? null,
      docDate: item.docDate ?? null,
      fileSize: item.fileSize ?? null
    })) ?? [];

    if (fromNotice.length > 0) {
      return fromNotice;
    }

    return (this.attachments ?? []).map(att => ({
      fileName: att.fileName,
      url: att.url,
      docKindName: att.documentKindName,
      docDescription: att.description,
      docDate: att.documentDate,
      fileSize: att.fileSize ?? null
    }));
  }

  getMaxPriceInfo(notice: NoticeCommonInfo): { maxPrice: number; currency?: string | null } | null {
    const contractPrice = notice.notificationInfo?.contractConditionsInfo?.maxPriceInfo;
    if (contractPrice?.maxPrice != null) {
      return {
        maxPrice: contractPrice.maxPrice,
        currency: contractPrice.currency?.name || contractPrice.currency?.code || null
      };
    }

    if (notice.maxPrice != null) {
      return { maxPrice: notice.maxPrice, currency: null };
    }

    const customerRequirementPrice = notice.notificationInfo?.customerRequirementsInfo?.items
      ?.find(item => item.innerContractConditionsInfo?.maxPriceInfo?.maxPrice != null)
      ?.innerContractConditionsInfo?.maxPriceInfo;

    if (customerRequirementPrice?.maxPrice != null) {
      return {
        maxPrice: customerRequirementPrice.maxPrice,
        currency: null
      };
    }

    return null;
  }

  hasContractConditions(notice: NoticeCommonInfo): boolean {
    const commonConditionsDefined =
      notice.notificationInfo?.contractConditionsInfo?.isOneSideRejectionSt95 !== undefined;

    const customerConditionsDefined =
      notice.notificationInfo?.customerRequirementsInfo?.items?.some(
        cr => cr.innerContractConditionsInfo?.isOneSideRejectionSt95 !== undefined
      ) ?? false;

    const hasWarrantyInfo =
      notice.notificationInfo?.customerRequirementsInfo?.items?.some(
        cr => !!(cr.warrantyInfo?.warrantyServiceRequirement || cr.warrantyInfo?.warrantyTerm)
      ) ?? false;

    const hasDeliveryPlaces =
      notice.notificationInfo?.customerRequirementsInfo?.items?.some(
        cr => cr.innerContractConditionsInfo?.deliveryPlacesInfo?.items?.length
      ) ?? false;

    return commonConditionsDefined || customerConditionsDefined || hasWarrantyInfo || hasDeliveryPlaces;
  }

  /** "2025-11-24+03:00" -> "24.11.2025" */
  formatRawDate(raw?: string | null): string | null {
    if (!raw) {
      return null;
    }
    const datePart = raw.split('+')[0];
    const parts = datePart.split('-').map(x => +x);
    if (parts.length !== 3 || !parts[0] || !parts[1] || !parts[2]) {
      return raw;
    }
    const [year, month, day] = parts;
    return `${day.toString().padStart(2, '0')}.` +
           `${month.toString().padStart(2, '0')}.` +
           `${year}`;
  }

  /** ISO string -> "dd.MM.yyyy HH:mm" */
  formatDateTime(value?: string | null): string | null {
    if (!value) {
      return null;
    }
    const d = new Date(value);
    if (isNaN(d.getTime())) {
      return value;
    }
    const dd = d.getDate().toString().padStart(2, '0');
    const mm = (d.getMonth() + 1).toString().padStart(2, '0');
    const yyyy = d.getFullYear();
    const hh = d.getHours().toString().padStart(2, '0');
    const min = d.getMinutes().toString().padStart(2, '0');
    return `${dd}.${mm}.${yyyy} ${hh}:${min}`;
  }

  /** bytes -> "54,3 КБ" */
  formatFileSize(bytes?: number | null): string | null {
    if (bytes == null) {
      return null;
    }
    if (bytes < 1024) {
      return `${bytes} Б`;
    }
    const kb = bytes / 1024;
    return `${kb.toFixed(1)} КБ`;
  }
}
