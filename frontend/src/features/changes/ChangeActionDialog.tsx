/**
 * @input A change, the action kind (close or cancel) and a close callback.
 * @output A dialog with the form that action needs; it sends the version the person saw.
 * @position Opened from the change work actions.
 */
import { Button } from '@astryxdesign/core/Button';
import { Text } from '@astryxdesign/core/Text';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { FormDateTimeInput, FormSelector, FormTextArea, FormTextInput } from '../../shared/form/formFields';
import { ROUTES } from '../../shared/routes';

import { CHANGE_OUTCOMES, useChangeAction, type ChangeAction, type ChangeDetail, type ChangeOutcome } from './api';
import { fromLocalInput, nowLocalInput } from './dateTime';

export type ChangeDialogKind = 'close' | 'cancel';

/** Mirror backend ChangeRequest.TextMaxLength / ReasonMaxLength. */
const TEXT_MAX_LENGTH = 4000;
const REASON_MAX_LENGTH = 500;
const SUCCESSFUL: ChangeOutcome = 'successful';
const FAILED: ChangeOutcome = 'failed';

const text = z.string().trim().max(TEXT_MAX_LENGTH, ERROR_CODES.tooLong);
const closeSchema = z
  .object({ outcome: z.string().min(1, ERROR_CODES.required), verificationNote: text, backoutNote: text, closingNote: text, actualEnd: z.string() })
  .superRefine((v, ctx) => {
    const missing = (path: string) => ctx.addIssue({ code: 'custom', path: [path], message: ERROR_CODES.required });
    if (v.outcome === SUCCESSFUL) {
      if (v.verificationNote.trim() === '') {
        missing('verificationNote');
      }

      if (v.actualEnd === '') {
        missing('actualEnd');
      }
    } else if (v.outcome !== '' && v.backoutNote.trim() === '') {
      missing('backoutNote');
    }
  });
const cancelSchema = z.object({ reason: z.string().trim().min(1, ERROR_CODES.required).max(REASON_MAX_LENGTH, ERROR_CODES.tooLong) });

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());

/** A failed change gets a new incident; nothing is created automatically. */
export function OpenIncidentButton() {
  const { t } = useTranslation('changes');
  return <Button label={t('actions.openIncident')} variant="secondary" href={ROUTES.newIncident} />;
}

function CloseForm({ change, onClose }: { change: ChangeDetail; onClose: () => void }) {
  const { t } = useTranslation('changes');
  const { t: tc } = useTranslation();
  // The version the user saw when opening the dialog; a background refetch must not silently skip the conflict check.
  const [openedVersion] = useState(change.version);
  const action = useChangeAction({ ...change, version: openedVersion });
  const { control, handleSubmit } = useForm<z.infer<typeof closeSchema>>({
    resolver: zodResolver(closeSchema),
    defaultValues: { outcome: '', verificationNote: '', backoutNote: '', closingNote: '', actualEnd: nowLocalInput() },
  });
  const outcome = useWatch({ control, name: 'outcome' });
  const onSubmit = handleSubmit((v) => {
    const successful = v.outcome === SUCCESSFUL;
    const body: ChangeAction = {
      kind: 'close',
      outcome: v.outcome as ChangeOutcome,
      verificationNote: successful ? orNull(v.verificationNote) : null,
      backoutNote: successful ? null : orNull(v.backoutNote),
      closingNote: orNull(v.closingNote),
      actualEnd: fromLocalInput(v.actualEnd),
    };
    action.mutate(body, { onSuccess: onClose });
  });
  return (
    <FormDialog
      title={`${t('actions.close')} · ${change.number}`}
      submitLabel={tc('actions.confirm')}
      isPending={action.isPending}
      alert={action.error ? describeError(action.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormSelector
        control={control}
        name="outcome"
        label={t('fields.outcome')}
        placeholder={t('fields.select')}
        options={CHANGE_OUTCOMES.map((value) => ({ value, label: t(`outcomes.${value}`) }))}
      />
      {outcome === SUCCESSFUL && <FormTextArea control={control} name="verificationNote" label={t('fields.verificationNote')} />}
      {outcome !== '' && outcome !== SUCCESSFUL && <FormTextArea control={control} name="backoutNote" label={t('fields.backoutNote')} />}
      <FormDateTimeInput control={control} name="actualEnd" label={t('fields.actualEnd')} />
      <FormTextArea control={control} name="closingNote" label={t('fields.closingNote')} />
      {outcome === FAILED && (
        <>
          <Text type="supporting">{t('fields.failedHint')}</Text>
          <OpenIncidentButton />
        </>
      )}
    </FormDialog>
  );
}

function CancelForm({ change, onClose }: { change: ChangeDetail; onClose: () => void }) {
  const { t } = useTranslation('changes');
  const { t: tc } = useTranslation();
  const [openedVersion] = useState(change.version);
  const action = useChangeAction({ ...change, version: openedVersion });
  const { control, handleSubmit } = useForm<z.infer<typeof cancelSchema>>({ resolver: zodResolver(cancelSchema), defaultValues: { reason: '' } });
  const onSubmit = handleSubmit((v) => action.mutate({ kind: 'cancel', reason: v.reason.trim() }, { onSuccess: onClose }));
  return (
    <FormDialog
      title={`${t('actions.cancel')} · ${change.number}`}
      submitLabel={tc('actions.confirm')}
      isPending={action.isPending}
      alert={action.error ? describeError(action.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="reason" label={t('fields.reason')} autoComplete="off" />
    </FormDialog>
  );
}

export function ChangeActionDialog({ change, kind, onClose }: { change: ChangeDetail; kind: ChangeDialogKind; onClose: () => void }) {
  return kind === 'close' ? <CloseForm change={change} onClose={onClose} /> : <CancelForm change={change} onClose={onClose} />;
}
