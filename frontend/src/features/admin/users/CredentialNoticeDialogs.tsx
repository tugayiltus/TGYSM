import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { Button } from '../../../shared/ui/Button';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';

import { DEFAULT_DELIVERY, type CredentialDelivery } from './api';
import { DeliveryChoice } from './DeliveryChoice';

/** Confirms that a set-password link was emailed; there is deliberately nothing secret to show. */
export function LinkSentDialog({ email, onClose }: { email: string; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t('users.linkSent.title')}
      description={t('users.linkSent.message', { email })}
      footer={<Button onClick={onClose}>{tc('actions.close')}</Button>}
    />
  );
}

export interface ResetPasswordDialogProps {
  name: string;
  isPending: boolean;
  error: unknown;
  onConfirm: (delivery: CredentialDelivery) => void;
  onCancel: () => void;
}

/** Password reset with the same delivery choice as user creation. */
export function ResetPasswordDialog({ name, isPending, error, onConfirm, onCancel }: ResetPasswordDialogProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [delivery, setDelivery] = useState<CredentialDelivery>(DEFAULT_DELIVERY);
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onCancel();
        }
      }}
      title={t('users.resetDialog.title')}
      description={t('users.resetDialog.message', { name })}
      footer={
        <>
          <Button variant="secondary" onClick={onCancel}>
            {tc('actions.cancel')}
          </Button>
          <Button isLoading={isPending} onClick={() => onConfirm(delivery)}>
            {tc('actions.confirm')}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <FormAlert message={error ? describeError(error) : null} />
        <DeliveryChoice value={delivery} onChange={setDelivery} />
      </div>
    </Dialog>
  );
}
