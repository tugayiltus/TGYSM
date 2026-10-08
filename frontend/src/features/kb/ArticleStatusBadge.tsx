import { useTranslation } from 'react-i18next';

import type { ArticleStatus, ArticleVisibility } from './api';

const STATUS_CLASSES: Record<ArticleStatus, string> = {
  draft: 'bg-warning-subtle text-warning-emphasis',
  published: 'bg-success-subtle text-success-emphasis',
  archived: 'bg-secondary-subtle text-secondary-emphasis',
};

const BADGE_BASE = 'badge fw-medium';

export function ArticleStatusBadge({ status }: { status: ArticleStatus }) {
  const { t } = useTranslation('kb');
  return <span className={`${BADGE_BASE} ${STATUS_CLASSES[status]}`}>{t(`status.${status}`)}</span>;
}

export function ArticleVisibilityBadge({ visibility }: { visibility: ArticleVisibility }) {
  const { t } = useTranslation('kb');
  const classes = visibility === 'internal' ? 'bg-dark-subtle text-dark-emphasis' : 'bg-info-subtle text-info-emphasis';
  return <span className={`${BADGE_BASE} ${classes}`}>{t(`visibility.${visibility}`)}</span>;
}
