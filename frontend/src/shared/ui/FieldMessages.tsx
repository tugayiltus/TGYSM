import { CFormFeedback, CFormText } from '@coreui/react';

/** Hint and error under a form control; the error uses CoreUI's invalid feedback (shown next to `.is-invalid`). */
export function FieldMessages({ hint, hintId, error, errorId }: { hint?: string; hintId?: string; error?: string; errorId?: string }) {
  return (
    <>
      {hint && <CFormText id={hintId}>{hint}</CFormText>}
      {error && (
        <CFormFeedback invalid id={errorId}>
          {error}
        </CFormFeedback>
      )}
    </>
  );
}
