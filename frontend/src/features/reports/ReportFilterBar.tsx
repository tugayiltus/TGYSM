/**
 * @input A report, the current filter and a change callback.
 * @output The report's filters across the full width: the main ones first (days side by side), the rest under a
 *   collapsed "Diğer filtreler" that counts its active filters.
 * @position Top of every report page; the caller writes each change to the URL (useReportFilter).
 */
import { Collapsible } from '@astryxdesign/core/Collapsible';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { Selector } from '@astryxdesign/core/Selector';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import type { SelectOption } from '../../shared/form/formFields';
import { translateOr } from '../../shared/i18n/dynamic';
import { useIsPhone } from '../../shared/list/useIsPhone';

import { useFilterChoices, type ReportName } from './api';
import { DayField } from './DayField';
import { DAY_BOUNDS, ENUM_FILTER_LABELS, ENUM_FILTERS, isValidFilterValue, REPORT_FILTER_FIELDS, splitFilterFields, type ReportFilterKey, type ReportFilterValues } from './useReportFilter';

const isEnumKey = (key: ReportFilterKey): key is keyof typeof ENUM_FILTERS => key in ENUM_FILTERS;
/** Two to four columns on a report card; one column in a narrow container such as the phone drawer. */
const FILTER_COLUMNS = { minWidth: 220, max: 4 } as const;
/** Start and end day share one row and take two filter columns. */
const DAY_COLUMNS = 2;
const isDayKey = (key: ReportFilterKey): key is 'from' | 'to' => key === 'from' || key === 'to';

export interface ReportFilterBarProps {
  report: ReportName;
  filter: ReportFilterValues;
  onChange: (key: ReportFilterKey, value: string) => void;
}

/** No per-field clear button: the active-filter chips and Temizle remove filters. */
export function ReportFilterBar({ report, filter, onChange }: ReportFilterBarProps) {
  const { t } = useTranslation('reports');
  const choices = useFilterChoices(REPORT_FILTER_FIELDS[report]);
  // In the phone drawer the grid has one column; spanning two there would add a column and overflow.
  const dayColumns = useIsPhone() ? 'full' : DAY_COLUMNS;

  const optionsOf = (key: Exclude<ReportFilterKey, 'from' | 'to'>): SelectOption[] =>
    isEnumKey(key) ? ENUM_FILTERS[key].map((value) => ({ value, label: translateOr(`${ENUM_FILTER_LABELS[key]}.${value}`, value) })) : choices[key];

  const field = (key: ReportFilterKey) =>
    isDayKey(key) ? (
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
    );

  const { main, other } = splitFilterFields(report);
  const days = main.filter(isDayKey);
  const otherActive = other.filter((key) => filter[key] !== undefined).length;

  return (
    <form aria-label={t('filters.label')} onSubmit={(event) => event.preventDefault()}>
      <VStack gap={3}>
        <Grid columns={FILTER_COLUMNS} gap={3}>
          {days.length > 0 && (
            <GridSpan columns={dayColumns}>
              <Grid columns={DAY_COLUMNS} gap={3}>
                {days.map(field)}
              </Grid>
            </GridSpan>
          )}
          {main.filter((key) => !isDayKey(key)).map(field)}
        </Grid>
        {other.length > 0 && (
          <Collapsible trigger={otherActive > 0 ? t('filters.moreCount', { count: otherActive }) : t('filters.more')} defaultIsOpen={false}>
            <Grid columns={FILTER_COLUMNS} gap={3}>
              {other.map(field)}
            </Grid>
          </Collapsible>
        )}
      </VStack>
    </form>
  );
}
