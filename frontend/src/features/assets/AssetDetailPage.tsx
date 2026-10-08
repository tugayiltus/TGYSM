import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';

import { formatDate, formatDateTime } from '../../shared/lib/format';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { useReturnTo } from '../../shared/navigation/returnTo';
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
      <dt className="small fw-normal text-body-secondary">{label}</dt>
      <dd className="mb-0 fw-medium text-break">{value}</dd>
    </div>
  );
}

function AssetCard({ asset }: { asset: AssetItem }) {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const optionalDate = (value: string | null) => (value === null ? tc('none') : formatDate(value));
  return (
    <dl className="card card-body d-grid gap-3 mb-0 tgysm-fields">
      <Field label={t('fields.type')} value={t(`types.${asset.type}`)} />
      <Field label={t('fields.brand')} value={asset.brand} />
      <Field label={t('fields.model')} value={asset.model} />
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
    <ol className="list-unstyled mb-0 d-flex flex-column gap-2">
      {movements.map((m) => (
        <li key={m.id} className="rounded border bg-body p-2">
          <div className="d-flex flex-wrap align-items-center justify-content-between gap-2">
            <span className="fw-medium">{t(`movements.${m.kind}`)}</span>
            <time className="small text-body-secondary" dateTime={m.occurredAt}>
              {formatDateTime(m.occurredAt)}
            </time>
          </div>
          {m.userName && <p className="mb-0 small">{t('detail.to', { name: m.userName })}</p>}
          {m.userResponsibilityRetained && <p className="mb-0 small">{t('detail.retained')}</p>}
          {m.reason && <p className="mb-0 small">{m.reason}</p>}
          {m.actorName && <p className="mb-0 small text-body-secondary">{t('detail.by', { name: m.actorName })}</p>}
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
  const backTo = useReturnTo(ROUTES.assets);
  useBreadcrumbLabel('assetDetail', asset.data?.assetTag);

  if (asset.isPending) {
    return <LoadingState />;
  }
  if (asset.isError) {
    return <ErrorState error={asset.error} onRetry={() => void asset.refetch()} />;
  }

  const item = asset.data;
  const operations = OPERATIONS_BY_STATUS[item.status];

  return (
    <>
      <PageHeader
        titleOverride={item.assetTag}
        actions={
          <>
            <Link to={backTo} className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center">
              {t('detail.back')}
            </Link>
            <Button variant="secondary" onClick={() => setEditing(true)}>
              {tc('actions.edit')}
            </Button>
          </>
        }
      />
      <div className="mb-3">
        <AssetStatusBadge status={item.status} />
      </div>
      <div className="row g-4">
        <aside className="col-12 col-lg-4 order-lg-2">
          <div className="tgysm-sticky-aside d-flex flex-column gap-3">

      <section className="card card-body d-flex flex-column gap-2" aria-labelledby="asset-operations">
        <h2 id="asset-operations" className="h6 mb-0">
          {t('detail.operations')}
        </h2>
        {operations.length === 0 ? (
          <p className="mb-0 text-body-secondary">{t('detail.noOperations')}</p>
        ) : (
          <div className="d-flex flex-wrap gap-2">
            {operations.map((kind) => (
              <Button key={kind} variant={kind === 'lost' || kind === 'retire' ? 'danger' : 'secondary'} onClick={() => setOperation(kind)}>
                {t(`actions.${kind}`)}
              </Button>
            ))}
          </div>
        )}
      </section>

          </div>
        </aside>
        <div className="col-12 col-lg-8 order-lg-1 d-flex flex-column gap-4 min-w-0">
          <AssetCard asset={item} />
      <section className="d-flex flex-column gap-2" aria-labelledby="asset-history">
        <h2 id="asset-history" className="h5 mb-0">
          {t('detail.history')}
        </h2>
        {history.isPending && <LoadingState />}
        {history.isError && <ErrorState error={history.error} onRetry={() => void history.refetch()} />}
        {history.data && <History movements={history.data.movements} />}
      </section>
        </div>
      </div>

      {operation !== null && <AssetOperationDialog key={operation} asset={item} kind={operation} onClose={() => setOperation(null)} />}
      {editing && <AssetFormDialog asset={item} onClose={() => setEditing(false)} />}
    </>
  );
}
