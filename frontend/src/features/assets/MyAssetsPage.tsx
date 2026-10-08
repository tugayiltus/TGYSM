import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';

import { useMyAssets } from './api';

export function MyAssetsPage() {
  const { t } = useTranslation('assets');
  const assets = useMyAssets();

  return (
    <>
      <PageHeader titleOverride={t('mine.title')} />
      {assets.isPending && <LoadingState />}
      {assets.isError && <ErrorState error={assets.error} onRetry={() => void assets.refetch()} />}
      {assets.data && assets.data.length === 0 && <EmptyState message={t('mine.empty')} />}
      {assets.data && assets.data.length > 0 && (
        <ul aria-label={t('mine.caption')} className="list-unstyled row g-3 mb-0">
          {assets.data.map((a) => (
            <li key={a.id} className="col-12 col-md-6">
              <div className="card card-body h-100">
              <p className="mb-0 fw-medium">{a.assetTag}</p>
              <p className="mb-0">{`${a.brand} ${a.model}`}</p>
              <p className="mb-0 small text-body-secondary">{t(`types.${a.type}`)}</p>
              {a.serialNumber && <p className="mb-0 small text-body-secondary">{a.serialNumber}</p>}
              <p className="mb-0 small text-body-secondary">{t('mine.assignedAt', { date: formatDateTime(a.assignedAt) })}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
