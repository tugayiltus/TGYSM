import { CFormSelect } from '@coreui/react';
import type { Ref, SelectHTMLAttributes } from 'react';

import { useFieldIds } from './fieldIds';
import { FieldLabel } from './FieldLabel';
import { FieldMessages } from './FieldMessages';

export interface SelectOption {
  value: string;
  label: string;
}

export interface SelectFieldProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, 'size' | 'value'> {
  value?: string | number;
  label: string;
  options: readonly SelectOption[];
  error?: string;
  hint?: string;
  /** Adds an empty first option with this label (e.g. "none"). */
  placeholder?: string;
  ref?: Ref<HTMLSelectElement>;
}

export function SelectField({ label, required, options, error, hint, placeholder, id, className = '', ref, ...selectProps }: SelectFieldProps) {
  const { fieldId, hintId, errorId, describedBy } = useFieldIds(id, hint, error);
  return (
    <div className={`d-flex flex-column ${className}`.trim()}>
      <FieldLabel htmlFor={fieldId} required={required}>
        {label}
      </FieldLabel>
      <CFormSelect id={fieldId} ref={ref} required={required} invalid={Boolean(error)} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...selectProps}>
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </CFormSelect>
      <FieldMessages hint={hint} hintId={hintId} error={error} errorId={errorId} />
    </div>
  );
}
