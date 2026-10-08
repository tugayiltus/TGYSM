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
import { TextAreaField } from '../../../shared/ui/TextAreaField';
import { TextField } from '../../../shared/ui/TextField';

import { useUpdateEmailTemplate, type EmailTemplateItem } from './api';
import { isEmailEventType } from './eventTypes';

/** Mirror backend `EmailTemplate.SubjectMaxLength` / `BodyMaxLength`. */
export const TEMPLATE_SUBJECT_MAX_LENGTH = 200;
export const TEMPLATE_BODY_MAX_LENGTH = 10_000;
const FORM_FIELDS = ['subject', 'body'] as const;
const VARIABLE_OPEN = '{{';
const VARIABLE_CLOSE = '}}';
const BODY_ROWS = 10;

const templateSchema = z.object({
  subject: z.string().trim().min(1, ERROR_CODES.required).max(TEMPLATE_SUBJECT_MAX_LENGTH, ERROR_CODES.tooLong),
  body: z.string().trim().min(1, ERROR_CODES.required).max(TEMPLATE_BODY_MAX_LENGTH, ERROR_CODES.tooLong),
});

type TemplateValues = z.infer<typeof templateSchema>;

export interface EmailTemplateEditorProps {
  template: EmailTemplateItem;
  onClose: () => void;
}

export function EmailTemplateEditor({ template, onClose }: EmailTemplateEditorProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const update = useUpdateEmailTemplate();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<TemplateValues>({ resolver: zodResolver(templateSchema), defaultValues: { subject: template.subject, body: template.body } });

  const onSubmit = handleSubmit((values) =>
    update.mutate(
      { eventType: template.eventType, version: template.version, ...values },
      { onSuccess: onClose, onError: (error) => applyServerErrors(error, setError, FORM_FIELDS) },
    ),
  );

  const alert = update.error && !isHandledByFields(update.error, FORM_FIELDS) ? describeError(update.error) : null;
  const title = isEmailEventType(template.eventType) ? t(`emailEvents.${template.eventType}`) : template.eventType;

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open && !update.isPending) {
          onClose();
        }
      }}
      title={`${t('emailTemplates.edit')}: ${title}`}
      footer={
        <>
          <Button variant="secondary" disabled={update.isPending} onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={update.isPending}>
            {tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="d-flex flex-column gap-3" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('emailTemplates.fields.subject')} autoComplete="off" error={fieldError(errors.subject)} {...register('subject')} />
        <TextAreaField label={t('emailTemplates.fields.body')} rows={BODY_ROWS} error={fieldError(errors.body)} {...register('body')} />
        <section aria-labelledby={`${formId}-variables`}>
          <h3 id={`${formId}-variables`} className="small fw-medium">
            {t('emailTemplates.variables.title')}
          </h3>
          <p className="mb-0 mt-1 small text-body-secondary">{t('emailTemplates.variables.hint')}</p>
          <ul className="list-unstyled mb-0 mt-2 d-flex flex-wrap gap-2">
            {template.allowedVariables.map((name) => (
              <li key={name} className="rounded-md bg-body-secondary px-2 py-1 font-monospace small">
                {`${VARIABLE_OPEN}${name}${VARIABLE_CLOSE}`}
              </li>
            ))}
          </ul>
        </section>
      </form>
    </Dialog>
  );
}
