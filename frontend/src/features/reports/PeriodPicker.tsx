/**
 * @input The current period, today and a change callback.
 * @output Quick period buttons (a selector on phones, where five buttons do not fit); the matching one is chosen.
 * @position Report filters; other ranges are set with the day fields.
 */
import { Selector } from '@astryxdesign/core/Selector';
import { ToggleButton, ToggleButtonGroup } from '@astryxdesign/core/ToggleButton';
import { useTranslation } from 'react-i18next';

import { useIsPhone } from '../../shared/list/useIsPhone';

import { matchPreset, PERIOD_PRESETS, presetPeriod, type Period } from './period';

type Preset = (typeof PERIOD_PRESETS)[number];

const isPreset = (value: unknown): value is Preset => typeof value === 'string' && (PERIOD_PRESETS as readonly string[]).includes(value);

export function PeriodPicker({ period, today, onChange }: { period: Partial<Period>; today: string; onChange: (period: Period) => void }) {
  const { t } = useTranslation('reports');
  const isPhone = useIsPhone();
  const active = matchPreset(period, today) ?? null;
  const choose = (value: unknown) => isPreset(value) && onChange(presetPeriod(value, today));
  if (isPhone) {
    // A custom range matches no preset; the selector then shows its placeholder.
    return (
      <Selector
        label={t('period.label')}
        placeholder={t('period.custom')}
        value={active ?? ''}
        options={PERIOD_PRESETS.map((preset) => ({ value: preset, label: t(`period.${preset}`) }))}
        onChange={choose}
      />
    );
  }
  return (
    // Pressing the active preset again would leave no choice; only a new preset changes the period.
    <ToggleButtonGroup label={t('period.label')} value={active} onChange={choose}>
      {PERIOD_PRESETS.map((preset) => (
        <ToggleButton key={preset} value={preset} label={t(`period.${preset}`)} size="sm" />
      ))}
    </ToggleButtonGroup>
  );
}
