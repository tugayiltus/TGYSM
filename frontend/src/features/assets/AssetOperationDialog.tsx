/**
 * @input An asset, the lifecycle operation and a close callback.
 * @output A dialog with the input that operation needs: a user, a location, a reason or a choice.
 * @position Opened from the asset detail page.
 */
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useMemo, useState } from 'react';
import { useForm, type Control } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { FormCheckbox, FormSelector, FormTextInput } from '../../shared/form/formFields';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';

import { useAssetOperation, useReferenceLocations, useUserSearch, type AssetItem, type AssetOperation, type AssetOperationKind } from './api';

/** Mirrors backend AssetMovement.ReasonMaxLength and AssetAssignment.NoteMaxLength. */
const TEXT_MAX_LENGTH = 500;
const SEARCH_DEBOUNCE_MS = 300;
const DIALOG_WIDTH = 520;

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

function UserPicker({ control, onSearchChange }: { control: Control<OperationValues>; onSearchChange: () => void }) {
  const { t } = useTranslation('assets');
  const [search, setSearch] = useState('');
  const users = useUserSearch(useDebouncedValue(search, SEARCH_DEBOUNCE_MS));
  const options = useMemo(() => (users.data ?? []).map((u) => ({ value: u.id, label: `${u.fullName} (${u.email})` })), [users.data]);
  return (
    <>
      <TextInput
        type="search"
        label={t('actionFields.userSearch')}
        value={search}
        onChange={(value) => {
          // The chosen user may vanish from the new results; a stale hidden value must not be sent.
          onSearchChange();
          setSearch(value);
        }}
      />
      <FormSelector control={control} name="userId" label={t('actionFields.user')} options={options} placeholder={t('actionFields.selectUser')} />
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
  const { control, setValue, handleSubmit } = useForm<OperationValues>({ resolver: zodResolver(SCHEMAS[kind]), defaultValues: DEFAULT_VALUES });

  const onSubmit = handleSubmit((values) => operation.mutate(toOperation(kind, values), { onSuccess: onClose }));
  const close = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };

  return (
    <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH} purpose="form">
      <Layout
        header={<DialogHeader title={`${t(`actions.${kind}`)} · ${asset.assetTag}`} onOpenChange={close} />}
        content={
          <LayoutContent>
            <form id={formId} noValidate onSubmit={(event) => void onSubmit(event)}>
              <VStack gap={4}>
                <FormAlert message={operation.error ? describeError(operation.error) : null} />
                {(kind === 'assign' || kind === 'transfer') && (
                  <>
                    <UserPicker control={control} onSearchChange={() => setValue('userId', '')} />
                    <FormTextInput control={control} name="note" label={t('actionFields.note')} autoComplete="off" />
                  </>
                )}
                {(kind === 'return' || kind === 'found') && (
                  <FormSelector control={control} name="locationId" label={t('actionFields.location')} options={locationOptions} placeholder={t('form.selectLocation')} />
                )}
                {(kind === 'lost' || kind === 'retire') && <FormTextInput control={control} name="reason" label={t('actionFields.reason')} autoComplete="off" />}
                {kind === 'maintenanceStart' && asset.status === 'assigned' && <FormCheckbox control={control} name="retainResponsibility" label={t('actionFields.retain')} />}
                {kind === 'maintenanceEnd' && <Text type="supporting">{t('actionFields.maintenanceEndHint')}</Text>}
              </VStack>
            </form>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack gap={2} hAlign="end">
              <Button label={tc('actions.cancel')} variant="secondary" onClick={onClose} />
              <Button type="submit" form={formId} label={tc('actions.confirm')} variant={kind === 'lost' || kind === 'retire' ? 'destructive' : 'primary'} isLoading={operation.isPending} />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
  );
}
