import { useTranslation } from 'react-i18next';

import type { AssetStatus } from './api';

const STATUS_CLASS: Record<AssetStatus, string> = {
  in_stock: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  assigned: 'bg-brand-50 text-brand-900 ring-brand-200',
  maintenance: 'bg-amber-50 text-amber-900 ring-amber-200',
  lost: 'bg-red-50 text-red-800 ring-red-200',
  retired: 'bg-slate-100 text-slate-700 ring-slate-200',
};

export function AssetStatusBadge({ status }: { status: AssetStatus }) {
  const { t } = useTranslation('assets');
  return <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${STATUS_CLASS[status]}`}>{t(`statuses.${status}`)}</span>;
}
