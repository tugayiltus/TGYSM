/**
 * @input The chosen delivery and a change callback.
 * @output Radio choices: emailed set-password link (default) or a temporary password shown once.
 * @position User create form and the password reset dialog.
 */
import { RadioList, RadioListItem } from '@astryxdesign/core/RadioList';
import { useTranslation } from 'react-i18next';

import type { CredentialDelivery } from './api';

const DELIVERIES = ['email_link', 'temporary_password'] as const satisfies readonly CredentialDelivery[];

const DELIVERY_LABEL_KEY = {
  email_link: 'users.delivery.emailLink',
  temporary_password: 'users.delivery.temporaryPassword',
} as const satisfies Record<CredentialDelivery, string>;

const isDelivery = (value: string): value is CredentialDelivery => (DELIVERIES as readonly string[]).includes(value);

export interface DeliveryChoiceProps {
  value: CredentialDelivery;
  onChange: (value: CredentialDelivery) => void;
}

export function DeliveryChoice({ value, onChange }: DeliveryChoiceProps) {
  const { t } = useTranslation('admin');
  return (
    <RadioList label={t('users.delivery.label')} value={value} onChange={(next) => isDelivery(next) && onChange(next)}>
      {DELIVERIES.map((delivery) => (
        <RadioListItem key={delivery} value={delivery} label={t(DELIVERY_LABEL_KEY[delivery])} />
      ))}
    </RadioList>
  );
}
