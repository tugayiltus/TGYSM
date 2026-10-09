import { zodResolver } from '@hookform/resolvers/zod';
import { useId } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { Button } from '../../../shared/ui/Button';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { SelectField } from '../../../shared/ui/SelectField';
import { TextField } from '../../../shared/ui/TextField';
import { withCurrentOption } from '../shared/referenceOptions';

import { useCreateLocation, useUpdateLocation } from './api';
import { collectSubtreeIds, findLocationNode, flattenLocationTree, type LocationNode } from './locationTree';

/** Mirrors backend Location.NameMaxLength. */
const LOCATION_NAME_MAX_LENGTH = 150;
const NO_PARENT = '';

const locationSchema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(LOCATION_NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  parentId: z.string(),
});

type LocationFormValues = z.infer<typeof locationSchema>;

const FORM_FIELDS = ['name', 'parentId'] as const;
const CODE_FIELDS = {
  [ERROR_CODES.locationNameTaken]: 'name',
  [ERROR_CODES.locationCycle]: 'parentId',
  [ERROR_CODES.referenceInactive]: 'parentId',
} as const;

export type LocationFormTarget = { kind: 'create'; parentId: string | null } | { kind: 'edit'; node: LocationNode };

export function LocationFormDialog({ roots, target, onClose }: { roots: readonly LocationNode[]; target: LocationFormTarget; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const create = useCreateLocation();
  const update = useUpdateLocation();
  const mutation = target.kind === 'create' ? create : update;

  const excluded = target.kind === 'edit' ? collectSubtreeIds(target.node) : new Set<string>();
  const currentParentId = target.kind === 'edit' ? target.node.item.parentId : target.parentId;
  const parentOptions = withCurrentOption(
    flattenLocationTree(roots)
      .filter((node) => node.item.isActive && !excluded.has(node.item.id))
      .map((node) => ({ value: node.item.id, label: node.path })),
    currentParentId,
    currentParentId ? findLocationNode(roots, currentParentId)?.path : undefined,
  );

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<LocationFormValues>({
    resolver: zodResolver(locationSchema),
    defaultValues: { name: target.kind === 'edit' ? target.node.item.name : '', parentId: currentParentId ?? NO_PARENT },
  });

  const onSubmit = handleSubmit(({ name, parentId }) => {
    const body = { name, parentId: parentId === NO_PARENT ? null : parentId };
    const callbacks = { onSuccess: onClose, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (target.kind === 'create') {
      create.mutate(body, callbacks);
    } else {
      update.mutate({ id: target.node.item.id, version: target.node.item.version, ...body }, callbacks);
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={target.kind === 'create' ? t('locations.new') : t('locations.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={mutation.isPending}>
            {target.kind === 'create' ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="row g-3" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('locations.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
        <SelectField
          label={t('locations.fields.parent')}
          options={parentOptions}
          placeholder={t('locations.noParent')}
          error={fieldError(errors.parentId)}
          {...register('parentId')}
        />
      </form>
    </Dialog>
  );
}
