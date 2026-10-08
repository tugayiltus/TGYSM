import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError, fieldError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { Button } from '../../shared/ui/Button';
import { Dialog } from '../../shared/ui/Dialog';
import { FormAlert } from '../../shared/ui/FormAlert';
import { SelectField } from '../../shared/ui/SelectField';
import { TextAreaField } from '../../shared/ui/TextAreaField';
import { TextField } from '../../shared/ui/TextField';

import {
  HOLD_REASONS,
  INCIDENT_OUTCOMES,
  LEVELS,
  TRANSFER_REASONS,
  useReferenceTeams,
  useTicketAction,
  type HoldReason,
  type Level,
  type TicketAction,
  type TicketDetail,
  type TicketOutcome,
  type TransferReason,
} from './api';

/** Kinds that need input; claim, start and resume run directly. */
export type DialogActionKind = 'hold' | 'complete' | 'cancel' | 'priority' | 'transfer' | 'invalidate';

/** Mirror backend Ticket.ReasonMaxLength / TextMaxLength. */
const REASON_MAX_LENGTH = 500;
const TEXT_MAX_LENGTH = 2000;
const OTHER = 'other';

const required = (max: number) => z.string().trim().min(1, ERROR_CODES.required).max(max, ERROR_CODES.tooLong);
const optional = (max: number) => z.string().trim().max(max, ERROR_CODES.tooLong);

const base = z.object({
  choice: z.string(),
  second: z.string(),
  third: z.string(),
  text: optional(TEXT_MAX_LENGTH),
  note: optional(REASON_MAX_LENGTH),
});

/** One small form per action: "choice"/"second"/"third" are selects, "text" the main text, "note" an optional note. */
const SCHEMAS: Record<DialogActionKind, typeof base> = {
  hold: base
    .extend({ choice: z.string().min(1, ERROR_CODES.required) })
    .refine((v) => v.choice !== OTHER || v.note.trim() !== '', { path: ['note'], message: ERROR_CODES.required }),
  complete: base.extend({ text: required(TEXT_MAX_LENGTH) }),
  cancel: base.extend({ text: required(REASON_MAX_LENGTH) }),
  invalidate: base.extend({ text: required(REASON_MAX_LENGTH) }),
  priority: base.extend({ choice: z.string().min(1, ERROR_CODES.required), second: z.string().min(1, ERROR_CODES.required), text: required(REASON_MAX_LENGTH) }),
  transfer: base
    .extend({ choice: z.string().min(1, ERROR_CODES.required), third: z.string().min(1, ERROR_CODES.required) })
    .refine((v) => v.third !== OTHER || v.note.trim() !== '', { path: ['note'], message: ERROR_CODES.required }),
};

type Values = z.infer<typeof base>;

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());

function toAction(kind: DialogActionKind, ticket: TicketDetail, v: Values): TicketAction {
  switch (kind) {
    case 'hold':
      return { kind, reason: v.choice as HoldReason, note: orNull(v.note) };
    case 'complete':
      return { kind, resolution: v.text.trim(), outcome: ticket.kind === 'incident' && v.choice !== '' ? (v.choice as TicketOutcome) : null, rootCause: orNull(v.note) };
    case 'cancel':
    case 'invalidate':
      return { kind, reason: v.text.trim() };
    case 'priority':
      return { kind, impact: v.choice as Level, urgency: v.second as Level, reason: v.text.trim() };
    case 'transfer':
      return { kind, teamId: v.choice, assigneeId: orNull(v.second), reason: v.third as TransferReason, note: orNull(v.note) };
  }
}

export function TicketActionDialog({ ticket, kind, onClose }: { ticket: TicketDetail; kind: DialogActionKind; onClose: () => void }) {
  const { t } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const formId = useId();
  // The version the user saw when opening the dialog; a background refetch must not silently skip the conflict check.
  const [openedVersion] = useState(ticket.version);
  const action = useTicketAction({ ...ticket, version: openedVersion });
  const teams = useReferenceTeams(kind === 'transfer');
  const {
    register,
    control,
    setValue,
    handleSubmit,
    formState: { errors },
  } = useForm<Values>({
    resolver: zodResolver(SCHEMAS[kind]),
    defaultValues: {
      choice: kind === 'priority' ? ticket.impact : '',
      second: kind === 'priority' ? ticket.urgency : '',
      third: '',
      text: '',
      note: '',
    },
  });
  const teamId = useWatch({ control, name: 'choice' });
  const memberOptions = useMemo(
    () => (teams.data?.find((team) => team.id === teamId)?.members ?? []).map((m) => ({ value: m.id, label: m.fullName })),
    [teams.data, teamId],
  );
  const onSubmit = handleSubmit((values) => action.mutate(toAction(kind, ticket, values), { onSuccess: onClose }));
  const levelOptions = LEVELS.map((value) => ({ value, label: t(`levels.${value}`) }));

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={`${t(`actions.${kind}`)} · ${ticket.number}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} variant={kind === 'cancel' ? 'danger' : 'primary'} isLoading={action.isPending}>
            {tc('actions.confirm')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="d-flex flex-column gap-3" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={action.error ? describeError(action.error) : null} />
        {kind === 'hold' && (
          <>
            <SelectField label={t('fields.holdReason')} placeholder={t('fields.select')} options={HOLD_REASONS.map((value) => ({ value, label: t(`holdReasons.${value}`) }))} error={fieldError(errors.choice)} {...register('choice')} />
            <TextField label={t('fields.note')} autoComplete="off" error={fieldError(errors.note)} {...register('note')} />
          </>
        )}
        {kind === 'complete' && (
          <>
            <TextAreaField label={t('fields.resolution')} error={fieldError(errors.text)} {...register('text')} />
            {ticket.kind === 'incident' && (
              <SelectField label={t('fields.outcome')} placeholder={t('fields.select')} options={INCIDENT_OUTCOMES.map((value) => ({ value, label: t(`outcomes.${value}`) }))} error={fieldError(errors.choice)} {...register('choice')} />
            )}
            <TextField label={t('fields.rootCause')} autoComplete="off" error={fieldError(errors.note)} {...register('note')} />
          </>
        )}
        {(kind === 'cancel' || kind === 'invalidate') && <TextField label={t('fields.reason')} autoComplete="off" error={fieldError(errors.text)} {...register('text')} />}
        {kind === 'priority' && (
          <>
            <SelectField label={t('fields.impact')} options={levelOptions} error={fieldError(errors.choice)} {...register('choice')} />
            <SelectField label={t('fields.urgency')} options={levelOptions} error={fieldError(errors.second)} {...register('second')} />
            <TextField label={t('fields.reason')} autoComplete="off" error={fieldError(errors.text)} {...register('text')} />
          </>
        )}
        {kind === 'transfer' && (
          <>
            <SelectField label={t('fields.team')} placeholder={t('fields.select')} options={(teams.data ?? []).map((team) => ({ value: team.id, label: team.name }))} error={fieldError(errors.choice)} {...register('choice', { onChange: () => setValue('second', '') })} />
            <SelectField label={t('fields.assignee')} placeholder={t('fields.anyone')} options={memberOptions} error={fieldError(errors.second)} {...register('second')} />
            <SelectField label={t('fields.transferReason')} placeholder={t('fields.select')} options={TRANSFER_REASONS.map((value) => ({ value, label: t(`transferReasons.${value}`) }))} error={fieldError(errors.third)} {...register('third')} />
            <TextField label={t('fields.note')} autoComplete="off" error={fieldError(errors.note)} {...register('note')} />
          </>
        )}
      </form>
    </Dialog>
  );
}
