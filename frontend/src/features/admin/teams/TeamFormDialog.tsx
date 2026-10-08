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
import { TextField } from '../../../shared/ui/TextField';

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
  const formId = useId();
  const create = useCreateTeam();
  const update = useUpdateTeam();
  const mutation = team === null ? create : update;
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
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
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={team === null ? t('teams.new') : t('teams.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={mutation.isPending}>
            {team === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="d-flex flex-column gap-3" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('teams.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
        {team !== null && (
          <label className="d-flex tgysm-touch align-items-center gap-2 small">
            <input type="checkbox" className="form-check-input" {...register('isDefaultIntake')} />
            <span>
              {t('teams.fields.isDefaultIntake')}
              <span className="d-block small text-body-secondary">{t('teams.intakeHint')}</span>
            </span>
          </label>
        )}
      </form>
    </Dialog>
  );
}
