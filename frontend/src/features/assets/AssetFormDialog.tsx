import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { Button } from '../../shared/ui/Button';
import { Dialog } from '../../shared/ui/Dialog';
import { FormAlert } from '../../shared/ui/FormAlert';
import { SelectField } from '../../shared/ui/SelectField';
import { TextField } from '../../shared/ui/TextField';
import { UnsavedChangesGuard } from '../../shared/ui/UnsavedChangesGuard';
import { withCurrentOption } from '../admin/shared/referenceOptions';

import { ASSET_TYPES, useCreateAsset, useReferenceDepartments, useReferenceLocations, useUpdateAsset, type AssetItem, type AssetType } from './api';

/** Mirror backend Asset.*MaxLength. */
const TAG_MAX_LENGTH = 50;
const NAME_MAX_LENGTH = 100;
const NOTES_MAX_LENGTH = 1000;
const NO_SELECTION = '';

const required = (max: number) => z.string().trim().min(1, ERROR_CODES.required).max(max, ERROR_CODES.tooLong);
const optional = (max: number) => z.string().trim().max(max, ERROR_CODES.tooLong);

const assetSchema = z.object({
  assetTag: required(TAG_MAX_LENGTH),
  type: z.string().min(1, ERROR_CODES.required),
  brand: required(NAME_MAX_LENGTH),
  model: required(NAME_MAX_LENGTH),
  serialNumber: optional(NAME_MAX_LENGTH),
  locationId: z.string().min(1, ERROR_CODES.required),
  departmentId: z.string(),
  warrantyEndsOn: z.string(),
  purchasedOn: z.string(),
  notes: optional(NOTES_MAX_LENGTH),
});

type AssetFormValues = z.infer<typeof assetSchema>;

const FORM_FIELDS = ['assetTag', 'type', 'brand', 'model', 'serialNumber', 'locationId', 'departmentId', 'warrantyEndsOn', 'purchasedOn', 'notes'] as const;
const CODE_FIELDS = {
  [ERROR_CODES.assetTagTaken]: 'assetTag',
  [ERROR_CODES.assetSerialTaken]: 'serialNumber',
  [ERROR_CODES.referenceInactive]: 'locationId',
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
    handleSubmit,
    setError,
    formState: { errors, isDirty },
  } = useForm<AssetFormValues>({
    resolver: zodResolver(assetSchema),
    defaultValues: {
      assetTag: asset?.assetTag ?? '',
      type: asset?.type ?? NO_SELECTION,
      brand: asset?.brand ?? '',
      model: asset?.model ?? '',
      serialNumber: asset?.serialNumber ?? '',
      locationId: asset?.locationId ?? NO_SELECTION,
      departmentId: asset?.departmentId ?? NO_SELECTION,
      warrantyEndsOn: asset?.warrantyEndsOn ?? '',
      purchasedOn: asset?.purchasedOn ?? '',
      notes: asset?.notes ?? '',
    },
  });

  const onSubmit = handleSubmit((values) => {
    const body = {
      assetTag: values.assetTag,
      type: values.type as AssetType,
      brand: values.brand,
      model: values.model,
      serialNumber: nullIfEmpty(values.serialNumber),
      locationId: values.locationId,
      departmentId: nullIfEmpty(values.departmentId),
      warrantyEndsOn: nullIfEmpty(values.warrantyEndsOn),
      purchasedOn: nullIfEmpty(values.purchasedOn),
      notes: nullIfEmpty(values.notes),
    };
    const callbacks = { onSuccess: onClose, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (asset === null) {
      create.mutate(body, callbacks);
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
        <TextField className="col-12 col-sm-6" label={t('fields.assetTag')} autoComplete="off" error={fieldError(errors.assetTag)} {...register('assetTag')} />
        <SelectField className="col-12 col-sm-6" label={t('fields.type')} options={typeOptions} placeholder={t('form.selectType')} error={fieldError(errors.type)} {...register('type')} />
        <TextField className="col-12 col-sm-6" label={t('fields.brand')} autoComplete="off" error={fieldError(errors.brand)} {...register('brand')} />
        <TextField className="col-12 col-sm-6" label={t('fields.model')} autoComplete="off" error={fieldError(errors.model)} {...register('model')} />
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
