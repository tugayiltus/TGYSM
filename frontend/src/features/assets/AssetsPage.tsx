import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { assetPath, ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { Pagination } from '../../shared/ui/Pagination';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../shared/ui/ResponsiveList';
import { SelectField } from '../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';
import { TextField } from '../../shared/ui/TextField';

import { ASSET_STATUSES, ASSET_TYPES, useAssets, type AssetItem, type AssetStatus, type AssetType } from './api';
import { AssetFormDialog } from './AssetFormDialog';
import { AssetStatusBadge } from './AssetStatusBadge';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SEARCH_DEBOUNCE_MS = 300;
const ALL = '';

const LINK_BUTTON_CLASS =
  'inline-flex min-h-11 items-center justify-center rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-800 hover:bg-slate-50';

function OpenLink({ asset }: { asset: AssetItem }) {
  const { t } = useTranslation('assets');
  return (
    <Link to={assetPath(asset.id)} className={LINK_BUTTON_CLASS} aria-label={recordActionLabel(t('list.open'), asset.assetTag)}>
      {t('list.open')}
    </Link>
  );
}

export function AssetsPage() {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const [search, setSearch] = useState('');
  const [type, setType] = useState<AssetType | typeof ALL>(ALL);
  const [status, setStatus] = useState<AssetStatus | typeof ALL>(ALL);
  const [page, setPage] = useState(FIRST_PAGE);
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
    { key: 'tag', header: t('list.columns.tag'), renderCell: (a) => <span className="font-medium">{a.assetTag}</span> },
    { key: 'device', header: t('list.columns.device'), renderCell: device },
    { key: 'status', header: t('list.columns.status'), renderCell: (a) => <AssetStatusBadge status={a.status} /> },
    { key: 'location', header: t('list.columns.location'), renderCell: (a) => a.locationName },
    { key: 'holder', header: t('list.columns.holder'), renderCell: (a) => a.assignedUserName ?? tc('none') },
    { key: 'actions', header: t('list.columns.actions'), renderCell: (a) => <OpenLink asset={a} /> },
  ];

  const renderCard = (a: AssetItem) => (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium break-words">{a.assetTag}</p>
          <p className="text-sm text-slate-600">{device(a)}</p>
          <p className="text-sm text-slate-600">{a.assignedUserName ?? a.locationName}</p>
        </div>
        <AssetStatusBadge status={a.status} />
      </div>
      <OpenLink asset={a} />
    </div>
  );

  const resetPage = () => setPage(FIRST_PAGE);

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('list.title')}</h1>
        <div className="flex flex-wrap gap-2">
          <Link to={ROUTES.assetImport} className={LINK_BUTTON_CLASS}>
            {t('list.import')}
          </Link>
          <Button onClick={() => setCreating(true)}>{t('list.new')}</Button>
        </div>
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <TextField
          type="search"
          label={t('list.searchLabel')}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            resetPage();
          }}
        />
        <SelectField
          label={t('list.typeFilter')}
          placeholder={t('list.allTypes')}
          options={ASSET_TYPES.map((value) => ({ value, label: t(`types.${value}`) }))}
          value={type}
          onChange={(event) => {
            setType(event.target.value as AssetType | typeof ALL);
            resetPage();
          }}
        />
        <SelectField
          label={t('list.statusFilter')}
          placeholder={t('list.allStatuses')}
          options={ASSET_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
          value={status}
          onChange={(event) => {
            setStatus(event.target.value as AssetStatus | typeof ALL);
            resetPage();
          }}
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

      {creating && <AssetFormDialog asset={null} onClose={() => setCreating(false)} />}
    </section>
  );
}
