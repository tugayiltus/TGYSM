/**
 * @input A change status, outcome or risk level.
 * @output A colored token with the translated name; color is never the only cue.
 * @position Change lists, change detail and the summary view.
 */
import { Token } from '@astryxdesign/core/Token';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import type { ChangeOutcome, ChangeStatus, Level } from './api';

type TokenColor = NonNullable<ComponentProps<typeof Token>['color']>;

const STATUS_COLOR: Record<ChangeStatus, TokenColor> = {
  draft: 'gray',
  pending_approval: 'yellow',
  scheduled: 'blue',
  implementing: 'cyan',
  verifying: 'cyan',
  closed: 'green',
  cancelled: 'gray',
};

const OUTCOME_COLOR: Record<ChangeOutcome, TokenColor> = {
  successful: 'green',
  rolled_back: 'yellow',
  failed: 'red',
};

const LEVEL_COLOR: Record<Level, TokenColor> = {
  low: 'green',
  medium: 'yellow',
  high: 'red',
};

export function ChangeStatusBadge({ status }: { status: ChangeStatus | null }) {
  const { t } = useTranslation('changes');
  return status === null ? null : <Token size="sm" color={STATUS_COLOR[status]} label={t(`statuses.${status}`)} />;
}

export function ChangeOutcomeBadge({ outcome }: { outcome: ChangeOutcome | null }) {
  const { t } = useTranslation('changes');
  return outcome === null ? null : <Token size="sm" color={OUTCOME_COLOR[outcome]} label={t(`outcomes.${outcome}`)} />;
}

export function RiskBadge({ risk }: { risk: Level }) {
  const { t } = useTranslation('changes');
  return <Token size="sm" color={LEVEL_COLOR[risk]} label={`${t('detail.risk')}: ${t(`levels.${risk}`)}`} />;
}
