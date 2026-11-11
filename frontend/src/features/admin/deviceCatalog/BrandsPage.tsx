/**
 * @input Nothing; search and status come from the URL query.
 * @output The device brand list with create, edit and (de)activate (ADR-0023).
 * @position `/admin/brands`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { FormTextInput } from '../../../shared/form/formFields';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { ListToolbar } from '../../../shared/list/ListToolbar';
import { useListParams } from '../../../shared/list/useListParams';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { ActiveToken } from '../../../shared/page/ActiveToken';
import { RecordActions } from '../../../shared/page/RecordActions';
import { Icons } from '../../../shared/ui/icons';
import { filterRows, readStatusFilter, STATUS_FILTER_KEY } from '../shared/statusFilter';
import { StatusFilterSelector } from '../shared/StatusFilterSelector';

import { useBrands, useSaveBrand, useSetBrandActive, type BrandItem } from './api';
import { NAME_CODE_FIELDS, NAME_FIELDS, nameSchema, type NameValues } from './catalogName';

const SKELETON_ROWS = 5;
const FILTER_KEYS = [STATUS_FILTER_KEY] as const;

function BrandFormDialog({ brand, onClose }: { brand: BrandItem | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const save = useSaveBrand();
  const { control, handleSubmit, setError } = useForm<NameValues>({ resolver: zodResolver(nameSchema), defaultValues: { name: brand?.name ?? '' } });
  const onSubmit = handleSubmit((v) =>
    save.mutate(
      { id: brand?.id ?? null, version: brand?.version ?? 0, name: v.name },
      { onSuccess: onClose, onError: (error) => applyServerErrors(error, setError, NAME_FIELDS, NAME_CODE_FIELDS) },
    ),
  );
  const alert = save.error && !isHandledByFields(save.error, NAME_FIELDS, NAME_CODE_FIELDS) ? describeError(save.error) : null;
  return (
    <FormDialog
      title={brand === null ? t('deviceCatalog.brands.new') : t('deviceCatalog.brands.edit')}
      submitLabel={brand === null ? tc('actions.create') : tc('actions.save')}
      isPending={save.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="name" label={t('deviceCatalog.fields.name')} autoComplete="off" isRequired />
    </FormDialog>
  );
}

export function BrandsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const list = useListParams(FILTER_KEYS);
  const status = readStatusFilter(list.filters.status);
  const brands = useBrands();
  const setActive = useSetBrandActive();
  const [form, setForm] = useState<{ brand: BrandItem | null } | null>(null);
  const [toggling, setToggling] = useState<BrandItem | null>(null);
  const rows = brands.data === undefined ? undefined : filterRows(brands.data, list.search, status, (b) => b.name);

  const actions = (b: BrandItem) => (
    <RecordActions
      name={b.name}
      isActive={b.isActive}
      onEdit={() => setForm({ brand: b })}
      onToggle={() => {
        setActive.reset();
        setToggling(b);
      }}
    />
  );
  const columns: DataColumn<BrandItem>[] = [
    { key: 'name', header: t('deviceCatalog.fields.name'), renderCell: (b) => <Text type="label">{b.name}</Text> },
    { key: 'status', header: t('deviceCatalog.fields.status'), renderCell: (b) => <ActiveToken isActive={b.isActive} /> },
    { key: 'actions', header: t('deviceCatalog.fields.actions'), renderCell: actions },
  ];
  const renderCard = (b: BrandItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <Text type="label" wordBreak="break-word">
            {b.name}
          </Text>
        </StackItem>
        <StackItem><ActiveToken isActive={b.isActive} /></StackItem>
      </HStack>
      {actions(b)}
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminBrands.icon}
        actions={<Button label={t('deviceCatalog.brands.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ brand: null })} />}
      />
      <ListToolbar
        title={t('deviceCatalog.brands.title')}
        search={list.search}
        searchLabel={t('deviceCatalog.brands.searchLabel')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={<StatusFilterSelector value={status} onChange={(value) => list.setFilter(STATUS_FILTER_KEY, value)} />}
      />
      <Card padding={0}>
        {brands.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {brands.isError && <ErrorState error={brands.error} onRetry={() => void brands.refetch()} />}
        {rows?.length === 0 && <EmptyState />}
        {rows !== undefined && rows.length > 0 && (
          <DataList items={rows} columns={columns} getKey={(b) => b.id} renderCard={renderCard} caption={t('deviceCatalog.brands.listCaption')} />
        )}
      </Card>
      {form !== null && <BrandFormDialog key={form.brand?.id ?? 'new'} brand={form.brand} onClose={() => setForm(null)} />}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('deviceCatalog.brands.confirmDeactivate', { name: toggling.name }) : t('deviceCatalog.brands.confirmReactivate', { name: toggling.name })}
          confirmLabel={tc('actions.confirm')}
          variant={toggling.isActive ? 'danger' : 'primary'}
          isPending={setActive.isPending}
          error={setActive.error}
          onConfirm={() => setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })}
          onCancel={() => setToggling(null)}
        />
      )}
    </VStack>
  );
}
