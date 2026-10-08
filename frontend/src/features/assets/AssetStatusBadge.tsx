import { useTranslation } from 'react-i18next';

import type { AssetStatus } from './api';

const STATUS_CLASS: Record<AssetStatus, string> = {
  in_stock: 'bg-success-subtle text-success-emphasis border-success-subtle',
  assigned: 'bg-primary-subtle text-primary-emphasis border-primary-subtle',
  maintenance: 'bg-warning-subtle text-warning-emphasis border-warning-subtle',
  lost: 'bg-danger-subtle text-danger-emphasis border-danger-subtle',
  retired: 'bg-secondary-subtle text-secondary-emphasis border-secondary-subtle',
};

export function AssetStatusBadge({ status }: { status: AssetStatus }) {
  const { t } = useTranslation('assets');
  return <span className={`badge rounded-pill border fw-medium ${STATUS_CLASS[status]}`}>{t(`statuses.${status}`)}</span>;
}
