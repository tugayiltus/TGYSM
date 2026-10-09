import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { DataCard } from '../../shared/ui/DataCard';
import { EmptyState, ErrorState, LoadingState, TABLE_SKELETON_ROWS } from '../../shared/ui/States';

import { useMyAssets } from './api';

export function MyAssetsPage() {
  const { t } = useTranslation('assets');
  const assets = useMyAssets();

  return (
    <>
      <PageHeader icon={ROUTE_META.myAssets.icon} titleOverride={t('mine.title')} />
      <DataCard>
        {assets.isPending && <LoadingState rows={TABLE_SKELETON_ROWS} />}
        {assets.isError && <ErrorState error={assets.error} onRetry={() => void assets.refetch()} />}
        {assets.data && assets.data.length === 0 && <EmptyState message={t('mine.empty')} />}
        {assets.data && assets.data.length > 0 && (
          <ul aria-label={t('mine.caption')} className="list-group list-group-flush">
            {assets.data.map((a) => (
              <li key={a.id} className="list-group-item py-3">
                <p className="mb-0 fw-medium">{a.assetTag}</p>
                <p className="mb-0">{`${a.brand} ${a.model}`}</p>
                <p className="mb-0 small text-body-secondary">{t(`types.${a.type}`)}</p>
                {a.serialNumber && <p className="mb-0 small text-body-secondary">{a.serialNumber}</p>}
                <p className="mb-0 small text-body-secondary">{t('mine.assignedAt', { date: formatDateTime(a.assignedAt) })}</p>
              </li>
            ))}
          </ul>
        )}
      </DataCard>
    </>
  );
}
