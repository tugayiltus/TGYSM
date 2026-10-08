import { CBadge } from '@coreui/react';
import { useTranslation } from 'react-i18next';

export function StatusBadge({ isActive }: { isActive: boolean }) {
  const { t } = useTranslation();
  return <CBadge color={isActive ? 'success' : 'secondary'}>{isActive ? t('status.active') : t('status.inactive')}</CBadge>;
}
