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
import { SelectField } from '../../../shared/ui/SelectField';
import { TextField } from '../../../shared/ui/TextField';
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
  const formId = useId();
  const approverOptions = withCurrentOption(useAdminOptions(), department?.approverUserId, department?.approverName);
  const create = useCreateDepartment();
  const update = useUpdateDepartment();
  const mutation = department === null ? create : update;
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
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
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={department === null ? t('departments.new') : t('departments.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={mutation.isPending}>
            {department === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="d-flex flex-column gap-3" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('departments.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
        <SelectField
          label={t('departments.fields.approver')}
          options={approverOptions}
          placeholder={tc('none')}
          error={fieldError(errors.approverUserId)}
          {...register('approverUserId')}
        />
      </form>
    </Dialog>
  );
}
