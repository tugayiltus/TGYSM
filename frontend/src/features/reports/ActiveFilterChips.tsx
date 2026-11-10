/**
 * @input The current filter and a remove callback.
 * @output The filters other than the period, as buttons that remove themselves.
 * @position Report pages, under the filter bar.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { useTranslation } from 'react-i18next';

import { translateOr } from '../../shared/i18n/dynamic';
import { Icons } from '../../shared/ui/icons';

import { activeFilterKeys, ENUM_FILTER_LABELS, type ReportFilterKey, type ReportFilterValues } from './useReportFilter';

export function ActiveFilterChips({ filter, onRemove }: { filter: ReportFilterValues; onRemove: (key: ReportFilterKey) => void }) {
  const { t } = useTranslation('reports');
  const keys = activeFilterKeys(filter);
  if (keys.length === 0) {
    return null;
  }
  return (
    <HStack gap={2} wrap="wrap" role="list" aria-label={t('filters.active')}>
      {keys.map((key) => {
        const value = filter[key] ?? '';
        const valueLabel = key in ENUM_FILTER_LABELS ? translateOr(`${ENUM_FILTER_LABELS[key as keyof typeof ENUM_FILTER_LABELS]}.${value}`, value) : null;
        const label = valueLabel === null ? t(`filters.${key}`) : `${t(`filters.${key}`)}: ${valueLabel}`;
        return (
          <HStack key={key} role="listitem">
            <Button label={label} aria-label={t('filters.remove', { name: label })} size="sm" variant="secondary" endContent={<Icon icon={Icons.close} />} onClick={() => onRemove(key)} />
          </HStack>
        );
      })}
    </HStack>
  );
}
