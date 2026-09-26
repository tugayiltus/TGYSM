import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

import i18n from '../i18n';

import { ApiError, ERROR_CODES, NetworkError } from './errors';

const ERRORS_NAMESPACE = 'errors';
const NAMESPACE_SEPARATOR = ':';
const NETWORK_MESSAGE_KEY = 'common:network';

// Error codes arrive at runtime, so they cannot use the typed key overloads.
const translate = i18n.t.bind(i18n) as unknown as (key: string) => string;

export function translateErrorCode(code: string): string {
  const key = `${ERRORS_NAMESPACE}${NAMESPACE_SEPARATOR}${code}`;
  return i18n.exists(key) ? translate(key) : translate(`${ERRORS_NAMESPACE}${NAMESPACE_SEPARATOR}${ERROR_CODES.unexpected}`);
}

/** Form messages are either API error codes ("validation.required") or full i18n keys ("auth:changePassword.mismatch"). */
export function translateMessage(message: string): string {
  return message.includes(NAMESPACE_SEPARATOR) ? translate(message) : translateErrorCode(message);
}

export function fieldError(error?: { message?: string }): string | undefined {
  return error?.message ? translateMessage(error.message) : undefined;
}

export function describeError(error: unknown): string {
  if (error instanceof NetworkError) {
    return translate(NETWORK_MESSAGE_KEY);
  }
  return translateErrorCode(error instanceof ApiError ? error.code : ERROR_CODES.unexpected);
}

type CodeFields<T extends FieldValues> = Readonly<Partial<Record<string, Path<T>>>>;

/** True when the error is shown next to a form field instead of in the form alert. */
export function isHandledByFields<T extends FieldValues>(error: unknown, fields: readonly Path<T>[], codeFields: CodeFields<T> = {}): boolean {
  if (!(error instanceof ApiError)) {
    return false;
  }
  return codeFields[error.code] !== undefined || fields.some((field) => (error.fieldErrors[field]?.length ?? 0) > 0);
}

/** Puts server errors on form fields: per-field validation codes, plus whole-request codes mapped via codeFields. */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly Path<T>[],
  codeFields: CodeFields<T> = {},
): boolean {
  if (!(error instanceof ApiError)) {
    return false;
  }
  let applied = false;
  for (const field of fields) {
    const code = error.fieldErrors[field]?.[0];
    if (code !== undefined) {
      // Focus the first failing field so keyboard and screen-reader users land on the error.
      setError(field, { type: 'server', message: code }, { shouldFocus: !applied });
      applied = true;
    }
  }
  const mappedField = codeFields[error.code];
  if (mappedField !== undefined) {
    setError(mappedField, { type: 'server', message: error.code }, { shouldFocus: !applied });
    applied = true;
  }
  return applied;
}
