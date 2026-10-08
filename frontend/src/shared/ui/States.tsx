import { CAlert, CSpinner } from '@coreui/react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../api/errorMessage';

import { Button } from './Button';

export function LoadingState() {
  const { t } = useTranslation();
  return (
    <p role="status" className="d-flex align-items-center justify-content-center gap-2 p-4 mb-0 text-body-secondary">
      <CSpinner size="sm" aria-hidden="true" />
      {t('states.loading')}
    </p>
  );
}

export function EmptyState({ message, action }: { message?: string; action?: ReactNode }) {
  const { t } = useTranslation();
  return (
    <div className="d-flex flex-column align-items-center gap-3 rounded border border-dashed p-4 text-center text-body-secondary">
      <p className="mb-0">{message ?? t('states.empty')}</p>
      {action}
    </div>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <CAlert color="danger" className="d-flex flex-column align-items-center gap-3 text-center">
      <p className="mb-0">{describeError(error)}</p>
      {onRetry && (
        <Button variant="secondary" onClick={onRetry}>
          {t('actions.retry')}
        </Button>
      )}
    </CAlert>
  );
}
