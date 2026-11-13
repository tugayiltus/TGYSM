/**
 * @input An optional change (edit mode), a saved callback and a close callback.
 * @output A dialog that creates a change, or edits a draft, a pending or an approved plan.
 * @position Change list page and the change detail page.
 */
import { Button } from '@astryxdesign/core/Button';
import { List, ListItem } from '@astryxdesign/core/List';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { ConfirmDialog } from '../../shared/feedback/ConfirmDialog';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { FormDateTimeInput, FormSelector, FormTextArea, FormTextInput } from '../../shared/form/formFields';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { useAssets } from '../assets/api';
import { useSession } from '../auth/session';
import { useReferenceTeams } from '../tickets/api';

import { LEVELS, useCreateChange, useEditChange, type ChangeDetail, type ChangeDraft, type Level } from './api';
import { fromLocalInput, toLocalInput } from './dateTime';

/** Mirror backend ChangeRequest.TitleMaxLength / AffectedServiceMaxLength / TextMaxLength. */
const TITLE_MAX_LENGTH = 200;
const SERVICE_MAX_LENGTH = 200;
const TEXT_MAX_LENGTH = 4000;
const DEFAULT_LEVEL: Level = 'medium';
const ASSET_SEARCH_DEBOUNCE_MS = 300;
const ASSET_SEARCH_PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const LABEL_SEPARATOR = ' · ';
const DIALOG_WIDTH = 640;

const optionalText = (max: number) => z.string().trim().max(max, ERROR_CODES.tooLong);

const fields = z.object({
    title: z.string().trim().min(1, ERROR_CODES.required).max(TITLE_MAX_LENGTH, ERROR_CODES.tooLong),
    justification: optionalText(TEXT_MAX_LENGTH),
    affectedService: optionalText(SERVICE_MAX_LENGTH),
    risk: z.string().min(1, ERROR_CODES.required),
    impact: z.string().min(1, ERROR_CODES.required),
    windowStart: z.string(),
    windowEnd: z.string(),
    implementationPlan: optionalText(TEXT_MAX_LENGTH),
    verificationPlan: optionalText(TEXT_MAX_LENGTH),
    backoutPlan: optionalText(TEXT_MAX_LENGTH),
    ownerTeamId: z.string(),
    ownerId: z.string(),
  });
type Values = z.infer<typeof fields>;

const windowIsOrdered = (v: Values) => v.windowStart === '' || v.windowEnd === '' || v.windowEnd > v.windowStart;
const WINDOW_ERROR = { path: ['windowEnd'], message: 'changes:form.invalidWindow' };
const editSchema = fields.refine(windowIsOrdered, WINDOW_ERROR);
/** A new change needs an owner team and an owner; an edit keeps the ones it has. */
const createSchema = fields
  .extend({ ownerTeamId: z.string().min(1, ERROR_CODES.required), ownerId: z.string().min(1, ERROR_CODES.required) })
  .refine(windowIsOrdered, WINDOW_ERROR);

interface PickedAsset {
  id: string;
  label: string;
}

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());

/** Fields the approval fingerprint covers (the assets are compared separately). */
const FINGERPRINT_KEYS = ['windowStart', 'windowEnd', 'risk', 'impact', 'implementationPlan', 'verificationPlan', 'backoutPlan'] as const;

function initialValues(change: ChangeDetail | undefined, ownerId: string): Values {
  return {
    title: change?.title ?? '',
    justification: change?.justification ?? '',
    affectedService: change?.affectedService ?? '',
    risk: change?.risk ?? DEFAULT_LEVEL,
    impact: change?.impact ?? DEFAULT_LEVEL,
    windowStart: toLocalInput(change?.windowStart ?? null),
    windowEnd: toLocalInput(change?.windowEnd ?? null),
    implementationPlan: change?.implementationPlan ?? '',
    verificationPlan: change?.verificationPlan ?? '',
    backoutPlan: change?.backoutPlan ?? '',
    ownerTeamId: change?.ownerTeamId ?? '',
    ownerId: change?.ownerId ?? ownerId,
  };
}

const assetLabel = (tag: string, brand: string, model: string) => [tag, `${brand} ${model}`].join(LABEL_SEPARATOR);

function sameSet(a: readonly string[], b: readonly string[]) {
  return a.length === b.length && a.every((id) => b.includes(id));
}

function AssetPicker({ picked, onChange }: { picked: PickedAsset[]; onChange: (next: PickedAsset[]) => void }) {
  const { t } = useTranslation('changes');
  const [search, setSearch] = useState('');
  const debounced = useDebouncedValue(search, ASSET_SEARCH_DEBOUNCE_MS);
  const inventory = useAssets({ search: debounced, type: undefined, status: undefined, page: FIRST_PAGE, pageSize: ASSET_SEARCH_PAGE_SIZE });
  const options = useMemo(
    () =>
      (inventory.data?.items ?? [])
        .filter((a) => !picked.some((p) => p.id === a.id))
        .map((a) => ({ value: a.id, label: assetLabel(a.assetTag, a.brand, a.model) })),
    [inventory.data, picked],
  );
  const add = (id: string) => {
    const found = options.find((o) => o.value === id);
    if (found !== undefined) {
      onChange([...picked, { id, label: found.label }]);
    }
  };
  return (
    <VStack gap={2}>
      <TextInput type="search" label={t('form.assetSearch')} value={search} onChange={setSearch} />
      <Selector label={t('form.assetPick')} placeholder={t('form.select')} options={options} value="" onChange={add} />
      {picked.length === 0 ? (
        <Text type="supporting">{t('form.noAssets')}</Text>
      ) : (
        <List hasDividers density="compact" aria-label={t('form.assets')}>
          {picked.map((asset) => (
            <ListItem
              key={asset.id}
              label={asset.label}
              endContent={
                <Button
                  label={t('detail.removeAsset')}
                  aria-label={t('form.assetRemove', { tag: asset.label.split(LABEL_SEPARATOR)[0] })}
                  variant="ghost"
                  onClick={() => onChange(picked.filter((p) => p.id !== asset.id))}
                />
              }
            />
          ))}
        </List>
      )}
    </VStack>
  );
}

export function ChangeFormDialog({ change, onSaved, onClose }: { change?: ChangeDetail; onSaved: (change: ChangeDetail) => void; onClose: () => void }) {
  const { t } = useTranslation('changes');
  const me = useSession();
  const teams = useReferenceTeams(true);
  const create = useCreateChange();
  // The version the user saw when opening the dialog; a background refetch must not silently skip the conflict check.
  const [openedVersion] = useState(change?.version ?? 0);
  const edit = useEditChange(change?.number ?? '', openedVersion);
  const [picked, setPicked] = useState<PickedAsset[]>(() => (change?.assets ?? []).map((a) => ({ id: a.id, label: assetLabel(a.assetTag, a.brand, a.model) })));
  const [pendingDraft, setPendingDraft] = useState<{ draft: ChangeDraft } | null>(null);
  const [initial] = useState(() => initialValues(change, me.id));
  const { control, setValue, handleSubmit, formState } = useForm<Values>({ resolver: zodResolver(change === undefined ? createSchema : editSchema), defaultValues: initial });
  const teamId = useWatch({ control, name: 'ownerTeamId' });
  const memberOptions = useMemo(
    () => (teams.data?.find((team) => team.id === teamId)?.members ?? []).map((m) => ({ value: m.id, label: m.fullName })),
    [teams.data, teamId],
  );
  const [initialAssetIds] = useState(() => (change?.assets ?? []).map((a) => a.id));
  const assetIds = picked.map((p) => p.id);
  const mutation = change === undefined ? create : edit;

  const save = (draft: ChangeDraft, values: Values) => {
    if (change === undefined) {
      create.mutate({ draft, ownerTeamId: values.ownerTeamId, ownerId: values.ownerId, assetIds }, { onSuccess: onSaved });
    } else {
      edit.mutate({ draft, assetIds }, { onSuccess: onSaved });
    }
  };
  const onSubmit = handleSubmit((v) => {
    const draft: ChangeDraft = {
      title: v.title.trim(),
      justification: orNull(v.justification),
      affectedService: orNull(v.affectedService),
      risk: v.risk as Level,
      impact: v.impact as Level,
      windowStart: fromLocalInput(v.windowStart),
      windowEnd: fromLocalInput(v.windowEnd),
      implementationPlan: orNull(v.implementationPlan),
      verificationPlan: orNull(v.verificationPlan),
      backoutPlan: orNull(v.backoutPlan),
    };
    const touchesFingerprint = FINGERPRINT_KEYS.some((key) => v[key].trim() !== initial[key].trim()) || !sameSet(assetIds, initialAssetIds);
    if (change?.status === 'scheduled' && touchesFingerprint) {
      setPendingDraft({ draft });
      return;
    }

    save(draft, v);
  });
  const levelOptions = LEVELS.map((value) => ({ value, label: t(`levels.${value}`) }));
  return (
    <>
      <FormDialog
        title={change === undefined ? t('form.createTitle') : `${t('form.editTitle')} · ${change.number}`}
        submitLabel={change === undefined ? t('form.submitCreate') : t('form.submitEdit')}
        isPending={mutation.isPending}
        alert={mutation.error ? describeError(mutation.error) : null}
        onSubmit={(event) => void onSubmit(event)}
        onClose={onClose}
        isDirty={formState.isDirty || !sameSet(assetIds, initialAssetIds)}
        width={DIALOG_WIDTH}
      >
        <FormTextInput control={control} name="title" label={t('form.changeTitle')} autoComplete="off" />
        <FormTextArea control={control} name="justification" label={t('form.justification')} />
        <FormTextInput control={control} name="affectedService" label={t('form.affectedService')} autoComplete="off" />
        {change === undefined && (
          <>
            <FormSelector
              control={control}
              name="ownerTeamId"
              label={t('form.team')}
              placeholder={t('form.select')}
              options={(teams.data ?? []).map((team) => ({ value: team.id, label: team.name }))}
              onValueChange={(next) => {
                const members = teams.data?.find((team) => team.id === next)?.members ?? [];
                setValue('ownerId', members.some((m) => m.id === me.id) ? me.id : '');
              }}
            />
            <FormSelector control={control} name="ownerId" label={t('form.owner')} placeholder={t('form.select')} options={memberOptions} />
          </>
        )}
        <FormSelector control={control} name="risk" label={t('form.risk')} options={levelOptions} />
        <FormSelector control={control} name="impact" label={t('form.impact')} options={levelOptions} />
        <FormDateTimeInput control={control} name="windowStart" label={t('form.windowStart')} />
        <FormDateTimeInput control={control} name="windowEnd" label={t('form.windowEnd')} />
        <FormTextArea control={control} name="implementationPlan" label={t('form.implementationPlan')} />
        <FormTextArea control={control} name="verificationPlan" label={t('form.verificationPlan')} />
        <FormTextArea control={control} name="backoutPlan" label={t('form.backoutPlan')} />
        <AssetPicker picked={picked} onChange={setPicked} />
      </FormDialog>
      {pendingDraft !== null && (
        <ConfirmDialog
          title={t('invalidate.title')}
          message={t('invalidate.message')}
          confirmLabel={t('invalidate.confirm')}
          isPending={edit.isPending}
          error={edit.error}
          onConfirm={() => edit.mutate({ draft: pendingDraft.draft, assetIds }, { onSuccess: onSaved })}
          onCancel={() => setPendingDraft(null)}
        />
      )}
    </>
  );
}
