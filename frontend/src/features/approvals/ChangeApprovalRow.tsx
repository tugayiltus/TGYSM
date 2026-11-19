/**
 * @input A pending change approval and a callback for the picked decision.
 * @output A list row with the change, its risk, window and plan excerpt, and approve, reject and return buttons.
 * @position Onaylar page.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { changePath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import type { ApprovalDecision, ChangeApprovalItem } from '../changes/api';
import { RiskBadge } from '../changes/ChangeBadges';
import { WindowText } from '../changes/WindowText';

import type { ChangeDecision } from './ChangeApprovalDialog';
import { KindToken } from './KindToken';

export function ChangeApprovalRow({ item, onDecide }: { item: ChangeApprovalItem; onDecide: (pending: ChangeDecision) => void }) {
  const { t } = useTranslation('changes');
  const location = useLocation();
  const link = useRef<HTMLAnchorElement>(null);
  const decide = (decision: ApprovalDecision) => () => onDecide({ item, decision });
  return (
    <ListItem
      startContent={<KindToken kind="change" />}
      label={
        <StateLink href={changePath(item.changeNumber)} state={linkState(location)} ref={link}>
          {item.title}
        </StateLink>
      }
      description={
        <VStack gap={1}>
          <HStack gap={2} vAlign="center" wrap="wrap">
            <Text type="supporting">{item.changeNumber}</Text>
            <RiskBadge risk={item.risk} />
            <WindowText start={item.windowStart} end={item.windowEnd} />
            {item.conflictCount > 0 && <Text type="supporting">{t('conflicts.count', { count: item.conflictCount })}</Text>}
          </HStack>
          {item.implementationPlanExcerpt !== '' && (
            <Text type="supporting" wordBreak="break-word">
              {item.implementationPlanExcerpt}
            </Text>
          )}
        </VStack>
      }
      interactiveRef={link}
      endContent={
        <HStack gap={2} wrap="wrap">
          <Button label={t('approval.approve')} aria-label={recordActionLabel(t('approval.approve'), item.changeNumber)} variant="primary" icon={<Icon icon={Icons.check} />} onClick={decide('approve')} />
          <Button label={t('approval.reject')} aria-label={recordActionLabel(t('approval.reject'), item.changeNumber)} variant="destructive" icon={<Icon icon={Icons.close} />} onClick={decide('reject')} />
          <Button label={t('approval.return')} aria-label={recordActionLabel(t('approval.return'), item.changeNumber)} variant="secondary" onClick={decide('return')} />
        </HStack>
      }
    />
  );
}
