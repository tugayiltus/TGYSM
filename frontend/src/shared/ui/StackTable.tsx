import type { ReactNode } from 'react';

export interface StackTableProps {
  caption: string;
  head: ReactNode;
  children: ReactNode;
}

/** A table that becomes one card per row below 992 px (`tgysm-stack-table`); cells carry `data-label`. */
export function StackTable({ caption, head, children }: StackTableProps) {
  return (
    <div className="table-responsive">
      <table className="table align-middle mb-0 tgysm-stack-table">
        <caption className="visually-hidden">{caption}</caption>
        <thead>{head}</thead>
        <tbody>{children}</tbody>
      </table>
    </div>
  );
}
