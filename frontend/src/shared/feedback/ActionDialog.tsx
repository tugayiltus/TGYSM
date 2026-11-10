/**
 * @input Title, confirm label and state, an optional error, the form content and the two callbacks.
 * @output A modal with the content, the error above it, and Vazgeç plus a confirm button.
 * @position Small one-step forms: link an asset, link an article, create a related request.
 */
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { VStack } from '@astryxdesign/core/VStack';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../api/errorMessage';

import { FormAlert } from './FormAlert';

const DIALOG_WIDTH = 480;

export interface ActionDialogProps {
  title: string;
  confirmLabel?: string;
  isPending: boolean;
  isConfirmDisabled?: boolean;
  error: unknown;
  onConfirm: () => void;
  onClose: () => void;
  children: ReactNode;
}

/** The parent renders the dialog while it is open and removes it on `onClose`. */
export function ActionDialog({ title, confirmLabel, isPending, isConfirmDisabled = false, error, onConfirm, onClose, children }: ActionDialogProps) {
  const { t } = useTranslation();
  const close = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };
  return (
    <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH} purpose="form">
      <Layout
        header={<DialogHeader title={title} onOpenChange={close} />}
        content={
          <LayoutContent>
            <VStack gap={4}>
              <FormAlert message={error ? describeError(error) : null} />
              {children}
            </VStack>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack gap={2} hAlign="end">
              <Button label={t('actions.cancel')} variant="secondary" onClick={onClose} />
              <Button label={confirmLabel ?? t('actions.confirm')} variant="primary" isDisabled={isConfirmDisabled} isLoading={isPending} onClick={onConfirm} />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
  );
}
