import { zodResolver } from '@hookform/resolvers/zod';
import { useId } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { Button } from '../../../shared/ui/Button';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { SelectField } from '../../../shared/ui/SelectField';
import { TextField } from '../../../shared/ui/TextField';
import { useDepartmentOptions, useLocationOptions, withCurrentOption } from '../shared/referenceOptions';

import { useCreateUser, useUpdateUser, type UserListItem } from './api';
import { toFormValues, toRequestFields, USER_ROLES, userFormSchema, type UserFormValues } from './userSchema';

const FORM_FIELDS = ['firstName', 'lastName', 'email', 'role', 'departmentId', 'locationId'] as const;
const CODE_FIELDS = { [ERROR_CODES.emailTaken]: 'email' } as const;

export interface UserFormDialogProps {
  user: UserListItem | null;
  onClose: () => void;
  onCreated: (temporaryPassword: string) => void;
}

export function UserFormDialog({ user, onClose, onCreated }: UserFormDialogProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const departmentOptions = withCurrentOption(useDepartmentOptions(), user?.departmentId, user?.departmentName);
  const locationOptions = withCurrentOption(useLocationOptions(), user?.locationId, user?.locationName);
  const create = useCreateUser();
  const update = useUpdateUser();
  const mutation = user === null ? create : update;
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<UserFormValues>({ resolver: zodResolver(userFormSchema), defaultValues: toFormValues(user) });

  // Closing while a create is in flight would unmount the dialog and lose the one-time temporary password.
  const close = () => {
    if (!mutation.isPending) {
      onClose();
    }
  };

  const onSubmit = handleSubmit((values) => {
    const fields = toRequestFields(values);
    const onError = (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS);
    if (user === null) {
      create.mutate(fields, { onSuccess: (result) => onCreated(result.temporaryPassword), onError });
    } else {
      update.mutate({ id: user.id, version: user.version, ...fields }, { onSuccess: onClose, onError });
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;
  const roleOptions = USER_ROLES.map((role) => ({ value: role, label: tc(`roles.${role}`) }));

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          close();
        }
      }}
      title={user === null ? t('users.new') : t('users.edit')}
      footer={
        <>
          <Button variant="secondary" disabled={mutation.isPending} onClick={close}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={mutation.isPending}>
            {user === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="flex flex-col gap-4" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label={t('users.fields.firstName')} autoComplete="off" error={fieldError(errors.firstName)} {...register('firstName')} />
          <TextField label={t('users.fields.lastName')} autoComplete="off" error={fieldError(errors.lastName)} {...register('lastName')} />
        </div>
        <TextField label={t('users.fields.email')} type="email" autoComplete="off" error={fieldError(errors.email)} {...register('email')} />
        <SelectField label={t('users.fields.role')} options={roleOptions} error={fieldError(errors.role)} {...register('role')} />
        <SelectField
          label={t('users.fields.department')}
          options={departmentOptions}
          placeholder={tc('none')}
          error={fieldError(errors.departmentId)}
          {...register('departmentId')}
        />
        <SelectField label={t('users.fields.location')} options={locationOptions} placeholder={tc('none')} error={fieldError(errors.locationId)} {...register('locationId')} />
      </form>
    </Dialog>
  );
}
