export interface RawJsonDialogData {
  purchaseNumber: string;
  entryName: string;
  rawJson: string;
}

export interface JsonTreeNode {
  key: string;
  type: 'object' | 'array' | 'value';
  value?: string;
  children?: JsonTreeNode[];
}
