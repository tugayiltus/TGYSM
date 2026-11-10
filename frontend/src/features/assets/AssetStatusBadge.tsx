/**
 * @input An asset status.
 * @output A colored token with the translated status; color is never the only cue.
 * @position Asset lists and asset detail.
 */
import { Token } from '@astryxdesign/core/Token';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import type { AssetStatus } from './api';

type TokenColor = NonNullable<ComponentProps<typeof Token>['color']>;

/** One fixed color per status, so people learn the mapping across the app. */
const STATUS_COLOR: Record<AssetStatus, TokenColor> = {
  in_stock: 'green',
  assigned: 'blue',
  maintenance: 'yellow',
  lost: 'red',
  retired: 'gray',
};

export function AssetStatusBadge({ status }: { status: AssetStatus }) {
  const { t } = useTranslation('assets');
  return <Token size="sm" color={STATUS_COLOR[status]} label={t(`statuses.${status}`)} />;
}
