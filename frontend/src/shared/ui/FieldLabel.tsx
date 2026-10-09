import { CFormLabel } from '@coreui/react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/** One label style for every field (spec §3); a required field shows "*" and says "(zorunlu)" to screen readers. */
export function FieldLabel({ htmlFor, required = false, children }: { htmlFor: string; required?: boolean; children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <CFormLabel htmlFor={htmlFor} className="form-label fw-medium small mb-1">
      {children}
      {required && (
        <>
          <span className="text-danger ms-1" aria-hidden="true">
            *
          </span>
          <span className="visually-hidden">{t('field.required')}</span>
        </>
      )}
    </CFormLabel>
  );
}
