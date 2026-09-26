import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';

import { useMyAssets } from './api';

export function MyAssetsPage() {
  const { t } = useTranslation('assets');
  const assets = useMyAssets();

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('mine.title')}</h1>
      {assets.isPending && <LoadingState />}
      {assets.isError && <ErrorState error={assets.error} onRetry={() => void assets.refetch()} />}
      {assets.data && assets.data.length === 0 && <EmptyState message={t('mine.empty')} />}
      {assets.data && assets.data.length > 0 && (
        <ul aria-label={t('mine.caption')} className="grid gap-3 sm:grid-cols-2">
          {assets.data.map((a) => (
            <li key={a.id} className="rounded-2xl border border-slate-200 bg-white p-4">
              <p className="font-medium">{a.assetTag}</p>
              <p className="text-slate-700">{`${a.brand} ${a.model}`}</p>
              <p className="text-sm text-slate-600">{t(`types.${a.type}`)}</p>
              {a.serialNumber && <p className="text-sm text-slate-600">{a.serialNumber}</p>}
              <p className="text-sm text-slate-600">{t('mine.assignedAt', { date: formatDateTime(a.assignedAt) })}</p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
