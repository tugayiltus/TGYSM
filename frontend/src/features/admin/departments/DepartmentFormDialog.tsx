/**
 * @input The department to edit, or null to create one, and a close callback.
 * @output A form dialog with the name and the approving admin.
 * @position Opened from the department list.
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { FormSelector, FormTextInput } from '../../../shared/form/formFields';
import { useAdminOptions, withCurrentOption } from '../shared/referenceOptions';

import { useCreateDepartment, useUpdateDepartment, type DepartmentItem } from './api';

/** Mirrors backend Department.NameMaxLength. */
const DEPARTMENT_NAME_MAX_LENGTH = 150;
const NO_SELECTION = '';

const departmentSchema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(DEPARTMENT_NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  approverUserId: z.string(),
});

type DepartmentFormValues = z.infer<typeof departmentSchema>;

const FORM_FIELDS = ['name', 'approverUserId'] as const;
const CODE_FIELDS = { [ERROR_CODES.departmentNameTaken]: 'name', [ERROR_CODES.approverInvalid]: 'approverUserId' } as const;

export function DepartmentFormDialog({ department, onClose }: { department: DepartmentItem | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const approverOptions = withCurrentOption(useAdminOptions(), department?.approverUserId, department?.approverName);
  const create = useCreateDepartment();
  const update = useUpdateDepartment();
  const mutation = department === null ? create : update;
  const {
    control,
    handleSubmit,
    setError,
    formState: { isDirty },
  } = useForm<DepartmentFormValues>({
    resolver: zodResolver(departmentSchema),
    defaultValues: { name: department?.name ?? '', approverUserId: department?.approverUserId ?? NO_SELECTION },
  });

  const onSubmit = handleSubmit(({ name, approverUserId }) => {
    const body = { name, approverUserId: approverUserId === NO_SELECTION ? null : approverUserId };
    const callbacks = { onSuccess: onClose, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (department === null) {
      create.mutate(body, callbacks);
    } else {
      update.mutate({ id: department.id, version: department.version, ...body }, callbacks);
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;

  return (
    <FormDialog
      isDirty={isDirty}
      title={department === null ? t('departments.new') : t('departments.edit')}
      submitLabel={department === null ? tc('actions.create') : tc('actions.save')}
      isPending={mutation.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="name" label={t('departments.fields.name')} autoComplete="off" />
      <FormSelector control={control} name="approverUserId" label={t('departments.fields.approver')} options={approverOptions} placeholder={tc('none')} isClearable />
    </FormDialog>
  );
}
