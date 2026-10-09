import { CAlert, CSpinner } from '@coreui/react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../api/errorMessage';

import { Button } from './Button';

/** Skeleton rows a list card shows while loading: the space of a first page, so nothing jumps when data arrives. */
export const TABLE_SKELETON_ROWS = 5;

export function LoadingState({ rows }: { rows?: number }) {
  const { t } = useTranslation();
  if (rows === undefined) {
    return (
      <p role="status" className="d-flex align-items-center justify-content-center gap-2 p-4 mb-0 text-body-secondary">
        <CSpinner size="sm" aria-hidden="true" />
        {t('states.loading')}
      </p>
    );
  }
  return (
    <div role="status" className="p-3 d-flex flex-column gap-2">
      <span className="visually-hidden">{t('states.loading')}</span>
      {Array.from({ length: rows }, (_, index) => (
        <span key={index} className="placeholder-glow tgysm-skeleton-row" aria-hidden="true">
          <span className="placeholder col-12 rounded" />
        </span>
      ))}
    </div>
  );
}

export function EmptyState({ message, action }: { message?: string; action?: ReactNode }) {
  const { t } = useTranslation();
  return (
    <div className="d-flex flex-column align-items-center gap-3 p-4 text-center text-body-secondary">
      <p className="mb-0">{message ?? t('states.empty')}</p>
      {action}
    </div>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <CAlert color="danger" className="d-flex flex-column align-items-center gap-3 text-center m-3">
      <p className="mb-0">{describeError(error)}</p>
      {onRetry && (
        <Button variant="secondary" onClick={onRetry}>
          {t('actions.retry')}
        </Button>
      )}
    </CAlert>
  );
}
