/**
 * @input An optional ticket number the new task belongs to, a created callback and a close callback.
 * @output A dialog that opens a task for any team, with an optional assignee, priority and due date.
 * @position Task list page and the Görevler section of a ticket.
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { FormDateInput, FormSelector, FormTextArea, FormTextInput } from '../../shared/form/formFields';
import { PRIORITIES, useReferenceTeams, type Priority } from '../tickets/api';

import { useCreateTask, type TaskDetail } from './api';

/** Mirror backend WorkTask.TitleMaxLength / DescriptionMaxLength. */
const TITLE_MAX_LENGTH = 200;
const DESCRIPTION_MAX_LENGTH = 4000;
const DEFAULT_PRIORITY: Priority = 'p3';

const schema = z.object({
  title: z.string().trim().min(1, ERROR_CODES.required).max(TITLE_MAX_LENGTH, ERROR_CODES.tooLong),
  description: z.string().trim().max(DESCRIPTION_MAX_LENGTH, ERROR_CODES.tooLong),
  teamId: z.string().min(1, ERROR_CODES.required),
  assigneeId: z.string(),
  priority: z.string().min(1, ERROR_CODES.required),
  dueDate: z.string(),
});
type Values = z.infer<typeof schema>;

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());

export function TaskFormDialog({ ticketNumber = null, onCreated, onClose }: { ticketNumber?: string | null; onCreated: (task: TaskDetail) => void; onClose: () => void }) {
  const { t } = useTranslation('tasks');
  const { t: tt } = useTranslation('tickets');
  const teams = useReferenceTeams(true);
  const create = useCreateTask();
  const { control, setValue, handleSubmit, formState } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { title: '', description: '', teamId: '', assigneeId: '', priority: DEFAULT_PRIORITY, dueDate: '' },
  });
  const teamId = useWatch({ control, name: 'teamId' });
  const memberOptions = useMemo(
    () => (teams.data?.find((team) => team.id === teamId)?.members ?? []).map((m) => ({ value: m.id, label: m.fullName })),
    [teams.data, teamId],
  );
  const onSubmit = handleSubmit((v) =>
    create.mutate(
      {
        title: v.title.trim(),
        description: orNull(v.description),
        teamId: v.teamId,
        assigneeId: orNull(v.assigneeId),
        priority: v.priority as Priority,
        dueDate: orNull(v.dueDate),
        ticketNumber,
      },
      { onSuccess: onCreated },
    ),
  );
  return (
    <FormDialog
      title={ticketNumber ? `${t('form.title')} · ${ticketNumber}` : t('form.title')}
      submitLabel={t('form.submit')}
      isPending={create.isPending}
      alert={create.error ? describeError(create.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
      isDirty={formState.isDirty}
    >
      <FormTextInput control={control} name="title" label={t('form.taskTitle')} autoComplete="off" />
      <FormTextArea control={control} name="description" label={t('form.description')} />
      <FormSelector
        control={control}
        name="teamId"
        label={t('form.team')}
        placeholder={t('form.select')}
        options={(teams.data ?? []).map((team) => ({ value: team.id, label: team.name }))}
        onValueChange={() => setValue('assigneeId', '')}
      />
      <FormSelector control={control} name="assigneeId" label={t('form.assignee')} placeholder={t('form.anyone')} options={memberOptions} isClearable />
      <FormSelector control={control} name="priority" label={t('form.priority')} options={PRIORITIES.map((value) => ({ value, label: tt(`priorities.${value}`) }))} />
      <FormDateInput control={control} name="dueDate" label={t('form.dueDate')} />
    </FormDialog>
  );
}
