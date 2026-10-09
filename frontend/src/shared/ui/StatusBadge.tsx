import { CBadge } from '@coreui/react';
import { useTranslation } from 'react-i18next';

export function StatusBadge({ isActive }: { isActive: boolean }) {
  const { t } = useTranslation();
  return <CBadge className={isActive ? 'bg-success-subtle text-success-emphasis' : 'bg-secondary-subtle text-secondary-emphasis'}>{isActive ? t('status.active') : t('status.inactive')}</CBadge>;
}
