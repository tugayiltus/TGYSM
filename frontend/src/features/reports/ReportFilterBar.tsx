/**
 * @input A report, the current filter and a change callback.
 * @output The report's filters: period days and the report's own selects.
 * @position Top of every report page; the caller writes each change to the URL (useReportFilter).
 */
import { Grid } from '@astryxdesign/core/Grid';
import { Selector } from '@astryxdesign/core/Selector';
import { useTranslation } from 'react-i18next';

import type { SelectOption } from '../../shared/form/formFields';
import { translateOr } from '../../shared/i18n/dynamic';

import { useFilterChoices, type ReportName } from './api';
import { DayField } from './DayField';
import { DAY_BOUNDS, ENUM_FILTER_LABELS, ENUM_FILTERS, isValidFilterValue, REPORT_FILTER_FIELDS, type ReportFilterKey, type ReportFilterValues } from './useReportFilter';

const isEnumKey = (key: ReportFilterKey): key is keyof typeof ENUM_FILTERS => key in ENUM_FILTERS;
/** Filters reflow from one column on phones to a row on wide screens. */
const FILTER_COLUMNS = { minWidth: 200 } as const;

export interface ReportFilterBarProps {
  report: ReportName;
  filter: ReportFilterValues;
  onChange: (key: ReportFilterKey, value: string) => void;
}

/** No per-field clear button: the active-filter chips and Temizle remove filters. */
export function ReportFilterBar({ report, filter, onChange }: ReportFilterBarProps) {
  const { t } = useTranslation('reports');
  const fields = REPORT_FILTER_FIELDS[report];
  const choices = useFilterChoices(fields);

  const optionsOf = (key: Exclude<ReportFilterKey, 'from' | 'to'>): SelectOption[] =>
    isEnumKey(key) ? ENUM_FILTERS[key].map((value) => ({ value, label: translateOr(`${ENUM_FILTER_LABELS[key]}.${value}`, value) })) : choices[key];

  return (
    <form aria-label={t('filters.label')} onSubmit={(event) => event.preventDefault()}>
      <Grid columns={FILTER_COLUMNS} gap={3}>
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
            <Selector
              key={key}
              label={t(`filters.${key}`)}
              placeholder={t('filters.all')}
              options={[...optionsOf(key)]}
              value={filter[key] ?? ''}
              onChange={(value) => onChange(key, value)}
            />
          ),
        )}
      </Grid>
    </form>
  );
}
