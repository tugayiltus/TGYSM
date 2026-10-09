import { CCardBody, CCardFooter, CCardHeader } from '@coreui/react';
import { useId, type ReactNode } from 'react';

export interface PageCardProps {
  title?: string;
  actions?: ReactNode;
  footer?: ReactNode;
  /** Body without padding: tables and lists that run edge to edge. */
  flush?: boolean;
  className?: string;
  bodyClassName?: string;
  children: ReactNode;
}

/** The one container for page content (spec §3): nothing sits directly on the page background. */
export function PageCard({ title, actions, footer, flush = false, className = '', bodyClassName = '', children }: PageCardProps) {
  const titleId = useId();
  const bodyClasses = `${flush ? 'p-0' : ''} ${bodyClassName}`.trim();
  return (
    // A native section: CCard has no `as`, and a titled section is the region landmark screen readers list.
    <section aria-labelledby={title === undefined ? undefined : titleId} className={`card shadow-sm ${className}`.trim()}>
      {(title !== undefined || actions !== undefined) && (
        <CCardHeader className="d-flex flex-wrap align-items-center justify-content-between gap-2 bg-body">
          {title !== undefined && (
            <h2 id={titleId} className="h6 mb-0 fw-semibold">
              {title}
            </h2>
          )}
          {actions !== undefined && <div className="d-flex flex-wrap gap-2 ms-auto">{actions}</div>}
        </CCardHeader>
      )}
      <CCardBody className={bodyClasses === '' ? undefined : bodyClasses}>{children}</CCardBody>
      {footer !== undefined && <CCardFooter className="bg-body">{footer}</CCardFooter>}
    </section>
  );
}
