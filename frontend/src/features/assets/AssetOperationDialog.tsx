import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError, fieldError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { Button } from '../../shared/ui/Button';
import { CheckField } from '../../shared/ui/CheckField';
import { Dialog } from '../../shared/ui/Dialog';
import { FormAlert } from '../../shared/ui/FormAlert';
import { SelectField } from '../../shared/ui/SelectField';
import { TextField } from '../../shared/ui/TextField';

import { useAssetOperation, useReferenceLocations, useUserSearch, type AssetItem, type AssetOperation, type AssetOperationKind } from './api';

/** Mirrors backend AssetMovement.ReasonMaxLength and AssetAssignment.NoteMaxLength. */
const TEXT_MAX_LENGTH = 500;
const SEARCH_DEBOUNCE_MS = 300;

const requiredText = z.string().trim().min(1, ERROR_CODES.required).max(TEXT_MAX_LENGTH, ERROR_CODES.tooLong);
const base = z.object({
  userId: z.string(),
  locationId: z.string(),
  reason: z.string(),
  note: z.string().trim().max(TEXT_MAX_LENGTH, ERROR_CODES.tooLong),
  retainResponsibility: z.boolean(),
});
const withUser = base.extend({ userId: z.string().min(1, ERROR_CODES.required) });
const withLocation = base.extend({ locationId: z.string().min(1, ERROR_CODES.required) });
const withReason = base.extend({ reason: requiredText });

const SCHEMAS: Record<AssetOperationKind, typeof base> = {
  assign: withUser,
  transfer: withUser,
  return: withLocation,
  found: withLocation,
  lost: withReason,
  retire: withReason,
  maintenanceStart: base,
  maintenanceEnd: base,
};

type OperationValues = z.infer<typeof base>;

const DEFAULT_VALUES: OperationValues = { userId: '', locationId: '', reason: '', note: '', retainResponsibility: false };

function toOperation(kind: AssetOperationKind, v: OperationValues): AssetOperation {
  const note = v.note === '' ? null : v.note;
  switch (kind) {
    case 'assign':
    case 'transfer':
      return { kind, userId: v.userId, note };
    case 'return':
    case 'found':
      return { kind, locationId: v.locationId };
    case 'lost':
    case 'retire':
      return { kind, reason: v.reason.trim() };
    case 'maintenanceStart':
      return { kind, retainResponsibility: v.retainResponsibility };
    case 'maintenanceEnd':
      return { kind };
  }
}

function UserPicker({ error, register, onSearchChange }: { error: string | undefined; register: ReturnType<typeof useForm<OperationValues>>['register']; onSearchChange: () => void }) {
  const { t } = useTranslation('assets');
  const [search, setSearch] = useState('');
  const users = useUserSearch(useDebouncedValue(search, SEARCH_DEBOUNCE_MS));
  const options = useMemo(() => (users.data ?? []).map((u) => ({ value: u.id, label: `${u.fullName} (${u.email})` })), [users.data]);
  return (
    <>
      <TextField type="search" label={t('actionFields.userSearch')} value={search} onChange={(event) => {
          // The chosen user may vanish from the new results; a stale hidden value must not be sent.
          onSearchChange();
          setSearch(event.target.value);
        }}
      />
      <SelectField label={t('actionFields.user')} options={options} placeholder={t('actionFields.selectUser')} error={error} {...register('userId')} />
    </>
  );
}

export function AssetOperationDialog({ asset, kind, onClose }: { asset: AssetItem; kind: AssetOperationKind; onClose: () => void }) {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const formId = useId();
  const operation = useAssetOperation(asset);
  const locations = useReferenceLocations();
  const locationOptions = useMemo(() => (locations.data ?? []).map((l) => ({ value: l.id, label: l.path })), [locations.data]);
  const {
    register,
    setValue,
    handleSubmit,
    formState: { errors },
  } = useForm<OperationValues>({ resolver: zodResolver(SCHEMAS[kind]), defaultValues: DEFAULT_VALUES });

  const onSubmit = handleSubmit((values) => operation.mutate(toOperation(kind, values), { onSuccess: onClose }));

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={`${t(`actions.${kind}`)} · ${asset.assetTag}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} variant={kind === 'lost' || kind === 'retire' ? 'danger' : 'primary'} isLoading={operation.isPending}>
            {tc('actions.confirm')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="row g-3" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={operation.error ? describeError(operation.error) : null} />
        {(kind === 'assign' || kind === 'transfer') && (
          <>
            <UserPicker error={fieldError(errors.userId)} register={register} onSearchChange={() => setValue('userId', '')} />
            <TextField label={t('actionFields.note')} autoComplete="off" error={fieldError(errors.note)} {...register('note')} />
          </>
        )}
        {(kind === 'return' || kind === 'found') && (
          <SelectField label={t('actionFields.location')} options={locationOptions} placeholder={t('form.selectLocation')} error={fieldError(errors.locationId)} {...register('locationId')} />
        )}
        {(kind === 'lost' || kind === 'retire') && <TextField label={t('actionFields.reason')} autoComplete="off" error={fieldError(errors.reason)} {...register('reason')} />}
        {kind === 'maintenanceStart' && asset.status === 'assigned' && (
          <CheckField label={t('actionFields.retain')} {...register('retainResponsibility')} />
        )}
        {kind === 'maintenanceEnd' && <p className="small text-body-secondary">{t('actionFields.maintenanceEndHint')}</p>}
      </form>
    </Dialog>
  );
}
