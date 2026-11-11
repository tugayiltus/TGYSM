/**
 * @input The current status filter and a change callback.
 * @output A "Durum" selector with Tümü, Aktif and Pasif.
 * @position Filter bar of admin lists that show active and inactive records.
 */
import { Selector } from '@astryxdesign/core/Selector';
import { useTranslation } from 'react-i18next';

import { STATUS_FILTERS, type StatusFilter } from './statusFilter';

export function StatusFilterSelector({ value, onChange }: { value: StatusFilter | undefined; onChange: (value: string) => void }) {
  const { t } = useTranslation();
  return (
    <Selector
      label={t('filters.status')}
      placeholder={t('status.all')}
      value={value ?? ''}
      options={STATUS_FILTERS.map((status) => ({ value: status, label: t(`status.${status}`) }))}
      onChange={onChange}
    />
  );
}
