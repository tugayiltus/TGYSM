import CIcon from '@coreui/icons-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { ROUTES } from '../../../shared/routes';
import { cilCheckCircle, cilWarning } from '../../../shared/ui/icons';
import { PageCard } from '../../../shared/ui/PageCard';
import { LoadingState } from '../../../shared/ui/States';
import { useDepartments } from '../departments/api';
import { useWorkerHeartbeats } from '../system/api';
import { useTeams } from '../teams/api';

/** Large enough to see every department and team of one company on a single page. */
const OVERVIEW_PAGE_SIZE = 100;
const HEALTHY_STATUS = 'ok';

/** `/admin` landing page: setup gaps found through existing admin endpoints (no new API). */
/** Number of setup checks: departments' approvers, the intake team and the background jobs. */
const CHECK_COUNT = 3;

/** One setup check: a warning (or, when everything is set up, a success) icon beside its text and link. */
function CheckItem({ ok = false, children }: { ok?: boolean; children: ReactNode }) {
  const color = ok ? 'success' : 'warning';
  return (
    <li className="list-group-item py-3 d-flex align-items-start gap-3">
      <span className={`d-inline-flex align-items-center justify-content-center rounded-3 flex-shrink-0 tgysm-kpi-icon bg-${color}-subtle text-${color}-emphasis`} aria-hidden="true">
        <CIcon icon={ok ? cilCheckCircle : cilWarning} size="xl" />
      </span>
      <div className="d-flex flex-column flex-sm-row flex-wrap justify-content-between gap-2 flex-grow-1 min-w-0 align-self-center">{children}</div>
    </li>
  );
}

export function AdminOverviewPage() {
  const { t } = useTranslation('admin');
  const departments = useDepartments({ search: '', isActive: true, page: 1, pageSize: OVERVIEW_PAGE_SIZE });
  const teams = useTeams({ search: '', page: 1, pageSize: OVERVIEW_PAGE_SIZE });
  const workers = useWorkerHeartbeats();

  if (departments.isPending || teams.isPending || workers.isPending) {
    return (
      <>
        <PageHeader icon={ROUTE_META.adminOverview.icon} />
        <PageCard title={t('overview.checksTitle')} flush>
          <LoadingState rows={CHECK_COUNT} />
        </PageCard>
      </>
    );
  }
  const withoutApprover = departments.data?.items.filter((d) => d.approverUserId === null) ?? [];
  const noIntake = teams.data !== undefined && !teams.data.items.some((team) => team.isActive && team.isDefaultIntake);
  const workerIssue = workers.data !== undefined && (workers.data.status !== HEALTHY_STATUS || workers.data.jobs.some((job) => job.stale || job.lastError !== null));
  const allGood = withoutApprover.length === 0 && !noIntake && !workerIssue;

  return (
    <>
      <PageHeader icon={ROUTE_META.adminOverview.icon} />
      <PageCard title={t('overview.checksTitle')} flush>
        <ul className="list-group list-group-flush">
          {allGood && (
            <CheckItem ok>
              <p className="mb-0">{t('overview.allGood')}</p>
            </CheckItem>
          )}
          {withoutApprover.length > 0 && (
            <CheckItem>
              <p className="mb-1 fw-semibold">{t('overview.missingApprovers')}</p>
              <ul className="list-unstyled mb-0 d-flex flex-wrap gap-3">
                {withoutApprover.map((department) => (
                  <li key={department.id}>
                    <Link to={ROUTES.adminDepartments}>{department.name}</Link>
                  </li>
                ))}
              </ul>
            </CheckItem>
          )}
          {noIntake && (
            <CheckItem>
              <p className="mb-0">{t('overview.noIntake')}</p>
              <Link to={ROUTES.adminTeams}>{t('overview.goToTeams')}</Link>
            </CheckItem>
          )}
          {workerIssue && (
            <CheckItem>
              <p className="mb-0">{t('overview.workerIssue')}</p>
              <Link to={ROUTES.adminSystem}>{t('overview.goToSystem')}</Link>
            </CheckItem>
          )}
        </ul>
      </PageCard>
    </>
  );
}
