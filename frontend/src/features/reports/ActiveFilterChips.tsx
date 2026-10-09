import { useTranslation } from 'react-i18next';

import { translateOr } from '../../shared/i18n/dynamic';

import { activeFilterKeys, ENUM_FILTERS, type ReportFilterKey, type ReportFilterValues } from './useReportFilter';

/** Where enum filter values find their names (as in the filter bar). */
const ENUM_LABELS: Record<keyof typeof ENUM_FILTERS, string> = {
  kind: 'tickets:kinds',
  priority: 'tickets:priorities',
  status: 'tickets:statuses',
  slaState: 'reports:slaStates',
  assetType: 'assets:types',
  assetStatus: 'assets:statuses',
};

/** Filters other than the period, as chips that remove themselves. */
export function ActiveFilterChips({ filter, onRemove }: { filter: ReportFilterValues; onRemove: (key: ReportFilterKey) => void }) {
  const { t } = useTranslation('reports');
  const keys = activeFilterKeys(filter);
  if (keys.length === 0) {
    return null;
  }
  return (
    <ul aria-label={t('filters.active')} className="list-unstyled d-flex flex-wrap gap-2 mb-0">
      {keys.map((key) => {
        const value = filter[key] ?? '';
        const valueLabel = key in ENUM_LABELS ? translateOr(`${ENUM_LABELS[key as keyof typeof ENUM_LABELS]}.${value}`, value) : null;
        const label = valueLabel === null ? t(`filters.${key}`) : `${t(`filters.${key}`)}: ${valueLabel}`;
        return (
          <li key={key}>
            <button type="button" className="btn btn-sm btn-light border rounded-pill" aria-label={t('filters.remove', { name: label })} onClick={() => onRemove(key)}>
              {label} <span aria-hidden="true">×</span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
