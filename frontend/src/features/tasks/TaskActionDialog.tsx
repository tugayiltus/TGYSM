/**
 * @input A task, the action kind and a close callback.
 * @output A dialog with the small form that action needs; it sends the version the person saw.
 * @position Opened from the task work actions.
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { FormSelector, FormTextArea, FormTextInput } from '../../shared/form/formFields';
import { HOLD_REASONS, TRANSFER_REASONS, useReferenceTeams, type HoldReason, type TransferReason } from '../tickets/api';

import { useTaskAction, type TaskAction, type TaskDetail } from './api';

/** Kinds that need input; claim, start, resume and reopen run directly. */
export type TaskDialogKind = 'hold' | 'complete' | 'cancel' | 'assign' | 'transfer';

/** Mirror backend WorkTask.ReasonMaxLength / NoteMaxLength. */
const REASON_MAX_LENGTH = 500;
const NOTE_MAX_LENGTH = 2000;
const OTHER = 'other';

const required = (max: number) => z.string().trim().min(1, ERROR_CODES.required).max(max, ERROR_CODES.tooLong);
const optional = (max: number) => z.string().trim().max(max, ERROR_CODES.tooLong);
const base = z.object({ choice: z.string(), second: z.string(), third: z.string(), text: optional(NOTE_MAX_LENGTH), note: optional(REASON_MAX_LENGTH) });

/** One small form per action: "choice"/"second"/"third" are selects, "text" the main text, "note" an optional note. */
const SCHEMAS: Record<TaskDialogKind, typeof base> = {
  hold: base
    .extend({ choice: z.string().min(1, ERROR_CODES.required) })
    .refine((v) => v.choice !== OTHER || v.note.trim() !== '', { path: ['note'], message: ERROR_CODES.required }),
  complete: base.extend({ text: required(NOTE_MAX_LENGTH) }),
  cancel: base.extend({ text: required(REASON_MAX_LENGTH) }),
  assign: base,
  transfer: base
    .extend({ choice: z.string().min(1, ERROR_CODES.required), third: z.string().min(1, ERROR_CODES.required) })
    .refine((v) => v.third !== OTHER || v.note.trim() !== '', { path: ['note'], message: ERROR_CODES.required }),
};
type Values = z.infer<typeof base>;

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());

function toAction(kind: TaskDialogKind, v: Values): TaskAction {
  switch (kind) {
    case 'hold':
      return { kind, reason: v.choice as HoldReason, note: orNull(v.note) };
    case 'complete':
      return { kind, resultNote: v.text.trim() };
    case 'cancel':
      return { kind, reason: v.text.trim() };
    case 'assign':
      return { kind, assigneeId: orNull(v.second) };
    case 'transfer':
      return { kind, teamId: v.choice, assigneeId: orNull(v.second), reason: v.third as TransferReason, note: orNull(v.note) };
  }
}

export function TaskActionDialog({ task, kind, onClose }: { task: TaskDetail; kind: TaskDialogKind; onClose: () => void }) {
  const { t } = useTranslation('tasks');
  const { t: tt } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  // The version the user saw when opening the dialog; a background refetch must not silently skip the conflict check.
  const [openedVersion] = useState(task.version);
  const action = useTaskAction({ ...task, version: openedVersion });
  const teams = useReferenceTeams(kind === 'transfer' || kind === 'assign');
  const { control, setValue, handleSubmit } = useForm<Values>({
    resolver: zodResolver(SCHEMAS[kind]),
    defaultValues: { choice: '', second: kind === 'assign' ? (task.assigneeId ?? '') : '', third: '', text: '', note: '' },
  });
  const chosenTeam = useWatch({ control, name: 'choice' });
  const teamId = kind === 'assign' ? task.teamId : chosenTeam;
  const memberOptions = useMemo(
    () => (teams.data?.find((team) => team.id === teamId)?.members ?? []).map((m) => ({ value: m.id, label: m.fullName })),
    [teams.data, teamId],
  );
  const onSubmit = handleSubmit((values) => action.mutate(toAction(kind, values), { onSuccess: onClose }));
  const select = (name: 'choice' | 'second' | 'third', label: string, options: { value: string; label: string }[], placeholder?: string, onValueChange?: (value: string) => void) => (
    <FormSelector control={control} name={name} label={label} options={options} placeholder={placeholder} onValueChange={onValueChange} />
  );
  return (
    <FormDialog
      title={`${t(`actions.${kind}`)} · ${task.number}`}
      submitLabel={tc('actions.confirm')}
      isPending={action.isPending}
      alert={action.error ? describeError(action.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      {kind === 'hold' && (
        <>
          {select('choice', t('fields.holdReason'), HOLD_REASONS.map((value) => ({ value, label: tt(`holdReasons.${value}`) })), t('fields.select'))}
          <FormTextInput control={control} name="note" label={t('fields.note')} autoComplete="off" />
        </>
      )}
      {kind === 'complete' && <FormTextArea control={control} name="text" label={t('fields.resultNote')} />}
      {kind === 'cancel' && <FormTextInput control={control} name="text" label={t('fields.reason')} autoComplete="off" />}
      {kind === 'assign' && select('second', t('fields.assignee'), memberOptions, t('fields.anyone'))}
      {kind === 'transfer' && (
        <>
          {select('choice', t('fields.team'), (teams.data ?? []).filter((team) => team.id !== task.teamId).map((team) => ({ value: team.id, label: team.name })), t('fields.select'), () => setValue('second', ''))}
          {select('second', t('fields.assignee'), memberOptions, t('fields.anyone'))}
          {select('third', t('fields.transferReason'), TRANSFER_REASONS.map((value) => ({ value, label: tt(`transferReasons.${value}`) })), t('fields.select'))}
          <FormTextInput control={control} name="note" label={t('fields.note')} autoComplete="off" />
        </>
      )}
    </FormDialog>
  );
}
