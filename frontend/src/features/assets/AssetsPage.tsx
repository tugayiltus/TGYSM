import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useSearchParams } from 'react-router';

import { readPageParam } from '../../shared/lib/pageParam';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { assetPath, ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { Pagination } from '../../shared/ui/Pagination';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../shared/ui/ResponsiveList';
import { SelectField } from '../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';
import { TextField } from '../../shared/ui/TextField';

import { ASSET_STATUSES, ASSET_TYPES, useAssets, type AssetItem } from './api';
import { AssetFormDialog } from './AssetFormDialog';
import { AssetStatusBadge } from './AssetStatusBadge';

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;
const ALL = '';
/** Filters live in the URL so an asset opened from the list can return to the same filtered view. */
const PARAM = { search: 'q', type: 'type', status: 'status', page: 'page' } as const;
const LINK_BUTTON_CLASS = 'btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center';

function OpenLink({ asset }: { asset: AssetItem }) {
  const { t } = useTranslation('assets');
  const location = useLocation();
  return (
    <Link to={assetPath(asset.id)} state={linkState(location)} className={LINK_BUTTON_CLASS} aria-label={recordActionLabel(t('list.open'), asset.assetTag)}>
      {t('list.open')}
    </Link>
  );
}

export function AssetsPage() {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const [params, setParams] = useSearchParams();
  const type = ASSET_TYPES.find((value) => value === params.get(PARAM.type)) ?? ALL;
  const status = ASSET_STATUSES.find((value) => value === params.get(PARAM.status)) ?? ALL;
  const page = readPageParam(params.get(PARAM.page));
  const [search, setSearchText] = useState(params.get(PARAM.search) ?? '');
  const setFilter = (key: string, value: string) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value === '') {
          next.delete(key);
        } else {
          next.set(key, value);
        }
        next.delete(PARAM.page);
        return next;
      },
      { replace: true },
    );
  const setPage = (next: number) =>
    setParams(
      (current) => {
        const updated = new URLSearchParams(current);
        updated.set(PARAM.page, String(next));
        return updated;
      },
      { replace: true },
    );
  const [creating, setCreating] = useState(false);
  const debouncedSearch = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  const assets = useAssets({
    search: debouncedSearch,
    type: type === ALL ? undefined : type,
    status: status === ALL ? undefined : status,
    page,
    pageSize: PAGE_SIZE,
  });

  const device = (a: AssetItem) => `${t(`types.${a.type}`)} · ${a.brand} ${a.model}`;
  const columns: Column<AssetItem>[] = [
    { key: 'tag', header: t('list.columns.tag'), renderCell: (a) => <span className="fw-medium">{a.assetTag}</span> },
    { key: 'device', header: t('list.columns.device'), renderCell: device },
    { key: 'status', header: t('list.columns.status'), renderCell: (a) => <AssetStatusBadge status={a.status} /> },
    { key: 'location', header: t('list.columns.location'), renderCell: (a) => a.locationName },
    { key: 'holder', header: t('list.columns.holder'), renderCell: (a) => a.assignedUserName ?? tc('none') },
    { key: 'actions', header: t('list.columns.actions'), renderCell: (a) => <OpenLink asset={a} /> },
  ];

  const renderCard = (a: AssetItem) => (
    <div className="d-flex flex-column gap-2">
      <div className="d-flex align-items-start justify-content-between gap-2">
        <div className="min-w-0">
          <p className="mb-0 fw-medium text-break">{a.assetTag}</p>
          <p className="mb-0 small text-body-secondary">{device(a)}</p>
          <p className="mb-0 small text-body-secondary">{a.assignedUserName ?? a.locationName}</p>
        </div>
        <AssetStatusBadge status={a.status} />
      </div>
      <OpenLink asset={a} />
    </div>
  );

  return (
    <>
      <PageHeader
        titleOverride={t('list.title')}
        actions={
          <>
            <Link to={ROUTES.assetImport} className={LINK_BUTTON_CLASS}>
              {t('list.import')}
            </Link>
            <Button onClick={() => setCreating(true)}>{t('list.new')}</Button>
          </>
        }
      />
      <div className="d-flex gap-2">
      <div className="row g-3">
        <TextField
          className="col-12 col-md-4"
          type="search"
          label={t('list.searchLabel')}
          value={search}
          onChange={(event) => {
            setSearchText(event.target.value);
            setFilter(PARAM.search, event.target.value);
          }}
        />
        <SelectField
          label={t('list.typeFilter')}
          placeholder={t('list.allTypes')}
          options={ASSET_TYPES.map((value) => ({ value, label: t(`types.${value}`) }))}
          className="col-12 col-md-4"
          value={type}
          onChange={(event) => setFilter(PARAM.type, event.target.value)}
        />
        <SelectField
          label={t('list.statusFilter')}
          placeholder={t('list.allStatuses')}
          options={ASSET_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
          className="col-12 col-md-4"
          value={status}
          onChange={(event) => setFilter(PARAM.status, event.target.value)}
        />
      </div>

      {assets.isPending && <LoadingState />}
      {assets.isError && <ErrorState error={assets.error} onRetry={() => void assets.refetch()} />}
      {assets.data && assets.data.items.length === 0 && <EmptyState />}
      {assets.data && assets.data.items.length > 0 && (
        <>
          <ResponsiveList items={assets.data.items} columns={columns} getKey={(a) => a.id} renderCard={renderCard} caption={t('list.caption')} />
          <Pagination page={assets.data.page} pageSize={assets.data.pageSize} totalCount={assets.data.totalCount} onPageChange={setPage} />
        </>
      )}

      </div>
      {creating && <AssetFormDialog asset={null} onClose={() => setCreating(false)} />}
    </>
  );
}
