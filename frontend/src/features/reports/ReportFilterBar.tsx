import { useTranslation } from 'react-i18next';

import { translateOr } from '../../shared/i18n/dynamic';
import { SelectField, type SelectOption } from '../../shared/ui/SelectField';

import { useFilterChoices, type ReportName } from './api';
import { DayField } from './DayField';
import { DAY_BOUNDS, ENUM_FILTERS, isValidFilterValue, REPORT_FILTER_FIELDS, type ReportFilterKey, type ReportFilterValues } from './useReportFilter';

/** Where each enum filter finds its value names. */
const ENUM_LABELS: Record<keyof typeof ENUM_FILTERS, string> = {
  kind: 'tickets:kinds',
  priority: 'tickets:priorities',
  status: 'tickets:statuses',
  slaState: 'reports:slaStates',
  assetType: 'assets:types',
  assetStatus: 'assets:statuses',
};

const isEnumKey = (key: ReportFilterKey): key is keyof typeof ENUM_FILTERS => key in ENUM_FILTERS;

export interface ReportFilterBarProps {
  report: ReportName;
  filter: ReportFilterValues;
  onChange: (key: ReportFilterKey, value: string) => void;
}

/** The report's filters; every change is written to the URL by the caller (useReportFilter). */
export function ReportFilterBar({ report, filter, onChange }: ReportFilterBarProps) {
  const { t } = useTranslation('reports');
  const fields = REPORT_FILTER_FIELDS[report];
  const choices = useFilterChoices(fields);

  const optionsOf = (key: Exclude<ReportFilterKey, 'from' | 'to'>): SelectOption[] =>
    isEnumKey(key) ? ENUM_FILTERS[key].map((value) => ({ value, label: translateOr(`${ENUM_LABELS[key]}.${value}`, value) })) : choices[key];

  return (
    <form aria-label={t('filters.label')} className="d-flex flex-column gap-2" onSubmit={(event) => event.preventDefault()}>
      <div className="tgysm-grid tgysm-grid-filters">
        {fields.map((key) =>
          key === 'from' || key === 'to' ? (
            <DayField
              key={key}
              label={t(`filters.${key}`)}
              min={key === 'to' ? (filter.from ?? DAY_BOUNDS.min) : DAY_BOUNDS.min}
              max={key === 'from' ? (filter.to ?? DAY_BOUNDS.max) : DAY_BOUNDS.max}
              value={filter[key] ?? ''}
              isValid={(value) => isValidFilterValue(key, value)}
              onChange={(value) => onChange(key, value)}
            />
          ) : (
            <SelectField
              key={key}
              label={t(`filters.${key}`)}
              placeholder={t('filters.all')}
              options={optionsOf(key)}
              value={filter[key] ?? ''}
              onChange={(event) => onChange(key, event.target.value)}
            />
          ),
        )}
      </div>
    </form>
  );
}
