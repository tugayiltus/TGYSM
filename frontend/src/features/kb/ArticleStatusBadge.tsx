/**
 * @input An article status or visibility.
 * @output A colored token with the translated name; color is never the only cue.
 * @position Knowledge list, article page and editor.
 */
import { Token } from '@astryxdesign/core/Token';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import type { ArticleStatus, ArticleVisibility } from './api';

type TokenColor = NonNullable<ComponentProps<typeof Token>['color']>;

const STATUS_COLOR: Record<ArticleStatus, TokenColor> = { draft: 'yellow', published: 'green', archived: 'gray' };
const VISIBILITY_COLOR: Record<ArticleVisibility, TokenColor> = { employees: 'blue', internal: 'gray' };

export function ArticleStatusBadge({ status }: { status: ArticleStatus }) {
  const { t } = useTranslation('kb');
  return <Token size="sm" color={STATUS_COLOR[status]} label={t(`status.${status}`)} />;
}

export function ArticleVisibilityBadge({ visibility }: { visibility: ArticleVisibility }) {
  const { t } = useTranslation('kb');
  return <Token size="sm" color={VISIBILITY_COLOR[visibility]} label={t(`visibility.${visibility}`)} />;
}
