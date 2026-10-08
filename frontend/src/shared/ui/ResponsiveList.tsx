import type { ReactNode } from 'react';

export interface Column<T> {
  key: string;
  header: string;
  renderCell: (item: T) => ReactNode;
}

export interface ResponsiveListProps<T> {
  items: readonly T[];
  columns: readonly Column<T>[];
  getKey: (item: T) => string;
  renderCard: (item: T) => ReactNode;
  caption: string;
}

/** Cards below 768 px, a table above; the hidden variant is display:none, so screen readers see one list. */
export function ResponsiveList<T>({ items, columns, getKey, renderCard, caption }: ResponsiveListProps<T>) {
  return (
    <>
      <ul className="list-unstyled d-flex flex-column gap-3 mb-0 d-md-none">
        {items.map((item) => (
          <li key={getKey(item)} className="card card-body">
            {renderCard(item)}
          </li>
        ))}
      </ul>
      <div className="d-none d-md-block table-responsive border rounded bg-body">
        <table className="table table-hover align-top mb-0">
          <caption className="visually-hidden">{caption}</caption>
          <thead className="table-light">
            <tr>
              {columns.map((column) => (
                <th key={column.key} scope="col">
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={getKey(item)}>
                {columns.map((column) => (
                  <td key={column.key}>
                    {column.renderCell(item)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
