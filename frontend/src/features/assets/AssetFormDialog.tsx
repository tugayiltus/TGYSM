/**
 * @input The asset to edit, or null to create one, and a close callback.
 * @output The asset form: device type, brand and model from the catalog, serial, location, department, dates and notes.
 * @position Opened from the inventory list (create) and the asset detail page (edit).
 */
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { Grid } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { Link } from '@astryxdesign/core/Link';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { useToast } from '@astryxdesign/core/Toast';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useMemo } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { UnsavedChangesGuard } from '../../shared/feedback/UnsavedChangesGuard';
import { FormDateInput, FormSelector, FormTextInput } from '../../shared/form/formFields';
import { TURKISH_LOCALE } from '../../shared/lib/turkish';
import { ROUTES } from '../../shared/routes';
import { withCurrentOption } from '../admin/shared/referenceOptions';
import { useSession } from '../auth/session';

import { ASSET_TYPES, useAssetCatalog, useCreateAsset, useReferenceDepartments, useReferenceLocations, useUpdateAsset, type AssetItem } from './api';

/** Mirror backend Asset.*MaxLength. */
const NAME_MAX_LENGTH = 100;
const ADMIN_ROLE = 'admin';
const NOTES_MAX_LENGTH = 1000;
const NO_SELECTION = '';
/** Choice value of an edited asset's brand that is no longer in the active catalog. */
const OWN_BRAND = 'own-brand';
const DIALOG_WIDTH = 640;
/** Paired fields sit side by side from this width. */
const FIELD_COLUMNS = { minWidth: 240 } as const;

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
  const showToast = useToast();

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
  // An edited asset keeps its own brand and (possibly inactive) model as choices, but only while its own type
  // and brand are both chosen: a model belongs to one brand and one type. Its brand may have left the active
  // catalog, so the brand is found by model, then by name, else kept as a choice of its own.
  const ownType = asset !== null && type === asset.type;
  const ownBrandId =
    asset === null
      ? NO_SELECTION
      : (catalog.data?.find((b) => b.models.some((m) => m.id === asset.modelId))?.id ?? catalog.data?.find((b) => b.name === asset.brand)?.id ?? OWN_BRAND);
  const brandId = brandIdValue !== NO_SELECTION || !ownType ? brandIdValue : ownBrandId;
  const brandOptions = useMemo(
    () =>
      withCurrentOption(
        (catalog.data ?? []).filter((b) => b.models.some((m) => m.type === type)).map((b) => ({ value: b.id, label: b.name })),
        ownType ? ownBrandId : undefined,
        ownType ? asset.brand : undefined,
      ),
    [catalog.data, type, asset, ownType, ownBrandId],
  );
  const ownBrand = ownType && brandId === ownBrandId;
  const modelOptions = useMemo(
    () =>
      withCurrentOption(
        (catalog.data?.find((b) => b.id === brandId)?.models ?? []).filter((m) => m.type === type).map((m) => ({ value: m.id, label: m.name })),
        ownBrand ? asset.modelId : undefined,
        ownBrand ? asset.model : undefined,
      ),
    [catalog.data, brandId, type, asset, ownBrand],
  );
  const typeName = (typeOptions.find((option) => option.value === type)?.label ?? '').toLocaleLowerCase(TURKISH_LOCALE);

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
        showToast({ body: t('form.created', { tag: created.assetTag }) });
        onClose();
      } });
    } else {
      update.mutate({ id: asset.id, version: asset.version, ...body }, callbacks);
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;

  const close = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };
  // Brands and models are listed per type, so a choice made for another type no longer applies.
  const clearCatalogChoice = () => {
    setValue('brandId', NO_SELECTION, { shouldDirty: true });
    setValue('modelId', NO_SELECTION, { shouldDirty: true });
  };

  return (
    <>
      <UnsavedChangesGuard when={isDirty && !mutation.isPending} />
      <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH} purpose="form">
        <Layout
          header={<DialogHeader title={asset === null ? t('form.new') : t('form.edit')} onOpenChange={close} />}
          content={
            <LayoutContent>
              <form id={formId} noValidate onSubmit={(event) => void onSubmit(event)}>
                <VStack gap={4}>
                  <FormAlert message={alert} />
                  {asset === null && <Text type="supporting">{t('form.tagGenerated')}</Text>}
                  <Grid columns={FIELD_COLUMNS} gap={4}>
                    <FormSelector control={control} name="type" label={t('fields.type')} options={typeOptions} placeholder={t('form.selectType')} onValueChange={clearCatalogChoice} />
                    <Controller
                      control={control}
                      name="brandId"
                      render={({ field }) => (
                        <Selector
                          label={t('fields.brand')}
                          options={brandOptions}
                          value={field.value === NO_SELECTION ? brandId : field.value}
                          placeholder={t('form.selectBrand')}
                          hasSearch
                          searchPlaceholder={t('form.searchBrand')}
                          emptyText={t('form.brandEmpty')}
                          isDisabled={type === NO_SELECTION}
                          disabledMessage={t('form.selectTypeFirst')}
                          status={fieldError(errors.brandId) === undefined ? undefined : { type: 'error', message: fieldError(errors.brandId) }}
                          onChange={(value) => {
                            field.onChange(value);
                            setValue('modelId', NO_SELECTION, { shouldDirty: true });
                          }}
                        />
                      )}
                    />
                    <FormSelector
                      control={control}
                      name="modelId"
                      label={t('fields.model')}
                      options={modelOptions}
                      placeholder={t('form.selectModel')}
                      emptyText={t('form.modelEmpty', { type: typeName })}
                      isDisabled={brandId === NO_SELECTION}
                      disabledMessage={t('form.selectBrandFirst')}
                    />
                  </Grid>
                  <HStack gap={1} wrap="wrap">
                    <Text type="supporting">{t('form.catalogMissing')}</Text>
                    {me.role === ADMIN_ROLE && <Link href={ROUTES.adminModels}>{t('form.editCatalog')}</Link>}
                  </HStack>
                  <Grid columns={FIELD_COLUMNS} gap={4}>
                    <FormTextInput control={control} name="serialNumber" label={t('fields.serialNumber')} autoComplete="off" />
                    <FormSelector control={control} name="locationId" label={t('fields.location')} options={locationOptions} placeholder={t('form.selectLocation')} />
                    <FormSelector control={control} name="departmentId" label={t('fields.department')} options={departmentOptions} placeholder={tc('none')} isClearable />
                    <FormDateInput control={control} name="warrantyEndsOn" label={t('fields.warrantyEndsOn')} />
                    <FormDateInput control={control} name="purchasedOn" label={t('fields.purchasedOn')} />
                  </Grid>
                  <FormTextInput control={control} name="notes" label={t('fields.notes')} autoComplete="off" />
                </VStack>
              </form>
            </LayoutContent>
          }
          footer={
            <LayoutFooter>
              <HStack gap={2} hAlign="end">
                <Button label={tc('actions.cancel')} variant="secondary" onClick={onClose} />
                <Button type="submit" form={formId} label={asset === null ? tc('actions.create') : tc('actions.save')} variant="primary" isLoading={mutation.isPending} />
              </HStack>
            </LayoutFooter>
          }
        />
      </Dialog>
    </>
  );
}
