import CIcon from '@coreui/icons-react';
import { CFormInput, CInputGroup, CInputGroupText } from '@coreui/react';
import type { InputHTMLAttributes, Ref } from 'react';

import { useFieldIds } from './fieldIds';
import { FieldLabel } from './FieldLabel';
import { FieldMessages } from './FieldMessages';

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'size' | 'value'> {
  /** CoreUI controls take a mutable value type; fields never hold arrays. */
  value?: string | number;
  label: string;
  error?: string;
  hint?: string;
  /** Icon at the start of the control (search, e-mail, password …), shown in an input group. */
  startIcon?: string[];
  /** Short unit after the control ("dk", "%"), shown in the same input group. */
  endText?: string;
  ref?: Ref<HTMLInputElement>;
}

/** Label, hint and error are linked to the input for screen readers; 16 px text avoids iOS zoom. */
export function TextField({ label, error, hint, id, className = '', ref, required, startIcon, endText, ...inputProps }: TextFieldProps) {
  const { fieldId, hintId, errorId, describedBy } = useFieldIds(id, hint, error);
  const control = (
    <CFormInput id={fieldId} ref={ref} required={required} invalid={Boolean(error)} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...inputProps} />
  );
  const messages = <FieldMessages hint={hint} hintId={hintId} error={error} errorId={errorId} />;
  return (
    <div className={`d-flex flex-column ${className}`.trim()}>
      <FieldLabel htmlFor={fieldId} required={required}>
        {label}
      </FieldLabel>
      {startIcon === undefined && endText === undefined ? (
        <>
          {control}
          {messages}
        </>
      ) : (
        // Feedback stays inside the group: Bootstrap shows it only as a sibling of the invalid control.
        <CInputGroup className={error ? 'has-validation' : undefined}>
          {startIcon !== undefined && (
            <CInputGroupText aria-hidden="true">
              <CIcon icon={startIcon} />
            </CInputGroupText>
          )}
          {control}
          {endText !== undefined && <CInputGroupText>{endText}</CInputGroupText>}
          {messages}
        </CInputGroup>
      )}
    </div>
  );
}
