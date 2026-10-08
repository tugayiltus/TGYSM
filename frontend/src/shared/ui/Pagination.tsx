import { useTranslation } from 'react-i18next';

import { Button } from './Button';

const FIRST_PAGE = 1;

export interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, pageSize, totalCount, onPageChange }: PaginationProps) {
  const { t } = useTranslation();
  if (totalCount === 0) {
    return null;
  }
  const from = (page - FIRST_PAGE) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);
  const lastPage = Math.ceil(totalCount / pageSize);

  return (
    <nav aria-label={t('pagination.label')} className="d-flex flex-wrap align-items-center justify-content-between gap-2 small">
      <p className="mb-0 text-body-secondary">{t('pagination.summary', { from, to, total: totalCount })}</p>
      <div className="d-flex gap-2">
        <Button variant="secondary" disabled={page <= FIRST_PAGE} onClick={() => onPageChange(page - 1)}>
          {t('pagination.previous')}
        </Button>
        <Button variant="secondary" disabled={page >= lastPage} onClick={() => onPageChange(page + 1)}>
          {t('pagination.next')}
        </Button>
      </div>
    </nav>
  );
}
