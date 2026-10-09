import { CCardBody, CCardFooter, CCardHeader } from '@coreui/react';
import { useId, type ReactNode } from 'react';

export interface FormSectionProps {
  title?: string;
  actions?: ReactNode;
  className?: string;
  children: ReactNode;
}

/** A titled group of form fields in a card; fields are grid columns (`col-12 col-sm-6` …), actions right-aligned below. */
export function FormSection({ title, actions, className = '', children }: FormSectionProps) {
  const titleId = useId();
  return (
    // A native section: CCard has no `as`, and a titled section is the region landmark screen readers list.
    <section aria-labelledby={title === undefined ? undefined : titleId} className={`card shadow-sm ${className}`.trim()}>
      {title !== undefined && (
        <CCardHeader className="bg-body">
          <h2 id={titleId} className="h6 mb-0 fw-semibold">
            {title}
          </h2>
        </CCardHeader>
      )}
      <CCardBody>
        <div className="row g-3">{children}</div>
      </CCardBody>
      {actions !== undefined && <CCardFooter className="bg-body d-flex flex-wrap gap-2 justify-content-end">{actions}</CCardFooter>}
    </section>
  );
}
