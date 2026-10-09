import { CFormCheck, CFormSwitch } from '@coreui/react';
import type { InputHTMLAttributes, Ref } from 'react';

import { useFieldIds } from './fieldIds';
import { FieldMessages } from './FieldMessages';

export interface CheckFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'size'> {
  label: string;
  type?: 'checkbox' | 'radio' | 'switch';
  error?: string;
  hint?: string;
  ref?: Ref<HTMLInputElement>;
}

/** Checkbox, radio or switch with a linked label and messages; the 44 px row keeps it easy to tap. */
export function CheckField({ label, type = 'checkbox', error, hint, id, className = '', ref, ...inputProps }: CheckFieldProps) {
  const { fieldId, hintId, errorId, describedBy } = useFieldIds(id, hint, error);
  const common = { id: fieldId, ref, label, invalid: Boolean(error), 'aria-invalid': error ? true : undefined, 'aria-describedby': describedBy, ...inputProps };
  return (
    <div className={`d-flex flex-column justify-content-center tgysm-touch ${className}`.trim()}>
      {type === 'switch' ? <CFormSwitch role="switch" {...common} /> : <CFormCheck type={type} {...common} />}
      <FieldMessages hint={hint} hintId={hintId} error={error} errorId={errorId} />
    </div>
  );
}
