/**
 * @input The team to edit, or null to create one, and a close callback.
 * @output A form dialog with the name and, when editing, the default intake switch.
 * @position Opened from the team list.
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { FormSwitch, FormTextInput } from '../../../shared/form/formFields';

import { useCreateTeam, useUpdateTeam, type TeamItem } from './api';

/** Mirrors backend Team.NameMaxLength. */
const TEAM_NAME_MAX_LENGTH = 150;

const teamSchema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(TEAM_NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  isDefaultIntake: z.boolean(),
});
type TeamFormValues = z.infer<typeof teamSchema>;

const FORM_FIELDS = ['name'] as const;
const CODE_FIELDS = { [ERROR_CODES.teamNameTaken]: 'name' } as const;

export function TeamFormDialog({ team, onClose }: { team: TeamItem | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const create = useCreateTeam();
  const update = useUpdateTeam();
  const mutation = team === null ? create : update;
  const {
    control,
    handleSubmit,
    setError,
    formState: { isDirty },
  } = useForm<TeamFormValues>({
    resolver: zodResolver(teamSchema),
    defaultValues: { name: team?.name ?? '', isDefaultIntake: team?.isDefaultIntake ?? false },
  });

  const onSubmit = handleSubmit(({ name, isDefaultIntake }) => {
    const callbacks = { onSuccess: onClose, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (team === null) {
      create.mutate(name, callbacks);
    } else {
      update.mutate({ id: team.id, version: team.version, name, isDefaultIntake }, callbacks);
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;

  return (
    <FormDialog
      isDirty={isDirty}
      title={team === null ? t('teams.new') : t('teams.edit')}
      submitLabel={team === null ? tc('actions.create') : tc('actions.save')}
      isPending={mutation.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="name" label={t('teams.fields.name')} autoComplete="off" />
      {team !== null && <FormSwitch control={control} name="isDefaultIntake" label={t('teams.fields.isDefaultIntake')} description={t('teams.intakeHint')} />}
    </FormDialog>
  );
}
