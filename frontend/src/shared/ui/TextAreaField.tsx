import { CFormLabel, CFormTextarea } from '@coreui/react';
import type { Ref, TextareaHTMLAttributes } from 'react';

import { useFieldIds } from './fieldIds';
import { FieldMessages } from './FieldMessages';

export interface TextAreaFieldProps extends Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'value'> {
  value?: string | number;
  label: string;
  error?: string;
  hint?: string;
  ref?: Ref<HTMLTextAreaElement>;
}

const DEFAULT_ROWS = 4;

/** Multi-line twin of TextField: label, hint and error linked for screen readers. */
export function TextAreaField({ label, error, hint, id, className = '', ref, rows = DEFAULT_ROWS, ...props }: TextAreaFieldProps) {
  const { fieldId, hintId, errorId, describedBy } = useFieldIds(id, hint, error);
  return (
    <div className={`d-flex flex-column ${className}`.trim()}>
      <CFormLabel htmlFor={fieldId}>{label}</CFormLabel>
      <CFormTextarea id={fieldId} ref={ref} rows={rows} invalid={Boolean(error)} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...props} />
      <FieldMessages hint={hint} hintId={hintId} error={error} errorId={errorId} />
    </div>
  );
}
