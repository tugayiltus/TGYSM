/**
 * @input Nothing; reads departments, teams and worker heartbeats.
 * @output The setup checks: departments without an approver, a missing intake team and background job problems.
 * @position `/admin`, the admin landing page.
 */
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Link } from '@astryxdesign/core/Link';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { LoadingState } from '../../../shared/feedback/States';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { SectionCard } from '../../../shared/page/SectionCard';
import { ROUTES } from '../../../shared/routes';
import { Icons } from '../../../shared/ui/icons';
import { useDepartments } from '../departments/api';
import { useWorkerHeartbeats } from '../system/api';
import { useTeams } from '../teams/api';

/** Large enough to see every department and team of one company on a single page. */
const OVERVIEW_PAGE_SIZE = 100;
const HEALTHY_STATUS = 'ok';

/** `/admin` landing page: setup gaps found through existing admin endpoints (no new API). */
/** Number of setup checks: departments' approvers, the intake team and the background jobs. */
const CHECK_COUNT = 3;

/** One setup check: a warning (or, when everything is set up, a success) icon before its text and link. */
function CheckItem({ ok = false, title, children }: { ok?: boolean; title: string; children?: ReactNode }) {
  return (
    <ListItem
      startContent={<Icon icon={ok ? Icons.checkCircle : Icons.warning} size="md" color={ok ? 'success' : 'warning'} />}
      label={<Text type="label">{title}</Text>}
      description={children}
    />
  );
}

export function AdminOverviewPage() {
  const { t } = useTranslation('admin');
  const departments = useDepartments({ search: '', isActive: true, page: 1, pageSize: OVERVIEW_PAGE_SIZE });
  const teams = useTeams({ search: '', page: 1, pageSize: OVERVIEW_PAGE_SIZE });
  const workers = useWorkerHeartbeats();

  if (departments.isPending || teams.isPending || workers.isPending) {
    return (
      <VStack gap={4}>
        <PageHeader icon={ROUTE_META.adminOverview.icon} />
        <SectionCard title={t('overview.checksTitle')}>
          <LoadingState rows={CHECK_COUNT} />
        </SectionCard>
      </VStack>
    );
  }
  const withoutApprover = departments.data?.items.filter((d) => d.approverUserId === null) ?? [];
  const noIntake = teams.data !== undefined && !teams.data.items.some((team) => team.isActive && team.isDefaultIntake);
  const workerIssue = workers.data !== undefined && (workers.data.status !== HEALTHY_STATUS || workers.data.jobs.some((job) => job.stale || job.lastError !== null));
  const allGood = withoutApprover.length === 0 && !noIntake && !workerIssue;

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminOverview.icon} />
      <SectionCard title={t('overview.checksTitle')}>
        <List hasDividers aria-label={t('overview.checksTitle')}>
          {allGood && <CheckItem ok title={t('overview.allGood')} />}
          {withoutApprover.length > 0 && (
            <CheckItem title={t('overview.missingApprovers')}>
              <HStack gap={3} wrap="wrap">
                {withoutApprover.map((department) => (
                  <Link key={department.id} href={ROUTES.adminDepartments}>
                    {department.name}
                  </Link>
                ))}
              </HStack>
            </CheckItem>
          )}
          {noIntake && (
            <CheckItem title={t('overview.noIntake')}>
              <Link href={ROUTES.adminTeams}>{t('overview.goToTeams')}</Link>
            </CheckItem>
          )}
          {workerIssue && (
            <CheckItem title={t('overview.workerIssue')}>
              <Link href={ROUTES.adminSystem}>{t('overview.goToSystem')}</Link>
            </CheckItem>
          )}
        </List>
      </SectionCard>
    </VStack>
  );
}
