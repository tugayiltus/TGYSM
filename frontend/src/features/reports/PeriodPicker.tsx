import { useTranslation } from 'react-i18next';

import { matchPreset, PERIOD_PRESETS, presetPeriod, type Period } from './period';

/** Quick periods of a report; the active one is pressed. Other ranges are set with the day fields of the filters. */
export function PeriodPicker({ period, today, onChange }: { period: Partial<Period>; today: string; onChange: (period: Period) => void }) {
  const { t } = useTranslation('reports');
  const active = matchPreset(period, today);
  return (
    <div role="group" aria-label={t('period.label')} className="d-flex flex-wrap gap-2">
      {PERIOD_PRESETS.map((preset) => (
        <button
          key={preset}
          type="button"
          aria-pressed={active === preset}
          className={`btn btn-sm tgysm-touch ${active === preset ? 'btn-primary' : 'btn-outline-primary'}`}
          onClick={() => onChange(presetPeriod(preset, today))}
        >
          {t(`period.${preset}`)}
        </button>
      ))}
    </div>
  );
}
