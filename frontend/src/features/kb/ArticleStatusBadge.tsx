import { useTranslation } from 'react-i18next';

import type { ArticleStatus, ArticleVisibility } from './api';

const STATUS_CLASSES: Record<ArticleStatus, string> = {
  draft: 'bg-amber-100 text-amber-900',
  published: 'bg-green-100 text-green-900',
  archived: 'bg-slate-200 text-slate-800',
};

const BADGE_BASE = 'inline-flex rounded px-1.5 py-0.5 text-xs font-medium';

export function ArticleStatusBadge({ status }: { status: ArticleStatus }) {
  const { t } = useTranslation('kb');
  return <span className={`${BADGE_BASE} ${STATUS_CLASSES[status]}`}>{t(`status.${status}`)}</span>;
}

export function ArticleVisibilityBadge({ visibility }: { visibility: ArticleVisibility }) {
  const { t } = useTranslation('kb');
  const classes = visibility === 'internal' ? 'bg-violet-100 text-violet-900' : 'bg-sky-100 text-sky-900';
  return <span className={`${BADGE_BASE} ${classes}`}>{t(`visibility.${visibility}`)}</span>;
}
