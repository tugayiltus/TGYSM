import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TextField } from '../../../shared/ui/TextField';

import { useRequestTypes, useSaveRequestType, useSetRequestTypeActive, type RequestTypeItem } from './api';

/** Mirrors backend RequestType.NameMaxLength. */
const NAME_MAX_LENGTH = 150;

const schema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  requiresApproval: z.boolean(),
});
type Values = z.infer<typeof schema>;

const FORM_FIELDS = ['name', 'requiresApproval'] as const;
const CODE_FIELDS = { [ERROR_CODES.requestTypeNameTaken]: 'name' } as const;

function RequestTypeFormDialog({ type, onClose }: { type: RequestTypeItem | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const save = useSaveRequestType();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { name: type?.name ?? '', requiresApproval: type?.requiresApproval ?? false } });
  const onSubmit = handleSubmit((v) =>
    save.mutate(
      { id: type?.id ?? null, version: type?.version ?? 0, name: v.name, requiresApproval: v.requiresApproval },
      { onSuccess: onClose, onError: (error) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) },
    ),
  );
  const alert = save.error && !isHandledByFields(save.error, FORM_FIELDS, CODE_FIELDS) ? describeError(save.error) : null;

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={type === null ? t('requestTypes.new') : t('requestTypes.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={save.isPending}>
            {type === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="flex flex-col gap-4" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('requestTypes.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
        <label className="flex min-h-11 items-center gap-3 text-sm">
          <input type="checkbox" className="size-5" {...register('requiresApproval')} />
          {t('requestTypes.fields.requiresApproval')}
        </label>
      </form>
    </Dialog>
  );
}

export function RequestTypesPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const types = useRequestTypes();
  const setActive = useSetRequestTypeActive();
  const [form, setForm] = useState<{ type: RequestTypeItem | null } | null>(null);
  const [toggling, setToggling] = useState<RequestTypeItem | null>(null);

  const approvalText = (r: RequestTypeItem) => (r.requiresApproval ? t('requestTypes.approval') : t('requestTypes.noApproval'));
  const actions = (r: RequestTypeItem) => (
    <div className="flex flex-wrap gap-2">
      <Button variant="secondary" aria-label={recordActionLabel(tc('actions.edit'), r.name)} onClick={() => setForm({ type: r })}>
        {tc('actions.edit')}
      </Button>
      <Button
        variant="secondary"
        aria-label={recordActionLabel(r.isActive ? tc('actions.deactivate') : tc('actions.reactivate'), r.name)}
        onClick={() => {
          setActive.reset();
          setToggling(r);
        }}
      >
        {r.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
      </Button>
    </div>
  );
  const columns: Column<RequestTypeItem>[] = [
    { key: 'name', header: t('requestTypes.columns.name'), renderCell: (r) => <span className="font-medium">{r.name}</span> },
    { key: 'approval', header: t('requestTypes.columns.approval'), renderCell: approvalText },
    { key: 'status', header: t('requestTypes.columns.status'), renderCell: (r) => <StatusBadge isActive={r.isActive} /> },
    { key: 'actions', header: t('requestTypes.columns.actions'), renderCell: actions },
  ];
  const renderCard = (r: RequestTypeItem) => (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium break-words">{r.name}</p>
          <p className="text-sm text-slate-600">{approvalText(r)}</p>
        </div>
        <StatusBadge isActive={r.isActive} />
      </div>
      {actions(r)}
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('requestTypes.title')}</h1>
        <Button onClick={() => setForm({ type: null })}>{t('requestTypes.new')}</Button>
      </div>
      {types.isPending && <LoadingState />}
      {types.isError && <ErrorState error={types.error} onRetry={() => void types.refetch()} />}
      {types.data && types.data.length === 0 && <EmptyState />}
      {types.data && types.data.length > 0 && <ResponsiveList items={types.data} columns={columns} getKey={(r) => r.id} renderCard={renderCard} caption={t('requestTypes.listCaption')} />}
      {form !== null && <RequestTypeFormDialog key={form.type?.id ?? 'new'} type={form.type} onClose={() => setForm(null)} />}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('requestTypes.confirmDeactivate', { name: toggling.name }) : t('requestTypes.confirmReactivate', { name: toggling.name })}
          confirmLabel={tc('actions.confirm')}
          variant={toggling.isActive ? 'danger' : 'primary'}
          isPending={setActive.isPending}
          error={setActive.error}
          onConfirm={() => setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })}
          onCancel={() => setToggling(null)}
        />
      )}
    </section>
  );
}
