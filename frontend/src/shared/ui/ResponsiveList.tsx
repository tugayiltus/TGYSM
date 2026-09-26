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
      <ul className="flex flex-col gap-3 md:hidden">
        {items.map((item) => (
          <li key={getKey(item)} className="rounded-xl border border-slate-200 bg-white p-4">
            {renderCard(item)}
          </li>
        ))}
      </ul>
      <div className="hidden overflow-x-auto rounded-xl border border-slate-200 bg-white md:block">
        <table className="w-full text-left text-sm">
          <caption className="sr-only">{caption}</caption>
          <thead className="bg-slate-50 text-slate-600">
            <tr>
              {columns.map((column) => (
                <th key={column.key} scope="col" className="px-4 py-3 font-medium">
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={getKey(item)} className="border-t border-slate-100 align-top">
                {columns.map((column) => (
                  <td key={column.key} className="px-4 py-3">
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
