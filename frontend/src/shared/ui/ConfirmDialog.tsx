import { useTranslation } from 'react-i18next';

import { describeError } from '../api/errorMessage';

import { Button } from './Button';
import { Dialog } from './Dialog';
import { FormAlert } from './FormAlert';

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

export function ConfirmDialog({ title, message, confirmLabel, variant = 'primary', isPending, error, onConfirm, onCancel }: ConfirmDialogProps) {
  const { t } = useTranslation();
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onCancel();
        }
      }}
      title={title}
      description={message}
      footer={
        <>
          <Button variant="secondary" onClick={onCancel}>
            {t('actions.cancel')}
          </Button>
          <Button variant={variant} isLoading={isPending} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <FormAlert message={error ? describeError(error) : null} />
    </Dialog>
  );
}
