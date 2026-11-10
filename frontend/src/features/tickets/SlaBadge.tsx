/**
 * @input One SLA timer, or all timers of a ticket.
 * @output The metric name with the remaining or overdue business time, or the final result.
 * @position Ticket lists and ticket detail.
 */
import { HStack } from '@astryxdesign/core/HStack';
import { StatusDot } from '@astryxdesign/core/StatusDot';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';

import type { TicketSla } from './api';
import { slaState, useFormatBusinessDuration, type SlaState } from './slaState';

type DotVariant = ComponentProps<typeof StatusDot>['variant'];

const STATE_DOT: Record<SlaState, DotVariant> = {
  running: 'accent',
  paused: 'neutral',
  warned: 'warning',
  breached: 'error',
  met: 'success',
  metBreached: 'error',
  cancelled: 'neutral',
};

/** `data-sla-state` lets E2E tests read the state without parsing the text. */
export function SlaBadge({ sla }: { sla: TicketSla }) {
  const { t } = useTranslation('tickets');
  const formatDuration = useFormatBusinessDuration();
  const state = slaState(sla);
  const stateText = t(`sla.states.${state}`, { duration: formatDuration(sla.remainingSeconds ?? 0) });
  return (
    <HStack gap={1} vAlign="center" wrap="wrap" data-sla-state={state}>
      <StatusDot variant={STATE_DOT[state]} label={stateText} />
      <Text type="supporting" color="primary">{`${t(`sla.metrics.${sla.metric}`)}:`}</Text>
      <Text type="supporting">{stateText}</Text>
    </HStack>
  );
}

export function SlaSummary({ sla, showDue = false }: { sla: readonly TicketSla[]; showDue?: boolean }) {
  const { t } = useTranslation('tickets');
  if (sla.length === 0) {
    return <Text type="supporting">{t('sla.none')}</Text>;
  }
  return (
    <VStack gap={1}>
      {sla.map((item) => (
        <VStack key={item.metric} gap={0}>
          <SlaBadge sla={item} />
          {showDue && item.dueAt !== null && <Text type="supporting">{t('sla.due', { date: formatDateTime(item.dueAt) })}</Text>}
        </VStack>
      ))}
    </VStack>
  );
}
