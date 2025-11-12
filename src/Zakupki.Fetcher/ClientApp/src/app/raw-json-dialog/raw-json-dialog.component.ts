import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
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

  constructor(
    private readonly dialogRef: MatDialogRef<RawJsonDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public readonly data: RawJsonDialogData
  ) {}

  ngOnInit(): void {
    this.initializeTree();
  }

  close(): void {
    this.dialogRef.close();
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
}
