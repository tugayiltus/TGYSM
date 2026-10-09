import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useMemo } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { ComboboxField } from '../../shared/ui/ComboboxField';
import { Dialog } from '../../shared/ui/Dialog';
import { FormAlert } from '../../shared/ui/FormAlert';
import { SelectField } from '../../shared/ui/SelectField';
import { TextField } from '../../shared/ui/TextField';
import { UnsavedChangesGuard } from '../../shared/ui/UnsavedChangesGuard';
import { useToast } from '../../shared/ui/useToast';
import { withCurrentOption } from '../admin/shared/referenceOptions';
import { useSession } from '../auth/session';

import { ASSET_TYPES, useAssetCatalog, useCreateAsset, useReferenceDepartments, useReferenceLocations, useUpdateAsset, type AssetItem } from './api';

/** Mirror backend Asset.*MaxLength. */
const NAME_MAX_LENGTH = 100;
const ADMIN_ROLE = 'admin';
const NOTES_MAX_LENGTH = 1000;
const NO_SELECTION = '';

const optional = (max: number) => z.string().trim().max(max, ERROR_CODES.tooLong);

const assetSchema = z.object({
  // Type and brand only narrow the model list; the model (from the admin catalog, ADR-0023) is what is saved.
  type: z.string().min(1, ERROR_CODES.required),
  brandId: z.string(),
  modelId: z.string().min(1, ERROR_CODES.required),
  serialNumber: optional(NAME_MAX_LENGTH),
  locationId: z.string().min(1, ERROR_CODES.required),
  departmentId: z.string(),
  warrantyEndsOn: z.string(),
  purchasedOn: z.string(),
  notes: optional(NOTES_MAX_LENGTH),
});

type AssetFormValues = z.infer<typeof assetSchema>;

const FORM_FIELDS = ['type', 'brandId', 'modelId', 'serialNumber', 'locationId', 'departmentId', 'warrantyEndsOn', 'purchasedOn', 'notes'] as const;
const CODE_FIELDS = {
  [ERROR_CODES.assetSerialTaken]: 'serialNumber',
  [ERROR_CODES.referenceInactive]: 'locationId',
  [ERROR_CODES.assetModelInactive]: 'modelId',
} as const;

const nullIfEmpty = (value: string) => (value === NO_SELECTION ? null : value);

export function AssetFormDialog({ asset, onClose }: { asset: AssetItem | null; onClose: () => void }) {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const formId = useId();
  const locations = useReferenceLocations();
  const departments = useReferenceDepartments();
  const create = useCreateAsset();
  const update = useUpdateAsset();
  const mutation = asset === null ? create : update;
  const catalog = useAssetCatalog();
  const me = useSession();
  const toast = useToast();

  const typeOptions = ASSET_TYPES.map((type) => ({ value: type, label: t(`types.${type}`) }));
  const locationOptions = useMemo(
    () => withCurrentOption((locations.data ?? []).map((l) => ({ value: l.id, label: l.path })), asset?.locationId, asset?.locationName),
    [locations.data, asset],
  );
  const departmentOptions = useMemo(
    () => withCurrentOption((departments.data ?? []).map((d) => ({ value: d.id, label: d.name })), asset?.departmentId, asset?.departmentName),
    [departments.data, asset],
  );

  const {
    register,
    control,
    handleSubmit,
    setError,
    setValue,
    formState: { errors, isDirty },
  } = useForm<AssetFormValues>({
    resolver: zodResolver(assetSchema),
    defaultValues: {
      type: asset?.type ?? NO_SELECTION,
      brandId: NO_SELECTION,
      modelId: asset?.modelId ?? NO_SELECTION,
      serialNumber: asset?.serialNumber ?? '',
      locationId: asset?.locationId ?? NO_SELECTION,
      departmentId: asset?.departmentId ?? NO_SELECTION,
      warrantyEndsOn: asset?.warrantyEndsOn ?? '',
      purchasedOn: asset?.purchasedOn ?? '',
      notes: asset?.notes ?? '',
    },
  });

  const type = useWatch({ control, name: 'type' });
  const brandIdValue = useWatch({ control, name: 'brandId' });
  const brands = useMemo(() => (catalog.data ?? []).filter((b) => b.models.some((m) => m.type === type)), [catalog.data, type]);
  // An edited asset starts on its own brand; its model may have left the active catalog and is then shown as is.
  const brandId = brandIdValue !== NO_SELECTION ? brandIdValue : (catalog.data?.find((b) => b.models.some((m) => m.id === asset?.modelId))?.id ?? NO_SELECTION);
  const modelOptions = useMemo(
    () =>
      withCurrentOption(
        (catalog.data?.find((b) => b.id === brandId)?.models ?? []).filter((m) => m.type === type).map((m) => ({ value: m.id, label: m.name })),
        asset?.modelId,
        asset?.model,
      ),
    [catalog.data, brandId, type, asset],
  );

  const onSubmit = handleSubmit((values) => {
    const body = {
      modelId: values.modelId,
      serialNumber: nullIfEmpty(values.serialNumber),
      locationId: values.locationId,
      departmentId: nullIfEmpty(values.departmentId),
      warrantyEndsOn: nullIfEmpty(values.warrantyEndsOn),
      purchasedOn: nullIfEmpty(values.purchasedOn),
      notes: nullIfEmpty(values.notes),
    };
    const callbacks = { onSuccess: onClose, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (asset === null) {
      // The server assigns the tag (ADR-0023); show the user which number the device got.
      create.mutate(body, { ...callbacks, onSuccess: (created) => {
        toast.show(t('form.created', { tag: created.assetTag }));
        onClose();
      } });
    } else {
      update.mutate({ id: asset.id, version: asset.version, ...body }, callbacks);
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;

  return (
    <>
    <UnsavedChangesGuard when={isDirty && !mutation.isPending} />
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={asset === null ? t('form.new') : t('form.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={mutation.isPending}>
            {asset === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="row g-3" onSubmit={(event) => void onSubmit(event)}>
        <div className="col-12">
          <FormAlert message={alert} />
        </div>
        {asset === null && <p className="col-12 small text-body-secondary mb-0">{t('form.tagGenerated')}</p>}
        <SelectField className="col-12 col-sm-6" label={t('fields.type')} options={typeOptions} placeholder={t('form.selectType')} error={fieldError(errors.type)} {...register('type')} />
        <div className="col-12 col-sm-6">
          <Controller
            control={control}
            name="brandId"
            render={({ field }) => (
              <ComboboxField
                label={t('fields.brand')}
                options={brands.map((b) => ({ value: b.id, label: b.name }))}
                value={field.value === NO_SELECTION ? brandId : field.value}
                emptyText={t('form.brandEmpty')}
                error={fieldError(errors.brandId)}
                onChange={(value) => {
                  field.onChange(value);
                  setValue('modelId', NO_SELECTION, { shouldDirty: true });
                }}
              />
            )}
          />
        </div>
        <SelectField className="col-12 col-sm-6" label={t('fields.model')} options={modelOptions} placeholder={t('form.selectModel')} error={fieldError(errors.modelId)} {...register('modelId')} />
        <p className="col-12 small text-body-secondary mb-0">
          {t('form.catalogMissing')}{' '}
          {me.role === ADMIN_ROLE && <Link to={ROUTES.adminModels}>{t('form.editCatalog')}</Link>}
        </p>
        <TextField className="col-12 col-sm-6" label={t('fields.serialNumber')} autoComplete="off" error={fieldError(errors.serialNumber)} {...register('serialNumber')} />
        <SelectField className="col-12 col-sm-6" label={t('fields.location')} options={locationOptions} placeholder={t('form.selectLocation')} error={fieldError(errors.locationId)} {...register('locationId')} />
        <SelectField className="col-12 col-sm-6" label={t('fields.department')} options={departmentOptions} placeholder={tc('none')} error={fieldError(errors.departmentId)} {...register('departmentId')} />
        <TextField className="col-12 col-sm-6" type="date" label={t('fields.warrantyEndsOn')} error={fieldError(errors.warrantyEndsOn)} {...register('warrantyEndsOn')} />
        <TextField className="col-12 col-sm-6" type="date" label={t('fields.purchasedOn')} error={fieldError(errors.purchasedOn)} {...register('purchasedOn')} />
        <TextField className="col-12" label={t('fields.notes')} autoComplete="off" error={fieldError(errors.notes)} {...register('notes')} />
      </form>
    </Dialog>
    </>
  );
}
