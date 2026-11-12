/**
 * @input The user to edit, or null to create one, and the close and created callbacks.
 * @output A form dialog with name, e-mail, role, department, location and, on create, the credential delivery.
 * @position Opened from the user list.
 */
import { Grid } from '@astryxdesign/core/Grid';
import { Text } from '@astryxdesign/core/Text';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { FormSelector, FormTextInput } from '../../../shared/form/formFields';
import { useLoginMethod } from '../../auth/api';
import { useDepartmentOptions, useLocationOptions, withCurrentOption } from '../shared/referenceOptions';

import { DEFAULT_DELIVERY, useCreateUser, useUpdateUser, type CredentialDelivery, type UserListItem } from './api';
import { DeliveryChoice } from './DeliveryChoice';
import { toFormValues, toRequestFields, USER_ROLES, userFormSchema, type UserFormValues } from './userSchema';

const FORM_FIELDS = ['firstName', 'lastName', 'email', 'role', 'departmentId', 'locationId'] as const;
const CODE_FIELDS = { [ERROR_CODES.emailTaken]: 'email' } as const;
const NAME_COLUMNS = { minWidth: 200 } as const;

export interface UserFormDialogProps {
  user: UserListItem | null;
  onClose: () => void;
  /** `temporaryPassword` is null when a set-password link was emailed instead. */
  onCreated: (result: { email: string; temporaryPassword: string | null }) => void;
}

export function UserFormDialog({ user, onClose, onCreated }: UserFormDialogProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const departmentOptions = withCurrentOption(useDepartmentOptions(), user?.departmentId, user?.departmentName);
  const locationOptions = withCurrentOption(useLocationOptions(), user?.locationId, user?.locationName);
  const [delivery, setDelivery] = useState<CredentialDelivery>(DEFAULT_DELIVERY);
  // Under a company-account method the server sets no password and sends no email (ADR-0030).
  const viaDirectory = useLoginMethod().method !== 'local';
  const create = useCreateUser();
  const update = useUpdateUser();
  const mutation = user === null ? create : update;
  const {
    control,
    handleSubmit,
    setError,
    formState: { isDirty },
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
      create.mutate(
        { ...fields, delivery },
        { onSuccess: (result) => (viaDirectory ? onClose() : onCreated({ email: fields.email, temporaryPassword: result.temporaryPassword })), onError },
      );
    } else {
      update.mutate({ id: user.id, version: user.version, ...fields }, { onSuccess: onClose, onError });
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;
  const roleOptions = USER_ROLES.map((role) => ({ value: role, label: tc(`roles.${role}`) }));

  return (
    <FormDialog
      isDirty={isDirty}
      title={user === null ? t('users.new') : t('users.edit')}
      submitLabel={user === null ? tc('actions.create') : tc('actions.save')}
      isPending={mutation.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={close}
    >
      <Grid columns={NAME_COLUMNS} gap={4}>
        <FormTextInput control={control} name="firstName" label={t('users.fields.firstName')} autoComplete="off" />
        <FormTextInput control={control} name="lastName" label={t('users.fields.lastName')} autoComplete="off" />
      </Grid>
      <FormTextInput control={control} name="email" label={t('users.fields.email')} type="email" autoComplete="off" />
      <FormSelector control={control} name="role" label={t('users.fields.role')} options={roleOptions} />
      <FormSelector control={control} name="departmentId" label={t('users.fields.department')} options={departmentOptions} placeholder={tc('none')} isClearable />
      <FormSelector control={control} name="locationId" label={t('users.fields.location')} options={locationOptions} placeholder={tc('none')} isClearable />
      {user === null && (viaDirectory ? <Text type="supporting">{t('users.directoryNote')}</Text> : <DeliveryChoice value={delivery} onChange={setDelivery} />)}
    </FormDialog>
  );
}
