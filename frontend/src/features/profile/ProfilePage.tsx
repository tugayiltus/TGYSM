import { CCard, CListGroup, CListGroupItem } from '@coreui/react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTES } from '../../shared/routes';
import { useSession } from '../auth/session';

export function ProfilePage() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const me = useSession();
  const rows = [
    { label: t('profile.name'), value: `${me.firstName} ${me.lastName}` },
    { label: t('profile.email'), value: me.email },
    { label: t('profile.role'), value: tc(`roles.${me.role}`) },
    { label: t('profile.department'), value: me.departmentName ?? t('profile.notSet') },
    { label: t('profile.location'), value: me.locationName ?? t('profile.notSet') },
  ];

  return (
    <>
      <PageHeader
        actions={
          <Link to={ROUTES.changePassword} className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center">
            {t('profile.changePassword')}
          </Link>
        }
      />
      <CCard className="tgysm-form-card">
        <CListGroup flush as="dl" className="mb-0">
          {rows.map((row) => (
            <CListGroupItem key={row.label} className="d-flex flex-column flex-sm-row justify-content-between gap-1">
              <dt className="fw-normal text-body-secondary">{row.label}</dt>
              <dd className="mb-0 fw-semibold text-break">{row.value}</dd>
            </CListGroupItem>
          ))}
        </CListGroup>
      </CCard>
    </>
  );
}
