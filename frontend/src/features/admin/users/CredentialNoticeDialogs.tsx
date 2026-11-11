/**
 * @input A user name or e-mail, the reset state and callbacks.
 * @output The "link sent" notice and the password reset dialog with the delivery choice.
 * @position User list actions.
 */
import { Text } from '@astryxdesign/core/Text';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ActionDialog } from '../../../shared/feedback/ActionDialog';
import { InfoDialog } from '../../../shared/feedback/InfoDialog';

import { DEFAULT_DELIVERY, type CredentialDelivery } from './api';
import { DeliveryChoice } from './DeliveryChoice';

/** Confirms that a set-password link was emailed; there is deliberately nothing secret to show. */
export function LinkSentDialog({ email, onClose }: { email: string; onClose: () => void }) {
  const { t } = useTranslation('admin');
  return <InfoDialog title={t('users.linkSent.title')} message={t('users.linkSent.message', { email })} onClose={onClose} />;
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
  const [delivery, setDelivery] = useState<CredentialDelivery>(DEFAULT_DELIVERY);
  return (
    <ActionDialog title={t('users.resetDialog.title')} isPending={isPending} error={error} onConfirm={() => onConfirm(delivery)} onClose={onCancel}>
      <Text type="body">{t('users.resetDialog.message', { name })}</Text>
      <DeliveryChoice value={delivery} onChange={setDelivery} />
    </ActionDialog>
  );
}
