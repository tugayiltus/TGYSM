/**
 * @input Items, column definitions, a card renderer for phones, and an accessible name.
 * @output A table on wide screens, or one list of cards on phones.
 * @position The main content region of a list page, under its `ListToolbar`.
 */
import { CheckboxInput } from '@astryxdesign/core/CheckboxInput';
import { HStack } from '@astryxdesign/core/HStack';
import { List, ListItem } from '@astryxdesign/core/List';
import { StackItem } from '@astryxdesign/core/Stack';
import { Table, useTableSelection, type TableColumn } from '@astryxdesign/core/Table';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { useIsPhone } from './useIsPhone';

export interface DataColumn<T> {
  key: string;
  header: string;
  renderCell: (item: T) => ReactNode;
}

export interface DataListProps<T> {
  items: readonly T[];
  columns: readonly DataColumn<T>[];
  getKey: (item: T) => string;
  renderCard: (item: T) => ReactNode;
  /** Accessible name of the table or list. */
  caption: string;
  /** Row selection with a checkbox per row (and select-all on wide screens). */
  selection?: DataSelection<T>;
}

export interface DataSelection<T> {
  selected: ReadonlySet<string>;
  onChange: (next: Set<string>) => void;
  /** Names the row in its checkbox label ("<name> öğesini seç"), e.g. the asset tag. */
  rowLabel: (item: T) => string;
}

/** Astryx `Table` needs plain record rows, so each item travels inside one. */
interface Row<T> extends Record<string, unknown> {
  rowKey: string;
  item: T;
}

export function DataList<T>({ items, columns, getKey, renderCard, caption, selection }: DataListProps<T>) {
  const { t } = useTranslation();
  const isPhone = useIsPhone();
  const isSelected = (key: string) => selection?.selected.has(key) ?? false;
  const toggle = (key: string, on: boolean) => {
    const next = new Set(selection?.selected);
    if (on) {
      next.add(key);
    } else {
      next.delete(key);
    }
    selection?.onChange(next);
  };
  const selectionPlugin = useTableSelection<Row<T>>({
    getIsItemSelected: (row) => isSelected(row.rowKey),
    onSelectItem: ({ item, isSelected: on }) => toggle(item.rowKey, on),
    onSelectAll: ({ isAllSelected }) => selection?.onChange(new Set(isAllSelected ? items.map(getKey) : [])),
    getIsAllSelected: () => items.length > 0 && items.every((item) => isSelected(getKey(item))),
    getIsIndeterminate: () => items.some((item) => isSelected(getKey(item))) && !items.every((item) => isSelected(getKey(item))),
    getRowLabel: (row) => selection?.rowLabel(row.item) ?? row.rowKey,
  });
  if (isPhone) {
    return (
      <List aria-label={caption} hasDividers>
        {items.map((item) => (
          <ListItem
            key={getKey(item)}
            label={
              selection === undefined ? (
                renderCard(item)
              ) : (
                <HStack gap={3} vAlign="start">
                  <CheckboxInput label={t('list.selectRow', { label: selection.rowLabel(item) })} isLabelHidden value={isSelected(getKey(item))} onChange={(on) => toggle(getKey(item), on)} />
                  <StackItem size="fill">{renderCard(item)}</StackItem>
                </HStack>
              )
            }
          />
        ))}
      </List>
    );
  }
  const rows: Row<T>[] = items.map((item) => ({ rowKey: getKey(item), item }));
  const tableColumns: TableColumn<Row<T>>[] = columns.map((column) => ({ key: column.key, header: column.header, renderCell: (row) => column.renderCell(row.item) }));
  return (
    <Table
      aria-label={caption}
      data={rows}
      columns={tableColumns}
      idKey="rowKey"
      hasHover
      plugins={selection === undefined ? undefined : { selection: selectionPlugin }}
    />
  );
}
