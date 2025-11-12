export interface NoticeAttachment {
  id: string;
  publishedContentId: string;
  fileName: string;
  fileSize: number | null;
  description: string | null;
  documentDate: string | null;
  documentKindCode: string | null;
  documentKindName: string | null;
  url: string | null;
  sourceFileName: string | null;
  insertedAt: string;
  lastSeenAt: string;
  hasBinaryContent: boolean;
}

export interface AttachmentDownloadResult {
  total: number;
  downloaded: number;
  failed: number;
}

export interface AttachmentDialogData {
  noticeId: string;
  purchaseNumber: string;
  entryName: string;
}
