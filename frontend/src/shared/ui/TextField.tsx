import { CFormInput, CFormLabel } from '@coreui/react';
import type { InputHTMLAttributes, Ref } from 'react';

import { useFieldIds } from './fieldIds';
import { FieldMessages } from './FieldMessages';

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'size' | 'value'> {
  /** CoreUI controls take a mutable value type; fields never hold arrays. */
  value?: string | number;
  label: string;
  error?: string;
  hint?: string;
  ref?: Ref<HTMLInputElement>;
}

/** Label, hint and error are linked to the input for screen readers; 16 px text avoids iOS zoom. */
export function TextField({ label, error, hint, id, className = '', ref, ...inputProps }: TextFieldProps) {
  const { fieldId, hintId, errorId, describedBy } = useFieldIds(id, hint, error);
  return (
    <div className={`d-flex flex-column ${className}`.trim()}>
      <CFormLabel htmlFor={fieldId}>{label}</CFormLabel>
      <CFormInput id={fieldId} ref={ref} invalid={Boolean(error)} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...inputProps} />
      <FieldMessages hint={hint} hintId={hintId} error={error} errorId={errorId} />
    </div>
  );
}
