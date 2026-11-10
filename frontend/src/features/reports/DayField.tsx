/**
 * @input Label, allowed range, the day in the URL, a validity check and a change callback.
 * @output An Astryx date input that writes only a valid day, or an empty value, to the URL.
 * @position Report filter bar (from / to).
 */
import { DateInput } from '@astryxdesign/core/DateInput';
import type { ComponentProps } from 'react';

type IsoDay = ComponentProps<typeof DateInput>['value'];

export interface DayFieldProps {
  label: string;
  min: string;
  max: string;
  /** The day in the URL, or '' when none is set. */
  value: string;
  isValid: (value: string) => boolean;
  /** Called with a valid day, or '' when the field is emptied. */
  onChange: (value: string) => void;
}

/**
 * The URL holds YYYY-MM-DD days, which is Astryx's ISO date form. Astryx keeps a half-typed date inside the input.
 * No clear button: Temizle on the filter bar resets the period.
 */
const asIsoDay = (day: string): IsoDay => (day === '' ? undefined : (day as IsoDay));

export function DayField({ label, min, max, value, isValid, onChange }: DayFieldProps) {
  return (
    <DateInput
      label={label}
      min={asIsoDay(min)}
      max={asIsoDay(max)}
      value={asIsoDay(value)}
      onChange={(next) => {
        if (next === undefined) {
          onChange('');
        } else if (isValid(next)) {
          onChange(next);
        }
      }}
    />
  );
}
