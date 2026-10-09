import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { DataCard } from '../../../shared/ui/DataCard';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { cilPlus } from '../../../shared/ui/icons';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState, TABLE_SKELETON_ROWS } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TextField } from '../../../shared/ui/TextField';

import { useBrands, useSaveBrand, useSetBrandActive, type BrandItem } from './api';
import { NAME_CODE_FIELDS, NAME_FIELDS, nameSchema, type NameValues } from './catalogName';
import { CatalogRowActions } from './CatalogToggle';

function BrandFormDialog({ brand, onClose }: { brand: BrandItem | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const save = useSaveBrand();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<NameValues>({ resolver: zodResolver(nameSchema), defaultValues: { name: brand?.name ?? '' } });
  const onSubmit = handleSubmit((v) =>
    save.mutate(
      { id: brand?.id ?? null, version: brand?.version ?? 0, name: v.name },
      { onSuccess: onClose, onError: (error) => applyServerErrors(error, setError, NAME_FIELDS, NAME_CODE_FIELDS) },
    ),
  );
  const alert = save.error && !isHandledByFields(save.error, NAME_FIELDS, NAME_CODE_FIELDS) ? describeError(save.error) : null;
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={brand === null ? t('deviceCatalog.brands.new') : t('deviceCatalog.brands.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={save.isPending}>
            {brand === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="d-flex flex-column gap-3" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('deviceCatalog.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
      </form>
    </Dialog>
  );
}

/** `/admin/brands`: device brands offered in asset forms (ADR-0023). */
export function BrandsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const brands = useBrands();
  const setActive = useSetBrandActive();
  const [form, setForm] = useState<{ brand: BrandItem | null } | null>(null);
  const [toggling, setToggling] = useState<BrandItem | null>(null);

  const actions = (b: BrandItem) => (
    <CatalogRowActions
      name={b.name}
      isActive={b.isActive}
      onEdit={() => setForm({ brand: b })}
      onToggle={() => {
        setActive.reset();
        setToggling(b);
      }}
    />
  );
  const columns: Column<BrandItem>[] = [
    { key: 'name', header: t('deviceCatalog.fields.name'), renderCell: (b) => <span className="fw-medium">{b.name}</span> },
    { key: 'status', header: t('deviceCatalog.fields.status'), renderCell: (b) => <StatusBadge isActive={b.isActive} /> },
    { key: 'actions', header: t('deviceCatalog.fields.actions'), renderCell: actions },
  ];

  return (
    <section>
      <PageHeader icon={ROUTE_META.adminBrands.icon} actions={<Button icon={cilPlus} onClick={() => setForm({ brand: null })}>{t('deviceCatalog.brands.new')}</Button>} />
      <DataCard>
        {brands.isPending && <LoadingState rows={TABLE_SKELETON_ROWS} />}
        {brands.isError && <ErrorState error={brands.error} onRetry={() => void brands.refetch()} />}
        {brands.data?.length === 0 && <EmptyState />}
        {brands.data !== undefined && brands.data.length > 0 && (
          <ResponsiveList
            items={brands.data}
            columns={columns}
            getKey={(b) => b.id}
            caption={t('deviceCatalog.brands.listCaption')}
            renderCard={(b) => (
              <div className="d-flex flex-column gap-2">
                <div className="d-flex justify-content-between gap-2">
                  <span className="fw-medium text-break">{b.name}</span>
                  <StatusBadge isActive={b.isActive} />
                </div>
                {actions(b)}
              </div>
            )}
          />
        )}
      </DataCard>
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
    </section>
  );
}
