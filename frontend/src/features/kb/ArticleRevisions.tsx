import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';
import { ResponsiveList, type Column } from '../../shared/ui/ResponsiveList';
import { ErrorState, LoadingState } from '../../shared/ui/States';

import { useArticleRevisions, type ArticleRevisionItem } from './api';

export function ArticleRevisionTable({ revisions }: { revisions: readonly ArticleRevisionItem[] }) {
  const { t } = useTranslation('kb');
  if (revisions.length === 0) {
    return <p className="text-slate-600">{t('revisions.empty')}</p>;
  }
  const columns: Column<ArticleRevisionItem>[] = [
    { key: 'revision', header: t('revisions.revision'), renderCell: (r) => r.revision },
    { key: 'title', header: t('revisions.title'), renderCell: (r) => r.title },
    { key: 'visibility', header: t('revisions.visibility'), renderCell: (r) => t(`visibility.${r.visibility}`) },
    { key: 'editedBy', header: t('revisions.editedBy'), renderCell: (r) => r.editedByName },
    { key: 'editedAt', header: t('revisions.editedAt'), renderCell: (r) => formatDateTime(r.editedAt) },
  ];
  const renderCard = (r: ArticleRevisionItem) => (
    <div className="flex flex-col gap-1">
      <p className="font-medium break-words">{`${t('revisions.revision')} ${r.revision} · ${r.title}`}</p>
      <p className="text-sm text-slate-600">{t(`visibility.${r.visibility}`)}</p>
      <p className="text-sm text-slate-600">{`${r.editedByName} · ${formatDateTime(r.editedAt)}`}</p>
    </div>
  );
  return <ResponsiveList items={revisions} columns={columns} getKey={(r) => String(r.revision)} renderCard={renderCard} caption={t('revisions.caption')} />;
}

/** IT staff only: the endpoint is closed to employees. */
export function ArticleRevisions({ articleId }: { articleId: string }) {
  const revisions = useArticleRevisions(articleId);
  if (revisions.isPending) {
    return <LoadingState />;
  }
  if (revisions.isError) {
    return <ErrorState error={revisions.error} onRetry={() => void revisions.refetch()} />;
  }
  return <ArticleRevisionTable revisions={revisions.data} />;
}
