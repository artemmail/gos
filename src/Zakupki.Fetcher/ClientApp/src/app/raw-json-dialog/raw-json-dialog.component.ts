import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NestedTreeControl } from '@angular/cdk/tree';
import { MatTreeNestedDataSource } from '@angular/material/tree';

import { JsonTreeNode, RawJsonDialogData } from '../models/raw-json.models';

@Component({
  selector: 'app-raw-json-dialog',
  templateUrl: './raw-json-dialog.component.html',
  styleUrls: ['./raw-json-dialog.component.css']
})
export class RawJsonDialogComponent implements OnInit {
  readonly treeControl = new NestedTreeControl<JsonTreeNode>(node => node.children ?? []);
  readonly dataSource = new MatTreeNestedDataSource<JsonTreeNode>();
  parseError = '';
  isFullscreen = false;

  private readonly fullscreenPanelClass = 'raw-json-dialog-panel-fullscreen';

  constructor(
    private readonly dialogRef: MatDialogRef<RawJsonDialogComponent>,
    private readonly snackBar: MatSnackBar,
    @Inject(MAT_DIALOG_DATA) public readonly data: RawJsonDialogData
  ) {}

  ngOnInit(): void {
    this.initializeTree();
  }

  close(): void {
    this.dialogRef.close();
  }

  toggleFullscreen(): void {
    this.isFullscreen = !this.isFullscreen;

    if (this.isFullscreen) {
      this.dialogRef.addPanelClass(this.fullscreenPanelClass);
      this.dialogRef.updatePosition({ top: '0', left: '0' });
    } else {
      this.dialogRef.removePanelClass(this.fullscreenPanelClass);
      this.dialogRef.updatePosition();
    }
  }

  async copyRawJson(): Promise<void> {
    if (!this.data.rawJson) {
      return;
    }

    try {
      const parsed = JSON.parse(this.data.rawJson);
      const sanitized = this.cloneWithCryptoSignsNull(parsed);
      const text = JSON.stringify(sanitized, null, 2);

      await navigator.clipboard.writeText(text);
      this.snackBar.open('JSON скопирован в буфер обмена.', 'Закрыть', { duration: 3000 });
    } catch {
      this.snackBar.open('Не удалось скопировать JSON.', 'Закрыть', { duration: 4000 });
    }
  }

  hasChild = (_: number, node: JsonTreeNode) => !!node.children && node.children.length > 0;

  private initializeTree(): void {
    if (!this.data.rawJson) {
      this.parseError = 'JSON данные отсутствуют.';
      return;
    }

    try {
      const parsed = JSON.parse(this.data.rawJson);
      this.dataSource.data = this.buildTree(parsed);
      this.treeControl.dataNodes = this.dataSource.data;
      this.treeControl.expandAll();
    } catch {
      this.parseError = 'Не удалось разобрать JSON.';
    }
  }

  private buildTree(value: unknown): JsonTreeNode[] {
    if (Array.isArray(value)) {
      return value.map((item, index) => this.createNode(item, `[${index}]`));
    }

    if (this.isObject(value)) {
      return Object.entries(value as Record<string, unknown>).map(([key, val]) =>
        this.createNode(val, key)
      );
    }

    return [this.createNode(value, 'Значение')];
  }

  private createNode(value: unknown, key: string): JsonTreeNode {
    if (Array.isArray(value)) {
      return {
        key,
        type: 'array',
        children: value.map((item, index) => this.createNode(item, `[${index}]`))
      };
    }

    if (this.isObject(value)) {
      return {
        key,
        type: 'object',
        children: Object.entries(value as Record<string, unknown>).map(([childKey, childValue]) =>
          this.createNode(childValue, childKey)
        )
      };
    }

    return {
      key,
      type: 'value',
      value: this.formatValue(value)
    };
  }

  private isObject(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
  }

  private formatValue(value: unknown): string {
    if (typeof value === 'string') {
      return `"${value}"`;
    }

    if (value === null) {
      return 'null';
    }

    return String(value);
  }

  private cloneWithCryptoSignsNull(value: unknown): unknown {
    if (Array.isArray(value)) {
      return value.map(item => this.cloneWithCryptoSignsNull(item));
    }

    if (this.isObject(value)) {
      const result: Record<string, unknown> = {};

      for (const [key, childValue] of Object.entries(value)) {
        result[key] = key === 'cryptoSigns' ? null : this.cloneWithCryptoSignsNull(childValue);
      }

      return result;
    }

    return value;
  }
}
