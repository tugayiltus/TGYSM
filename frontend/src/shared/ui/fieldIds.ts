import { useId } from 'react';

/** Ids for a form control and its hint/error, and the aria-describedby that links them. */
export function useFieldIds(id: string | undefined, hint: string | undefined, error: string | undefined) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;
  const hintId = hint ? `${fieldId}-hint` : undefined;
  const errorId = error ? `${fieldId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;
  return { fieldId, hintId, errorId, describedBy };
}
