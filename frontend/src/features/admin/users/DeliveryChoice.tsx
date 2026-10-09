import { useId } from 'react';
import { useTranslation } from 'react-i18next';

import { CheckField } from '../../../shared/ui/CheckField';

import type { CredentialDelivery } from './api';

const DELIVERIES = ['email_link', 'temporary_password'] as const satisfies readonly CredentialDelivery[];

const DELIVERY_LABEL_KEY = {
  email_link: 'users.delivery.emailLink',
  temporary_password: 'users.delivery.temporaryPassword',
} as const satisfies Record<CredentialDelivery, string>;

export interface DeliveryChoiceProps {
  value: CredentialDelivery;
  onChange: (value: CredentialDelivery) => void;
}

/** How the user gets their first password: emailed set-password link (default) or a temporary password shown once. */
export function DeliveryChoice({ value, onChange }: DeliveryChoiceProps) {
  const { t } = useTranslation('admin');
  const name = useId();
  return (
    <fieldset className="d-flex flex-column">
      <legend className="form-label fw-medium small mb-1">{t('users.delivery.label')}</legend>
      {DELIVERIES.map((delivery) => (
        <CheckField key={delivery} type="radio" name={name} value={delivery} label={t(DELIVERY_LABEL_KEY[delivery])} checked={value === delivery} onChange={() => onChange(delivery)} />
      ))}
    </fieldset>
  );
}
