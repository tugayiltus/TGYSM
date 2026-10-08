import { CCard, CCardBody } from '@coreui/react';
import type { PropsWithChildren } from 'react';

/** Centred card for screens outside the app (sign-in, password links, forced change, 404): no menu, no breadcrumb. */
export function AuthCard({ children }: PropsWithChildren) {
  return (
    <main className="min-vh-100 d-flex align-items-center justify-content-center p-3 bg-body-tertiary">
      <CCard className="w-100 shadow-sm tgysm-auth-card">
        <CCardBody className="p-4 d-flex flex-column gap-3">{children}</CCardBody>
      </CCard>
    </main>
  );
}
