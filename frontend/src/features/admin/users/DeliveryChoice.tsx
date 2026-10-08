import { useId } from 'react';
import { useTranslation } from 'react-i18next';

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
    <fieldset className="d-flex flex-column gap-2">
      <legend className="small fw-medium">{t('users.delivery.label')}</legend>
      {DELIVERIES.map((delivery) => (
        <label key={delivery} className="d-flex tgysm-touch align-items-center gap-2">
          <input type="radio" name={name} value={delivery} checked={value === delivery} onChange={() => onChange(delivery)} />
          {t(DELIVERY_LABEL_KEY[delivery])}
        </label>
      ))}
    </fieldset>
  );
}
