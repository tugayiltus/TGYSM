/**
 * @input Title, message, confirm label, pending state, last error, and the two callbacks.
 * @output A modal that asks the person to confirm one action.
 * @position Rendered by a page or by `UnsavedChangesGuard` while a decision is open.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { describeError } from '../api/errorMessage';

const DIALOG_WIDTH = 440;

export interface ConfirmDialogProps {
  title: string;
  message: string;
  confirmLabel: string;
  variant?: 'primary' | 'danger';
  isPending: boolean;
  error: unknown;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * A `Dialog`, not an `AlertDialog`: a failed action must show its error inside the dialog.
 * The dialog shows while it is rendered. The parent removes it on `onCancel`.
 */
export function ConfirmDialog({ title, message, confirmLabel, variant = 'primary', isPending, error, onConfirm, onCancel }: ConfirmDialogProps) {
  const { t } = useTranslation();
  const close = (open: boolean) => {
    if (!open) {
      onCancel();
    }
  };
  return (
    <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH} purpose="form">
      <Layout
        header={<DialogHeader title={title} onOpenChange={close} />}
        content={
          <LayoutContent>
            <VStack gap={3}>
              <Text type="body">{message}</Text>
              {error ? <Banner status="error" title={describeError(error)} /> : null}
            </VStack>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack gap={2} hAlign="end">
              <Button label={t('actions.cancel')} variant="secondary" onClick={onCancel} />
              <Button label={confirmLabel} variant={variant === 'danger' ? 'destructive' : 'primary'} isLoading={isPending} onClick={onConfirm} />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
  );
}
