/**
 * @input The change number from the route.
 * @output The change: summary, plan, assets, tasks by phase, approval, history and actions; or the read-only working summary.
 * @position `/changes/:number`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { MetadataList } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useLocation, useParams } from 'react-router';

import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { useIsPhone } from '../../shared/list/useIsPhone';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState, useReturnTo } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { assetPath, ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { NotFoundPage } from '../../shared/ui/NotFoundPage';

import { useChange, type ChangeDetail } from './api';
import { ChangeActions } from './ChangeActions';
import { ChangeApprovalBox } from './ChangeApprovalBox';
import { ChangeOutcomeBadge, ChangeStatusBadge, RiskBadge } from './ChangeBadges';
import { ChangeConflictBanner } from './ChangeConflictBanner';
import { ChangeSummaryView } from './ChangeSummaryView';
import { ChangeTasks } from './ChangeTasks';
import { ChangeTicketLinks } from './ChangeTicketLinks';
import { ChangeTimeline } from './ChangeTimeline';
import { Field, NodeField } from './Field';
import { WindowText } from './WindowText';

/** Wide screens: content takes two of three columns, the side column one. */
const WIDE_COLUMNS = 3;
const MAIN_SPAN = 2;
const META_SEPARATOR = ' · ';

const dateTimeOrNull = (iso: string | null) => (iso === null ? null : formatDateTime(iso));

function AssetsList({ change }: { change: ChangeDetail }) {
  const { t } = useTranslation('changes');
  const location = useLocation();
  if (change.assets.length === 0) {
    return <Text type="supporting">{t('detail.noAssets')}</Text>;
  }

  return (
    <List hasDividers density="compact" aria-label={t('detail.assetsSection')}>
      {change.assets.map((asset) => (
        <ListItem
          key={asset.id}
          label={
            <StateLink href={assetPath(asset.id)} state={linkState(location)}>
              {[asset.assetTag, `${asset.brand} ${asset.model}`].join(META_SEPARATOR)}
            </StateLink>
          }
        />
      ))}
    </List>
  );
}

function FullView({ change }: { change: ChangeDetail }) {
  const { t } = useTranslation('changes');
  const isPhone = useIsPhone();
  const side = (
    <VStack gap={4}>
      <ChangeActions change={change} />
      <SectionCard title={t('detail.sideSection')}>
        <MetadataList>
          <Field label={t('detail.team')} value={change.ownerTeamName} />
          <Field label={t('detail.owner')} value={change.ownerName} />
          <Field label={t('detail.createdBy')} value={change.createdByName} />
          <Field label={t('detail.createdAt')} value={formatDateTime(change.createdAt)} />
          <Field label={t('detail.actualStart')} value={dateTimeOrNull(change.actualStart)} />
          <Field label={t('detail.backoutStartedAt')} value={dateTimeOrNull(change.backoutStartedAt)} />
          <Field label={t('detail.actualEnd')} value={dateTimeOrNull(change.actualEnd)} />
        </MetadataList>
      </SectionCard>
    </VStack>
  );
  const main = (
    <VStack gap={4}>
      <SectionCard title={t('detail.summarySection')}>
        <MetadataList label={{ position: 'top' }}>
          <Field label={t('detail.justification')} value={change.justification} />
          <Field label={t('detail.affectedService')} value={change.affectedService} />
          <NodeField label={t('detail.risk')}>
            <RiskBadge risk={change.risk} />
          </NodeField>
          <Field label={t('detail.impact')} value={t(`levels.${change.impact}`)} />
          {change.verificationNote && <Field label={t('detail.verificationNote')} value={change.verificationNote} />}
          {change.backoutNote && <Field label={t('detail.backoutNote')} value={change.backoutNote} />}
          {change.closingNote && <Field label={t('detail.closingNote')} value={change.closingNote} />}
          {change.cancelReason && <Field label={t('detail.cancelReason')} value={change.cancelReason} />}
        </MetadataList>
      </SectionCard>
      <ChangeConflictBanner conflicts={change.conflicts} />
      <SectionCard title={t('detail.planSection')}>
        <MetadataList label={{ position: 'top' }}>
          <NodeField label={t('detail.window')}>
            <WindowText start={change.windowStart} end={change.windowEnd} />
          </NodeField>
          <Field label={t('detail.implementationPlan')} value={change.implementationPlan} />
          <Field label={t('detail.verificationPlan')} value={change.verificationPlan} />
          <Field label={t('detail.backoutPlan')} value={change.backoutPlan} />
        </MetadataList>
      </SectionCard>
      <SectionCard title={t('detail.assetsSection')}>
        <AssetsList change={change} />
      </SectionCard>
      <ChangeTicketLinks number={change.number} status={change.status} canEdit={change.canWork} />
      <SectionCard title={t('detail.tasksSection')}>
        <ChangeTasks change={change} />
      </SectionCard>
      <SectionCard title={t('detail.approvalSection')}>
        <ChangeApprovalBox change={change} />
      </SectionCard>
      <SectionCard title={t('detail.timelineSection')}>
        <ChangeTimeline number={change.number} />
      </SectionCard>
    </VStack>
  );
  return (
    <VStack gap={4}>
      <HStack gap={2} vAlign="center" wrap="wrap">
        <Text type="supporting">{change.number}</Text>
        <ChangeStatusBadge status={change.status} />
        <ChangeOutcomeBadge outcome={change.outcome} />
        <RiskBadge risk={change.risk} />
      </HStack>
      {/* Phones show the actions before the long content; wide screens put them in a side column. */}
      {isPhone ? (
        <VStack gap={4}>
          {side}
          {main}
        </VStack>
      ) : (
        <Grid columns={WIDE_COLUMNS} gap={4} align="start">
          <GridSpan columns={MAIN_SPAN}>{main}</GridSpan>
          {side}
        </Grid>
      )}
    </VStack>
  );
}

/** Keyed by number: switching changes starts with fresh dialogs and errors. */
export function ChangeDetailPage() {
  const { number = '' } = useParams();
  return <ChangeDetailView key={number} number={number} />;
}

function ChangeDetailView({ number }: { number: string }) {
  const { t } = useTranslation('changes');
  const change = useChange(number);
  const backTo = useReturnTo(ROUTES.changes);
  // Only from loaded data: a 404 must not show the number typed into the URL as a title.
  const loaded = change.data?.detail ?? change.data?.summary ?? undefined;
  useBreadcrumbLabel('changeDetail', loaded?.number);
  if (change.isPending) {
    return <LoadingState />;
  }

  if (change.isError) {
    return <ErrorState error={change.error} onRetry={() => void change.refetch()} />;
  }

  const { detail, summary } = change.data;
  const title = detail?.title ?? summary?.title;
  if (title === undefined) {
    return <NotFoundPage />;
  }

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.changes.icon}
        titleOverride={title}
        actions={<Button label={t('detail.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />}
      />
      {detail !== null && change.data.access === 'full' ? <FullView change={detail} /> : summary !== null && <ChangeSummaryView summary={summary} />}
    </VStack>
  );
}
