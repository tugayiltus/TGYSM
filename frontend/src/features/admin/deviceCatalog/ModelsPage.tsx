import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { DataCard } from '../../../shared/ui/DataCard';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { cilPlus } from '../../../shared/ui/icons';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState, TABLE_SKELETON_ROWS } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TextField } from '../../../shared/ui/TextField';
import { ASSET_TYPES } from '../../assets/api';

import { useAssetModels, useBrands, useSaveAssetModel, useSetAssetModelActive, type AssetModelItem, type AssetType } from './api';
import { NAME_CODE_FIELDS, nameSchema } from './catalogName';
import { CatalogRowActions } from './CatalogToggle';

const ALL_BRANDS = '';

const createSchema = nameSchema.extend({
  brandId: z.string().min(1, ERROR_CODES.required),
  type: z.string().min(1, ERROR_CODES.required),
});
type CreateValues = z.infer<typeof createSchema>;
const CREATE_FIELDS = ['brandId', 'type', 'name'] as const;

function ModelFormDialog({ model, onClose }: { model: AssetModelItem | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: ta } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const formId = useId();
  const save = useSaveAssetModel();
  const brands = useBrands();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<CreateValues>({
    resolver: zodResolver(model === null ? createSchema : nameSchema.extend({ brandId: z.string(), type: z.string() })),
    defaultValues: { brandId: model?.brandId ?? '', type: model?.type ?? '', name: model?.name ?? '' },
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
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={model === null ? t('deviceCatalog.models.new') : t('deviceCatalog.models.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={save.isPending}>
            {model === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="d-flex flex-column gap-3" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <SelectField
          label={t('deviceCatalog.fields.brand')}
          placeholder={t('deviceCatalog.fields.selectBrand')}
          disabled={fixed}
          options={(brands.data ?? []).filter((b) => b.isActive || b.id === model?.brandId).map((b) => ({ value: b.id, label: b.name }))}
          error={fieldError(errors.brandId)}
          {...register('brandId')}
        />
        <SelectField
          label={t('deviceCatalog.fields.type')}
          placeholder={t('deviceCatalog.fields.selectType')}
          disabled={fixed}
          options={ASSET_TYPES.map((type) => ({ value: type, label: ta(`types.${type}`) }))}
          error={fieldError(errors.type)}
          {...register('type')}
        />
        <TextField label={t('deviceCatalog.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
      </form>
    </Dialog>
  );
}

/** `/admin/models`: models per brand and device type offered in asset forms (ADR-0023). */
export function ModelsPage() {
  const { t } = useTranslation('admin');
  const { t: ta } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const [brandId, setBrandId] = useState(ALL_BRANDS);
  const brands = useBrands();
  const models = useAssetModels(brandId);
  const setActive = useSetAssetModelActive();
  const [form, setForm] = useState<{ model: AssetModelItem | null } | null>(null);
  const [toggling, setToggling] = useState<AssetModelItem | null>(null);

  const actions = (m: AssetModelItem) => (
    <CatalogRowActions
      name={`${m.brandName} ${m.name}`}
      isActive={m.isActive}
      onEdit={() => setForm({ model: m })}
      onToggle={() => {
        setActive.reset();
        setToggling(m);
      }}
    />
  );
  const columns: Column<AssetModelItem>[] = [
    { key: 'brand', header: t('deviceCatalog.fields.brand'), renderCell: (m) => m.brandName },
    { key: 'type', header: t('deviceCatalog.fields.type'), renderCell: (m) => ta(`types.${m.type}`) },
    { key: 'name', header: t('deviceCatalog.fields.name'), renderCell: (m) => <span className="fw-medium">{m.name}</span> },
    { key: 'status', header: t('deviceCatalog.fields.status'), renderCell: (m) => <StatusBadge isActive={m.isActive} /> },
    { key: 'actions', header: t('deviceCatalog.fields.actions'), renderCell: actions },
  ];

  return (
    <section>
      <PageHeader icon={ROUTE_META.adminModels.icon} actions={<Button icon={cilPlus} onClick={() => setForm({ model: null })}>{t('deviceCatalog.models.new')}</Button>} />
      <DataCard
        filters={
          <>
            <SelectField
              className="tgysm-filter-field"
              label={t('deviceCatalog.models.brandFilter')}
              placeholder={t('deviceCatalog.models.allBrands')}
              options={(brands.data ?? []).map((b) => ({ value: b.id, label: b.name }))}
              value={brandId}
              onChange={(event) => setBrandId(event.target.value)}
            />
          </>
        }
      >
        {models.isPending && <LoadingState rows={TABLE_SKELETON_ROWS} />}
        {models.isError && <ErrorState error={models.error} onRetry={() => void models.refetch()} />}
        {models.data?.length === 0 && <EmptyState />}
        {models.data !== undefined && models.data.length > 0 && (
          <ResponsiveList
            items={models.data}
            columns={columns}
            getKey={(m) => m.id}
            caption={t('deviceCatalog.models.listCaption')}
            renderCard={(m) => (
              <div className="d-flex flex-column gap-2">
                <div className="d-flex justify-content-between gap-2">
                  <div className="min-w-0">
                    <p className="mb-0 fw-medium text-break">{`${m.brandName} ${m.name}`}</p>
                    <p className="mb-0 small text-body-secondary">{ta(`types.${m.type}`)}</p>
                  </div>
                  <StatusBadge isActive={m.isActive} />
                </div>
                {actions(m)}
              </div>
            )}
          />
        )}
      </DataCard>
      {form !== null && <ModelFormDialog key={form.model?.id ?? 'new'} model={form.model} onClose={() => setForm(null)} />}
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
    </section>
  );
}
