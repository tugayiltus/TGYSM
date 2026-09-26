import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { ticketPath } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { SelectField } from '../../shared/ui/SelectField';
import { TextAreaField } from '../../shared/ui/TextAreaField';
import { TextField } from '../../shared/ui/TextField';
import { useReferenceLocations } from '../assets/api';

import { useAddComment, useCreateTicket, useReferenceCategories, useReferenceRequestTypes, type TicketKind } from './api';
import { AttachmentInput } from './AttachmentInput';

/** Mirror backend Ticket.SubjectMaxLength / DescriptionMaxLength. */
const SUBJECT_MAX_LENGTH = 200;
const DESCRIPTION_MAX_LENGTH = 4000;
const NO_SELECTION = '';

const ticketSchema = z.object({
  subject: z.string().trim().min(1, ERROR_CODES.required).max(SUBJECT_MAX_LENGTH, ERROR_CODES.tooLong),
  description: z.string().trim().min(1, ERROR_CODES.required).max(DESCRIPTION_MAX_LENGTH, ERROR_CODES.tooLong),
  categoryId: z.string(),
  locationId: z.string(),
  requestTypeId: z.string(),
});
const requestSchema = ticketSchema.extend({ requestTypeId: z.string().min(1, ERROR_CODES.required) });

type TicketFormValues = z.infer<typeof ticketSchema>;

const FORM_FIELDS = ['subject', 'description', 'categoryId', 'locationId', 'requestTypeId'] as const;
const CODE_FIELDS = { [ERROR_CODES.referenceInactive]: 'categoryId' } as const;

const nullIfEmpty = (value: string) => (value === NO_SELECTION ? null : value);

export function NewTicketPage({ kind }: { kind: TicketKind }) {
  const { t } = useTranslation('tickets');
  const navigate = useNavigate();
  const categories = useReferenceCategories(kind);
  const locations = useReferenceLocations();
  const requestTypes = useReferenceRequestTypes();
  const create = useCreateTicket();
  const upload = useAddComment();
  const [files, setFiles] = useState<File[]>([]);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const isRequest = kind === 'request';

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<TicketFormValues>({
    resolver: zodResolver(isRequest ? requestSchema : ticketSchema),
    defaultValues: { subject: '', description: '', categoryId: NO_SELECTION, locationId: NO_SELECTION, requestTypeId: NO_SELECTION },
  });
  const requestTypeId = useWatch({ control, name: 'requestTypeId' });
  const needsApproval = requestTypes.data?.some((r) => r.id === requestTypeId && r.requiresApproval) ?? false;

  const categoryOptions = useMemo(() => (categories.data ?? []).map((c) => ({ value: c.id, label: c.path })), [categories.data]);
  const locationOptions = useMemo(() => (locations.data ?? []).map((l) => ({ value: l.id, label: l.path })), [locations.data]);
  const typeOptions = useMemo(() => (requestTypes.data ?? []).map((r) => ({ value: r.id, label: r.name })), [requestTypes.data]);

  const onSubmit = handleSubmit((values) => {
    setUploadError(null);
    create.mutate(
      {
        kind,
        subject: values.subject,
        description: values.description,
        categoryId: nullIfEmpty(values.categoryId),
        locationId: nullIfEmpty(values.locationId),
        requestTypeId: isRequest ? values.requestTypeId : null,
      },
      {
        onError: (error) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS),
        onSuccess: (ticket) => {
          const open = () => void navigate(ticketPath(ticket.number));
          if (files.length === 0) {
            open();
            return;
          }
          upload.mutate(
            { number: ticket.number, body: '', visibility: 'public', files },
            { onSuccess: open, onError: (error) => setUploadError(t('new.filesFailed', { message: describeError(error) })) },
          );
        },
      },
    );
  });

  const alert = create.error && !isHandledByFields(create.error, FORM_FIELDS, CODE_FIELDS) ? describeError(create.error) : uploadError;

  return (
    <section className="mx-auto flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-semibold">{isRequest ? t('new.requestTitle') : t('new.incidentTitle')}</h1>
      <p className="text-slate-700">{isRequest ? t('new.requestIntro') : t('new.incidentIntro')}</p>
      <form noValidate className="flex flex-col gap-4" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        {isRequest && (
          <>
            <SelectField label={t('new.requestType')} options={typeOptions} placeholder={t('new.selectRequestType')} error={fieldError(errors.requestTypeId)} {...register('requestTypeId')} />
            {needsApproval && <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900">{t('new.approvalHint')}</p>}
          </>
        )}
        <TextField label={t('new.subject')} autoComplete="off" error={fieldError(errors.subject)} {...register('subject')} />
        <TextAreaField label={t('new.description')} error={fieldError(errors.description)} {...register('description')} />
        <SelectField label={t('new.category')} options={categoryOptions} placeholder={t('fields.select')} error={fieldError(errors.categoryId)} {...register('categoryId')} />
        <SelectField label={t('new.location')} options={locationOptions} placeholder={t('fields.select')} error={fieldError(errors.locationId)} {...register('locationId')} />
        <AttachmentInput label={t('new.files')} hint={t('new.filesHint')} files={files} onChange={setFiles} />
        <Button type="submit" className="self-start" isLoading={create.isPending || upload.isPending}>
          {t('new.submit')}
        </Button>
      </form>
    </section>
  );
}

export function NewIncidentPage() {
  return <NewTicketPage kind="incident" />;
}

export function NewRequestPage() {
  return <NewTicketPage kind="request" />;
}
