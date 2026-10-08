import { useState } from 'react';

import { TextField } from '../../shared/ui/TextField';

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

/** What the user typed, and the URL value it was typed against. */
interface Draft {
  text: string;
  source: string;
}

/**
 * A date input that keeps half-typed days (a browser reports "0002-05-10" while the year is typed) in a local draft and
 * writes only a valid day or an empty field to the URL. When the URL value changes elsewhere (clear, navigation), the
 * draft no longer matches its source and the URL value is shown again.
 */
export function DayField({ label, min, max, value, isValid, onChange }: DayFieldProps) {
  const [draft, setDraft] = useState<Draft>({ text: value, source: value });
  const shown = draft.source === value ? draft.text : value;

  return (
    <TextField
      type="date"
      label={label}
      min={min}
      max={max}
      value={shown}
      onChange={(event) => {
        const text = event.target.value;
        const writes = text === '' || isValid(text);
        setDraft({ text, source: writes ? text : value });
        if (writes) {
          onChange(text);
        }
      }}
    />
  );
}
