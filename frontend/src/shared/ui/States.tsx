import { useTranslation } from 'react-i18next';

import { describeError } from '../api/errorMessage';

import { Button } from './Button';

export function LoadingState() {
  const { t } = useTranslation();
  return (
    <p role="status" className="p-6 text-center text-slate-600">
      {t('states.loading')}
    </p>
  );
}

export function EmptyState({ message }: { message?: string }) {
  const { t } = useTranslation();
  return <p className="rounded-xl border border-dashed border-slate-300 p-6 text-center text-slate-600">{message ?? t('states.empty')}</p>;
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <div role="alert" className="flex flex-col items-center gap-3 rounded-xl border border-red-200 bg-red-50 p-6 text-center text-red-800">
      <p>{describeError(error)}</p>
      {onRetry && (
        <Button variant="secondary" onClick={onRetry}>
          {t('actions.retry')}
        </Button>
      )}
    </div>
  );
}
