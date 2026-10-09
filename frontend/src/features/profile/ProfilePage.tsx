import CIcon from '@coreui/icons-react';
import { CAvatar, CListGroup, CListGroupItem } from '@coreui/react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTES } from '../../shared/routes';
import { cilLockLocked, cilUser } from '../../shared/ui/icons';
import { PageCard } from '../../shared/ui/PageCard';
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
        icon={cilUser}
        actions={
          <Link to={ROUTES.changePassword} className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center gap-2">
            <CIcon icon={cilLockLocked} aria-hidden="true" />
            {t('profile.changePassword')}
          </Link>
        }
      />
      <PageCard flush className="tgysm-form-card">
        <div className="d-flex align-items-center gap-3 p-3 border-bottom">
          <CAvatar color="primary" textColor="white" size="xl" aria-hidden="true">
            {`${me.firstName.charAt(0)}${me.lastName.charAt(0)}`}
          </CAvatar>
          <div className="min-w-0">
            <p className="mb-0 fw-semibold text-break">{`${me.firstName} ${me.lastName}`}</p>
            <p className="mb-0 small text-body-secondary text-break">{tc(`roles.${me.role}`)}</p>
          </div>
        </div>
        <CListGroup flush as="dl" className="mb-0">
          {rows.map((row) => (
            <CListGroupItem key={row.label} className="d-flex flex-column flex-sm-row justify-content-between gap-1">
              <dt className="fw-normal text-body-secondary">{row.label}</dt>
              <dd className="mb-0 fw-semibold text-break">{row.value}</dd>
            </CListGroupItem>
          ))}
        </CListGroup>
      </PageCard>
    </>
  );
}
