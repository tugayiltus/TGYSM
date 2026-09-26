import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';

import { formatDate, formatDateTime } from '../../shared/lib/format';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';

import { OPERATIONS_BY_STATUS, useAsset, useAssetHistory, type AssetItem, type AssetMovementItem, type AssetOperationKind } from './api';
import { AssetFormDialog } from './AssetFormDialog';
import { AssetOperationDialog } from './AssetOperationDialog';
import { AssetStatusBadge } from './AssetStatusBadge';

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-slate-600">{label}</dt>
      <dd className="font-medium break-words">{value}</dd>
    </div>
  );
}

function AssetCard({ asset }: { asset: AssetItem }) {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const optionalDate = (value: string | null) => (value === null ? tc('none') : formatDate(value));
  return (
    <dl className="grid gap-4 rounded-2xl border border-slate-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-3">
      <Field label={t('fields.type')} value={t(`types.${asset.type}`)} />
      <Field label={t('fields.brand')} value={`${asset.brand} ${asset.model}`} />
      <Field label={t('fields.serialNumber')} value={asset.serialNumber ?? tc('none')} />
      <Field label={t('fields.holder')} value={asset.assignedUserName ?? tc('none')} />
      <Field label={t('fields.location')} value={asset.locationName} />
      <Field label={t('fields.department')} value={asset.departmentName ?? tc('none')} />
      <Field label={t('fields.warrantyEndsOn')} value={optionalDate(asset.warrantyEndsOn)} />
      <Field label={t('fields.purchasedOn')} value={optionalDate(asset.purchasedOn)} />
      <Field label={t('fields.createdAt')} value={formatDateTime(asset.createdAt)} />
      {asset.notes && <Field label={t('fields.notes')} value={asset.notes} />}
    </dl>
  );
}

function History({ movements }: { movements: AssetMovementItem[] }) {
  const { t } = useTranslation('assets');
  if (movements.length === 0) {
    return <EmptyState />;
  }
  return (
    <ol className="flex flex-col gap-3">
      {movements.map((m) => (
        <li key={m.id} className="rounded-xl border border-slate-200 bg-white p-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="font-medium">{t(`movements.${m.kind}`)}</span>
            <time className="text-sm text-slate-600" dateTime={m.occurredAt}>
              {formatDateTime(m.occurredAt)}
            </time>
          </div>
          {m.userName && <p className="text-sm text-slate-700">{t('detail.to', { name: m.userName })}</p>}
          {m.userResponsibilityRetained && <p className="text-sm text-slate-700">{t('detail.retained')}</p>}
          {m.reason && <p className="text-sm text-slate-700">{m.reason}</p>}
          {m.actorName && <p className="text-xs text-slate-600">{t('detail.by', { name: m.actorName })}</p>}
        </li>
      ))}
    </ol>
  );
}

export function AssetDetailPage() {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const { id = '' } = useParams();
  const asset = useAsset(id);
  const history = useAssetHistory(id);
  const [operation, setOperation] = useState<AssetOperationKind | null>(null);
  const [editing, setEditing] = useState(false);

  if (asset.isPending) {
    return <LoadingState />;
  }
  if (asset.isError) {
    return <ErrorState error={asset.error} onRetry={() => void asset.refetch()} />;
  }

  const item = asset.data;
  const operations = OPERATIONS_BY_STATUS[item.status];

  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <Link to={ROUTES.assets} className="text-sm text-brand-900 underline">
          {t('detail.back')}
        </Link>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold break-all">{item.assetTag}</h1>
            <AssetStatusBadge status={item.status} />
          </div>
          <Button variant="secondary" onClick={() => setEditing(true)}>
            {tc('actions.edit')}
          </Button>
        </div>
      </div>

      <AssetCard asset={item} />

      <section className="flex flex-col gap-3" aria-labelledby="asset-operations">
        <h2 id="asset-operations" className="text-lg font-semibold">
          {t('detail.operations')}
        </h2>
        {operations.length === 0 ? (
          <p className="text-slate-600">{t('detail.noOperations')}</p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {operations.map((kind) => (
              <Button key={kind} variant={kind === 'lost' || kind === 'retire' ? 'danger' : 'secondary'} onClick={() => setOperation(kind)}>
                {t(`actions.${kind}`)}
              </Button>
            ))}
          </div>
        )}
      </section>

      <section className="flex flex-col gap-3" aria-labelledby="asset-history">
        <h2 id="asset-history" className="text-lg font-semibold">
          {t('detail.history')}
        </h2>
        {history.isPending && <LoadingState />}
        {history.isError && <ErrorState error={history.error} onRetry={() => void history.refetch()} />}
        {history.data && <History movements={history.data.movements} />}
      </section>

      {operation !== null && <AssetOperationDialog key={operation} asset={item} kind={operation} onClose={() => setOperation(null)} />}
      {editing && <AssetFormDialog asset={item} onClose={() => setEditing(false)} />}
    </section>
  );
}
