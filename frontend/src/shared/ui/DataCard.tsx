import { CCard, CCardBody, CCardFooter, CCardHeader } from '@coreui/react';
import type { ReactNode } from 'react';

export interface DataCardProps {
  filters?: ReactNode;
  footer?: ReactNode;
  children: ReactNode;
}

/** List page card (spec §3): filters on top, the list edge to edge, paging below. */
export function DataCard({ filters, footer, children }: DataCardProps) {
  return (
    <CCard className="shadow-sm">
      {filters !== undefined && <CCardHeader className="bg-body py-3">{filters}</CCardHeader>}
      <CCardBody className="p-0 tgysm-card-body-min">{children}</CCardBody>
      {footer !== undefined && <CCardFooter className="bg-body">{footer}</CCardFooter>}
    </CCard>
  );
}
