import { useTranslation } from 'react-i18next';

export function StatusBadge({ isActive }: { isActive: boolean }) {
  const { t } = useTranslation();
  return (
    <span
      className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${isActive ? 'bg-emerald-50 text-emerald-800' : 'bg-slate-100 text-slate-700'}`}
    >
      {isActive ? t('status.active') : t('status.inactive')}
    </span>
  );
}
