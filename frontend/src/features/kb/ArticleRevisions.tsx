/**
 * @input An article id, or a list of revisions.
 * @output The revision history: number, title, visibility, editor and time.
 * @position Article page, for IT staff.
 */
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { DataList, type DataColumn } from '../../shared/list/DataList';

import { useArticleRevisions, type ArticleRevisionItem } from './api';

const META_SEPARATOR = ' · ';

export function ArticleRevisionTable({ revisions }: { revisions: readonly ArticleRevisionItem[] }) {
  const { t } = useTranslation('kb');
  if (revisions.length === 0) {
    return <Text type="supporting">{t('revisions.empty')}</Text>;
  }
  const columns: DataColumn<ArticleRevisionItem>[] = [
    { key: 'revision', header: t('revisions.revision'), renderCell: (r) => r.revision },
    { key: 'title', header: t('revisions.title'), renderCell: (r) => r.title },
    { key: 'visibility', header: t('revisions.visibility'), renderCell: (r) => t(`visibility.${r.visibility}`) },
    { key: 'editedBy', header: t('revisions.editedBy'), renderCell: (r) => r.editedByName },
    { key: 'editedAt', header: t('revisions.editedAt'), renderCell: (r) => formatDateTime(r.editedAt) },
  ];
  const renderCard = (r: ArticleRevisionItem) => (
    <VStack gap={0.5}>
      <Text type="label" wordBreak="break-word">{`${t('revisions.revision')} ${r.revision}${META_SEPARATOR}${r.title}`}</Text>
      <Text type="supporting">{t(`visibility.${r.visibility}`)}</Text>
      <Text type="supporting">{`${r.editedByName}${META_SEPARATOR}${formatDateTime(r.editedAt)}`}</Text>
    </VStack>
  );
  return <DataList items={revisions} columns={columns} getKey={(r) => String(r.revision)} renderCard={renderCard} caption={t('revisions.caption')} />;
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
