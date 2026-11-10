/**
 * @input Items, column definitions, a card renderer for phones, and an accessible name.
 * @output A table on wide screens, or one list of cards on phones.
 * @position The main content region of a list page, under its `ListToolbar`.
 */
import { List, ListItem } from '@astryxdesign/core/List';
import { Table, type TableColumn } from '@astryxdesign/core/Table';
import type { ReactNode } from 'react';

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
}

/** Astryx `Table` needs plain record rows, so each item travels inside one. */
interface Row<T> extends Record<string, unknown> {
  rowKey: string;
  item: T;
}

export function DataList<T>({ items, columns, getKey, renderCard, caption }: DataListProps<T>) {
  const isPhone = useIsPhone();
  if (isPhone) {
    return (
      <List aria-label={caption} hasDividers>
        {items.map((item) => (
          <ListItem key={getKey(item)} label={renderCard(item)} />
        ))}
      </List>
    );
  }
  const rows: Row<T>[] = items.map((item) => ({ rowKey: getKey(item), item }));
  const tableColumns: TableColumn<Row<T>>[] = columns.map((column) => ({ key: column.key, header: column.header, renderCell: (row) => column.renderCell(row.item) }));
  return <Table aria-label={caption} data={rows} columns={tableColumns} idKey="rowKey" hasHover />;
}
