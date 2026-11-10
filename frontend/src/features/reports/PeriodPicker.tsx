/**
 * @input The current period, today and a change callback.
 * @output Quick period buttons; the one matching the period is pressed.
 * @position Report filters; other ranges are set with the day fields.
 */
import { ToggleButton, ToggleButtonGroup } from '@astryxdesign/core/ToggleButton';
import { useTranslation } from 'react-i18next';

import { matchPreset, PERIOD_PRESETS, presetPeriod, type Period } from './period';

type Preset = (typeof PERIOD_PRESETS)[number];

const isPreset = (value: unknown): value is Preset => typeof value === 'string' && (PERIOD_PRESETS as readonly string[]).includes(value);

export function PeriodPicker({ period, today, onChange }: { period: Partial<Period>; today: string; onChange: (period: Period) => void }) {
  const { t } = useTranslation('reports');
  const active = matchPreset(period, today) ?? null;
  return (
    // Pressing the active preset again would leave no choice; only a new preset changes the period.
    <ToggleButtonGroup label={t('period.label')} value={active} onChange={(value) => isPreset(value) && onChange(presetPeriod(value, today))}>
      {PERIOD_PRESETS.map((preset) => (
        <ToggleButton key={preset} value={preset} label={t(`period.${preset}`)} size="sm" />
      ))}
    </ToggleButtonGroup>
  );
}
