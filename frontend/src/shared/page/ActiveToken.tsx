/**
 * @input Whether a record is active.
 * @output A green "Aktif" or gray "Pasif" token.
 * @position Admin lists and detail rows.
 */
import { Token } from '@astryxdesign/core/Token';
import { useTranslation } from 'react-i18next';

export function ActiveToken({ isActive }: { isActive: boolean }) {
  const { t } = useTranslation();
  return <Token size="sm" color={isActive ? 'green' : 'gray'} label={isActive ? t('status.active') : t('status.inactive')} />;
}
