/**
 * @input A change's working summary (number, title, status, window, risk and the plan the viewer's tasks need).
 * @output Read-only text: no actions, approvals, history or other teams' tasks.
 * @position Change detail page when the viewer only has summary access (ADR-0031).
 */
import { Banner } from '@astryxdesign/core/Banner';
import { HStack } from '@astryxdesign/core/HStack';
import { MetadataList } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { SectionCard } from '../../shared/page/SectionCard';

import type { ChangeSummary } from './api';
import { ChangeStatusBadge, RiskBadge } from './ChangeBadges';
import { ChangeTicketLinks } from './ChangeTicketLinks';
import { Field, NodeField } from './Field';
import { WindowText } from './WindowText';

export function ChangeSummaryView({ summary }: { summary: ChangeSummary }) {
  const { t } = useTranslation('changes');
  return (
    <VStack gap={4}>
      <HStack gap={2} vAlign="center" wrap="wrap">
        <Text type="supporting">{summary.number}</Text>
        <ChangeStatusBadge status={summary.status} />
        <RiskBadge risk={summary.risk} />
      </HStack>
      <Banner status="info" title={t('summary.banner')} description={t('summary.bannerDescription')} />
      <SectionCard title={t('detail.summarySection')}>
        <MetadataList label={{ position: 'top' }}>
          <NodeField label={t('detail.window')}>
            <WindowText start={summary.windowStart} end={summary.windowEnd} />
          </NodeField>
          {summary.implementationPlan !== null && <Field label={t('summary.implementationPlan')} value={summary.implementationPlan} />}
          {summary.backoutPlan !== null && <Field label={t('summary.backoutPlan')} value={summary.backoutPlan} />}
        </MetadataList>
      </SectionCard>
      <ChangeTicketLinks number={summary.number} status={summary.status} canEdit={false} />
    </VStack>
  );
}
