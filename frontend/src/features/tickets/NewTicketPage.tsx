import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { ticketPath } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { FormSection } from '../../shared/ui/FormSection';
import { cilPlus, cilWarning } from '../../shared/ui/icons';
import { SelectField } from '../../shared/ui/SelectField';
import { TextAreaField } from '../../shared/ui/TextAreaField';
import { TextField } from '../../shared/ui/TextField';
import { UnsavedChangesGuard } from '../../shared/ui/UnsavedChangesGuard';
import { useMyAssets, useReferenceLocations } from '../assets/api';

import { useAddComment, useCreateTicket, useReferenceCategories, useReferenceRequestTypes, type TicketKind } from './api';
import { AttachmentInput } from './AttachmentInput';
import { describeCommentError as describeUploadError } from './commentErrors';
import type { TicketDetailState } from './TicketDetailPage';

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
  assetId: z.string(),
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
  const myAssets = useMyAssets(kind === 'incident');
  const create = useCreateTicket();
  const upload = useAddComment();
  const [files, setFiles] = useState<File[]>([]);
  const isRequest = kind === 'request';

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isDirty },
  } = useForm<TicketFormValues>({
    resolver: zodResolver(isRequest ? requestSchema : ticketSchema),
    defaultValues: { subject: '', description: '', categoryId: NO_SELECTION, locationId: NO_SELECTION, requestTypeId: NO_SELECTION, assetId: NO_SELECTION },
  });
  const hasChanges = isDirty || files.length > 0;
  const requestTypeId = useWatch({ control, name: 'requestTypeId' });
  const needsApproval = requestTypes.data?.some((r) => r.id === requestTypeId && r.requiresApproval) ?? false;

  const categoryOptions = useMemo(() => (categories.data ?? []).map((c) => ({ value: c.id, label: c.path })), [categories.data]);
  const locationOptions = useMemo(() => (locations.data ?? []).map((l) => ({ value: l.id, label: l.path })), [locations.data]);
  const assetOptions = useMemo(() => (myAssets.data ?? []).map((a) => ({ value: a.id, label: `${a.assetTag} · ${a.brand} ${a.model}` })), [myAssets.data]);
  const typeOptions = useMemo(() => (requestTypes.data ?? []).map((r) => ({ value: r.id, label: r.name })), [requestTypes.data]);

  const onSubmit = handleSubmit((values) => {
    create.mutate(
      {
        kind,
        subject: values.subject,
        description: values.description,
        categoryId: nullIfEmpty(values.categoryId),
        locationId: nullIfEmpty(values.locationId),
        requestTypeId: isRequest ? values.requestTypeId : null,
        assetId: isRequest ? null : nullIfEmpty(values.assetId),
      },
      {
        onError: (error) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS),
        onSuccess: (ticket) => {
          const open = (state?: TicketDetailState) => void navigate(ticketPath(ticket.number), { state: { ...state, created: true } });
          if (files.length === 0) {
            open();
            return;
          }
          upload.mutate(
            { number: ticket.number, body: '', visibility: 'public', files },
            // The ticket exists: open it with the error instead of staying here, where "Gönder" would create a duplicate.
            { onSuccess: () => open(), onError: (error) => open({ uploadError: t('new.filesFailed', { message: describeUploadError(error) }) }) },
          );
        },
      },
    );
  });

  const alert = create.error && !isHandledByFields(create.error, FORM_FIELDS, CODE_FIELDS) ? describeError(create.error) : null;

  return (
    <>
      <PageHeader icon={ROUTE_META.myTickets.icon} titleOverride={isRequest ? t('new.requestTitle') : t('new.incidentTitle')} />
      <UnsavedChangesGuard when={({ nextLocation }) => hasChanges && (nextLocation.state as TicketDetailState | null)?.created !== true} />
      <form noValidate onSubmit={(event) => void onSubmit(event)}>
        <FormSection
          actions={
            <Button type="submit" icon={isRequest ? cilPlus : cilWarning} isLoading={create.isPending || upload.isPending}>
              {t('new.submit')}
            </Button>
          }
        >
          <p className="col-12 mb-0 text-body-secondary">{isRequest ? t('new.requestIntro') : t('new.incidentIntro')}</p>
          <div className="col-12">
            <FormAlert message={alert} />
          </div>
          {isRequest && (
            <>
              <SelectField className="col-12 col-md-6" label={t('new.requestType')} options={typeOptions} placeholder={t('new.selectRequestType')} error={fieldError(errors.requestTypeId)} {...register('requestTypeId')} />
              {needsApproval && <p className="col-12 alert alert-warning mb-0">{t('new.approvalHint')}</p>}
            </>
          )}
          <TextField className="col-12" label={t('new.subject')} autoComplete="off" error={fieldError(errors.subject)} {...register('subject')} />
          <TextAreaField className="col-12" label={t('new.description')} error={fieldError(errors.description)} {...register('description')} />
          <SelectField className="col-12 col-md-6" label={t('new.category')} options={categoryOptions} placeholder={t('fields.select')} error={fieldError(errors.categoryId)} {...register('categoryId')} />
          {!isRequest && (
            <SelectField className="col-12 col-md-6" label={t('new.asset')} options={assetOptions} placeholder={t('new.noAsset')} hint={t('new.assetHint')} {...register('assetId')} />
          )}
          <SelectField className="col-12 col-md-6" label={t('new.location')} options={locationOptions} placeholder={t('fields.select')} error={fieldError(errors.locationId)} {...register('locationId')} />
          <div className="col-12">
            <AttachmentInput label={t('new.files')} hint={t('new.filesHint')} files={files} onChange={setFiles} />
          </div>
        </FormSection>
      </form>
    </>
  );
}

export function NewIncidentPage() {
  return <NewTicketPage kind="incident" />;
}

export function NewRequestPage() {
  return <NewTicketPage kind="request" />;
}
