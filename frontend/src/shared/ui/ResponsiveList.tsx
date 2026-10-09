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

/** Inside a list card: list-group rows below 768 px, a borderless table above; the hidden variant is display:none, so screen readers see one list. */
export function ResponsiveList<T>({ items, columns, getKey, renderCard, caption }: ResponsiveListProps<T>) {
  return (
    <>
      <ul className="list-group list-group-flush d-md-none">
        {items.map((item) => (
          <li key={getKey(item)} className="list-group-item py-3">
            {renderCard(item)}
          </li>
        ))}
      </ul>
      <div className="d-none d-md-block table-responsive">
        <table className="table table-hover align-middle mb-0">
          <caption className="visually-hidden">{caption}</caption>
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column.key} scope="col" className="text-body-secondary small text-uppercase fw-semibold">
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
