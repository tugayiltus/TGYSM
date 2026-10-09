import CIcon from '@coreui/icons-react';
import { CCard, CCardBody } from '@coreui/react';
import type { PropsWithChildren } from 'react';
import { useTranslation } from 'react-i18next';

import { cilDevices } from '../ui/icons';

/** Centred card for screens outside the app (sign-in, password links, forced change, 404): no menu, no breadcrumb. */
export function AuthCard({ children }: PropsWithChildren) {
  const { t } = useTranslation();
  return (
    <main className="min-vh-100 d-flex flex-column align-items-center justify-content-center gap-3 p-3 bg-body-tertiary">
      <p className="tgysm-auth-brand d-flex align-items-center gap-2 mb-0 fs-4 fw-semibold text-primary">
        <CIcon icon={cilDevices} size="xxl" aria-hidden="true" />
        {t('appName')}
      </p>
      <CCard className="w-100 shadow tgysm-auth-card">
        <CCardBody className="p-4 p-sm-5 d-flex flex-column gap-3">{children}</CCardBody>
      </CCard>
    </main>
  );
}
