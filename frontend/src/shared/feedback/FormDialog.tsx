/**
 * @input Title, submit label, pending state, a form-level error, the submit handler, the fields and a close callback.
 * @output A modal form: header, error, fields, then Vazgeç and a submit button in the footer.
 * @position Create and edit dialogs (admin records, assets).
 */
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { VStack } from '@astryxdesign/core/VStack';
import { useId, type FormEvent, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { FormAlert } from './FormAlert';
import { useDiscardGuard } from './useDiscardGuard';

const DEFAULT_WIDTH = 520;

export interface FormDialogProps {
  title: string;
  submitLabel: string;
  isPending: boolean;
  /** A message no single field owns, or null. */
  alert: string | null;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onClose: () => void;
  /** The form has unsaved changes: closing it asks first. */
  isDirty?: boolean;
  width?: number;
  children: ReactNode;
}

/** The submit button sits in the footer, outside the form, so it names the form by id. The parent removes the dialog on close. */
export function FormDialog({ title, submitLabel, isPending, alert, onSubmit, onClose, isDirty = false, width = DEFAULT_WIDTH, children }: FormDialogProps) {
  const { t } = useTranslation();
  const formId = useId();
  const { requestClose, confirm } = useDiscardGuard(isDirty && !isPending, onClose);
  const close = (open: boolean) => {
    if (!open) {
      requestClose();
    }
  };
  return (
    <>
    <Dialog isOpen onOpenChange={close} width={width} purpose="form">
      <Layout
        header={<DialogHeader title={title} onOpenChange={close} />}
        content={
          <LayoutContent>
            <form id={formId} noValidate onSubmit={onSubmit}>
              <VStack gap={4}>
                <FormAlert message={alert} />
                {children}
              </VStack>
            </form>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack gap={2} hAlign="end">
              <Button label={t('actions.cancel')} variant="secondary" onClick={requestClose} />
              <Button type="submit" form={formId} label={submitLabel} variant="primary" isLoading={isPending} />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
    {confirm}
    </>
  );
}
