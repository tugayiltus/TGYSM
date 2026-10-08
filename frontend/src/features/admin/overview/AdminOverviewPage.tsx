import { CCard, CCardBody } from '@coreui/react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTES } from '../../../shared/routes';
import { LoadingState } from '../../../shared/ui/States';
import { useDepartments } from '../departments/api';
import { useWorkerHeartbeats } from '../system/api';
import { useTeams } from '../teams/api';

/** Large enough to see every department and team of one company on a single page. */
const OVERVIEW_PAGE_SIZE = 100;
const HEALTHY_STATUS = 'ok';

/** `/admin` landing page: setup gaps found through existing admin endpoints (no new API). */
export function AdminOverviewPage() {
  const { t } = useTranslation('admin');
  const departments = useDepartments({ search: '', isActive: true, page: 1, pageSize: OVERVIEW_PAGE_SIZE });
  const teams = useTeams({ search: '', page: 1, pageSize: OVERVIEW_PAGE_SIZE });
  const workers = useWorkerHeartbeats();

  if (departments.isPending || teams.isPending || workers.isPending) {
    return (
      <>
        <PageHeader />
        <LoadingState />
      </>
    );
  }
  const withoutApprover = departments.data?.items.filter((d) => d.approverUserId === null) ?? [];
  const noIntake = teams.data !== undefined && !teams.data.items.some((team) => team.isActive && team.isDefaultIntake);
  const workerIssue = workers.data !== undefined && (workers.data.status !== HEALTHY_STATUS || workers.data.jobs.some((job) => job.stale || job.lastError !== null));
  const allGood = withoutApprover.length === 0 && !noIntake && !workerIssue;

  return (
    <>
      <PageHeader />
      <div className="d-flex flex-column gap-2">
        {allGood && <p className="mb-0">{t('overview.allGood')}</p>}
        {withoutApprover.length > 0 && (
          <CCard>
            <CCardBody>
              <h2 className="h6">{t('overview.missingApprovers')}</h2>
              <ul className="list-unstyled mb-0 mb-0">
                {withoutApprover.map((department) => (
                  <li key={department.id}>
                    <Link to={ROUTES.adminDepartments}>{department.name}</Link>
                  </li>
                ))}
              </ul>
            </CCardBody>
          </CCard>
        )}
        {noIntake && (
          <CCard>
            <CCardBody className="d-flex flex-wrap justify-content-between gap-2">
              <p className="mb-0">{t('overview.noIntake')}</p>
              <Link to={ROUTES.adminTeams}>{t('overview.goToTeams')}</Link>
            </CCardBody>
          </CCard>
        )}
        {workerIssue && (
          <CCard>
            <CCardBody className="d-flex flex-wrap justify-content-between gap-2">
              <p className="mb-0">{t('overview.workerIssue')}</p>
              <Link to={ROUTES.adminSystem}>{t('overview.goToSystem')}</Link>
            </CCardBody>
          </CCard>
        )}
      </div>
    </>
  );
}
