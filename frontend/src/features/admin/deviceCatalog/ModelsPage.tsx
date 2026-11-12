/**
 * @input Nothing; search, type, brand and status come from the URL query.
 * @output The device model list as type, brand and name, with create, edit and (de)activate (ADR-0023).
 * @position `/admin/models`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Selector } from '@astryxdesign/core/Selector';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { FormSelector, FormTextInput } from '../../../shared/form/formFields';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { ListToolbar } from '../../../shared/list/ListToolbar';
import { useListParams } from '../../../shared/list/useListParams';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { ActiveToken } from '../../../shared/page/ActiveToken';
import { RecordActions } from '../../../shared/page/RecordActions';
import { Icons } from '../../../shared/ui/icons';
import { ASSET_TYPES } from '../../assets/api';
import { filterRows, readStatusFilter, STATUS_FILTER_KEY } from '../shared/statusFilter';
import { StatusFilterSelector } from '../shared/StatusFilterSelector';

import { useAssetModels, useBrands, useSaveAssetModel, useSetAssetModelActive, type AssetModelItem, type AssetType } from './api';
import { NAME_CODE_FIELDS, nameSchema } from './catalogName';

const SKELETON_ROWS = 5;
export const BRAND_FILTER_KEY = 'brand';
/** Navigation state from a brand row: open the model form with this brand chosen. */
export interface ModelsPageState {
  createModelForBrand: string;
}
const brandToCreateFor = (state: unknown): string | undefined =>
  typeof state === 'object' && state !== null && 'createModelForBrand' in state && typeof state.createModelForBrand === 'string' ? state.createModelForBrand : undefined;
const TYPE_FILTER_KEY = 'type';
const FILTER_KEYS = [TYPE_FILTER_KEY, BRAND_FILTER_KEY, STATUS_FILTER_KEY] as const;
const isAssetType = (value: string | undefined): value is AssetType => ASSET_TYPES.some((type) => type === value);

const createSchema = nameSchema.extend({
  brandId: z.string().min(1, ERROR_CODES.required),
  type: z.string().min(1, ERROR_CODES.required),
});
const editSchema = nameSchema.extend({ brandId: z.string(), type: z.string() });
type CreateValues = z.infer<typeof createSchema>;
const CREATE_FIELDS = ['type', 'brandId', 'name'] as const;

const modelName = (m: AssetModelItem) => `${m.brandName} ${m.name}`;

function ModelFormDialog({ model, initialBrandId, onClose }: { model: AssetModelItem | null; initialBrandId?: string; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: ta } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const save = useSaveAssetModel();
  const brands = useBrands();
  const {
    control,
    handleSubmit,
    setError,
    formState: { isDirty },
  } = useForm<CreateValues>({
    resolver: zodResolver(model === null ? createSchema : editSchema),
    defaultValues: { brandId: model?.brandId ?? initialBrandId ?? '', type: model?.type ?? '', name: model?.name ?? '' },
  });
  const onSubmit = handleSubmit((v) =>
    save.mutate(model === null ? { id: null, brandId: v.brandId, type: v.type as AssetType, name: v.name } : { id: model.id, version: model.version, name: v.name }, {
      onSuccess: onClose,
      onError: (error) => applyServerErrors(error, setError, CREATE_FIELDS, NAME_CODE_FIELDS),
    }),
  );
  const alert = save.error && !isHandledByFields(save.error, CREATE_FIELDS, NAME_CODE_FIELDS) ? describeError(save.error) : null;
  // Brand and type identify the model: they are chosen once, only the name can change later.
  const fixed = model !== null;
  return (
    <FormDialog
      isDirty={isDirty}
      title={model === null ? t('deviceCatalog.models.new') : t('deviceCatalog.models.edit')}
      submitLabel={model === null ? tc('actions.create') : tc('actions.save')}
      isPending={save.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormSelector
        control={control}
        name="type"
        label={t('deviceCatalog.fields.type')}
        placeholder={t('deviceCatalog.fields.selectType')}
        isReadOnly={fixed}
        isRequired={!fixed}
        options={ASSET_TYPES.map((type) => ({ value: type, label: ta(`types.${type}`) }))}
      />
      <FormSelector
        control={control}
        name="brandId"
        label={t('deviceCatalog.fields.brand')}
        placeholder={t('deviceCatalog.fields.selectBrand')}
        isReadOnly={fixed}
        isRequired={!fixed}
        options={(brands.data ?? []).filter((b) => b.isActive || b.id === model?.brandId).map((b) => ({ value: b.id, label: b.name }))}
      />
      <FormTextInput control={control} name="name" label={t('deviceCatalog.fields.name')} autoComplete="off" isRequired />
    </FormDialog>
  );
}

export function ModelsPage() {
  const { t } = useTranslation('admin');
  const { t: ta } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const list = useListParams(FILTER_KEYS);
  const brandId = list.filters.brand;
  const status = readStatusFilter(list.filters.status);
  const brands = useBrands();
  const models = useAssetModels(brandId);
  const setActive = useSetAssetModelActive();
  const location = useLocation();
  const navigate = useNavigate();
  const [form, setForm] = useState<{ model: AssetModelItem | null; brandId?: string } | null>(() => {
    const brandFromRow = brandToCreateFor(location.state);
    return brandFromRow === undefined ? null : { model: null, brandId: brandFromRow };
  });
  const [toggling, setToggling] = useState<AssetModelItem | null>(null);
  const closeForm = () => {
    setForm(null);
    // The request from a brand row is used once; a reload or a back step must not open the form again.
    if (brandToCreateFor(location.state) !== undefined) {
      void navigate({ pathname: location.pathname, search: location.search }, { replace: true, state: null });
    }
  };
  const type = isAssetType(list.filters.type) ? list.filters.type : undefined;
  const rows = models.data === undefined ? undefined : filterRows(models.data, list.search, status, modelName).filter((m) => type === undefined || m.type === type);

  const actions = (m: AssetModelItem) => (
    <RecordActions
      name={modelName(m)}
      isActive={m.isActive}
      onEdit={() => setForm({ model: m })}
      onToggle={() => {
        setActive.reset();
        setToggling(m);
      }}
    />
  );
  const columns: DataColumn<AssetModelItem>[] = [
    { key: 'type', header: t('deviceCatalog.fields.type'), renderCell: (m) => ta(`types.${m.type}`) },
    { key: 'brand', header: t('deviceCatalog.fields.brand'), renderCell: (m) => m.brandName },
    { key: 'name', header: t('deviceCatalog.fields.name'), renderCell: (m) => <Text type="label">{m.name}</Text> },
    { key: 'status', header: t('deviceCatalog.fields.status'), renderCell: (m) => <ActiveToken isActive={m.isActive} /> },
    { key: 'actions', header: t('deviceCatalog.fields.actions'), renderCell: actions },
  ];
  const renderCard = (m: AssetModelItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <Text type="supporting">{ta(`types.${m.type}`)}</Text>
            <Text type="label" wordBreak="break-word">
              {modelName(m)}
            </Text>
          </VStack>
        </StackItem>
        <StackItem><ActiveToken isActive={m.isActive} /></StackItem>
      </HStack>
      {actions(m)}
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminModels.icon}
        actions={<Button label={t('deviceCatalog.models.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ model: null })} />}
      />
      <ListToolbar
        title={t('deviceCatalog.models.title')}
        search={list.search}
        searchLabel={t('deviceCatalog.models.searchLabel')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('deviceCatalog.models.typeFilter')}
              placeholder={t('deviceCatalog.models.allTypes')}
              value={type ?? ''}
              options={ASSET_TYPES.map((value) => ({ value, label: ta(`types.${value}`) }))}
              onChange={(value) => list.setFilter(TYPE_FILTER_KEY, value)}
            />
            <Selector
              label={t('deviceCatalog.models.brandFilter')}
              placeholder={t('deviceCatalog.models.allBrands')}
              value={brandId}
              options={(brands.data ?? []).map((b) => ({ value: b.id, label: b.name }))}
              onChange={(value) => list.setFilter(BRAND_FILTER_KEY, value)}
            />
            <StatusFilterSelector value={status} onChange={(value) => list.setFilter(STATUS_FILTER_KEY, value)} />
          </>
        }
      />
      <Card padding={0}>
        {models.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {models.isError && <ErrorState error={models.error} onRetry={() => void models.refetch()} />}
        {rows?.length === 0 && <EmptyState />}
        {rows !== undefined && rows.length > 0 && (
          <DataList items={rows} columns={columns} getKey={(m) => m.id} renderCard={renderCard} caption={t('deviceCatalog.models.listCaption')} />
        )}
      </Card>
      {form !== null && <ModelFormDialog key={form.model?.id ?? 'new'} model={form.model} initialBrandId={form.brandId} onClose={closeForm} />}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('deviceCatalog.models.confirmDeactivate', { name: toggling.name }) : t('deviceCatalog.models.confirmReactivate', { name: toggling.name })}
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
