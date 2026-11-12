/**
 * @input A ticket, the action kind and a close callback.
 * @output A dialog with the small form that action needs; it sends the version the person saw.
 * @position Opened from the ticket work actions.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { FormSelector, FormTextArea, FormTextInput } from '../../shared/form/formFields';

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
const DIALOG_WIDTH = 520;

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
  const { control, setValue, handleSubmit } = useForm<Values>({
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

  const close = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };
  const select = (name: 'choice' | 'second' | 'third', label: string, options: { value: string; label: string }[], placeholder?: string, onValueChange?: (value: string) => void) => (
    <FormSelector control={control} name={name} label={label} options={options} placeholder={placeholder} onValueChange={onValueChange} />
  );
  const text = (name: 'text' | 'note', label: string) => <FormTextInput control={control} name={name} label={label} autoComplete="off" />;

  return (
    <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH} purpose="form">
      <Layout
        header={<DialogHeader title={`${t(`actions.${kind}`)} · ${ticket.number}`} onOpenChange={close} />}
        content={
          <LayoutContent>
            <form id={formId} noValidate onSubmit={(event) => void onSubmit(event)}>
              <VStack gap={4}>
                <FormAlert message={action.error ? describeError(action.error) : null} />
                {kind === 'hold' && (
                  <>
                    {select('choice', t('fields.holdReason'), HOLD_REASONS.map((value) => ({ value, label: t(`holdReasons.${value}`) })), t('fields.select'))}
                    {text('note', t('fields.note'))}
                  </>
                )}
                {kind === 'complete' && (
                  <>
                    {ticket.openTaskCount > 0 && <Banner status="warning" title={t('detail.openTasksWarning', { count: ticket.openTaskCount })} />}
                    <FormTextArea control={control} name="text" label={t('fields.resolution')} />
                    {ticket.kind === 'incident' && select('choice', t('fields.outcome'), INCIDENT_OUTCOMES.map((value) => ({ value, label: t(`outcomes.${value}`) })), t('fields.select'))}
                    {text('note', t('fields.rootCause'))}
                  </>
                )}
                {(kind === 'cancel' || kind === 'invalidate') && text('text', t('fields.reason'))}
                {kind === 'priority' && (
                  <>
                    {select('choice', t('fields.impact'), levelOptions)}
                    {select('second', t('fields.urgency'), levelOptions)}
                    {text('text', t('fields.reason'))}
                  </>
                )}
                {kind === 'transfer' && (
                  <>
                    {select('choice', t('fields.team'), (teams.data ?? []).map((team) => ({ value: team.id, label: team.name })), t('fields.select'), () => setValue('second', ''))}
                    {select('second', t('fields.assignee'), memberOptions, t('fields.anyone'))}
                    {select('third', t('fields.transferReason'), TRANSFER_REASONS.map((value) => ({ value, label: t(`transferReasons.${value}`) })), t('fields.select'))}
                    {text('note', t('fields.note'))}
                  </>
                )}
              </VStack>
            </form>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack gap={2} hAlign="end">
              <Button label={tc('actions.cancel')} variant="secondary" onClick={onClose} />
              <Button type="submit" form={formId} label={tc('actions.confirm')} variant={kind === 'cancel' ? 'destructive' : 'primary'} isLoading={action.isPending} />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
  );
}
