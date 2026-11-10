/**
 * @input The article id from the route.
 * @output The article with its meta line and body; IT staff also get status actions and revisions.
 * @position `/kb/:id`, inside the app shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Divider } from '@astryxdesign/core/Divider';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { useReturnTo } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { SectionCard } from '../../shared/page/SectionCard';
import { articleEditPath, ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { isItStaffOrAdmin, useSession } from '../auth/session';

import { useArticle, useArticleAction, type ArticleAction, type ArticleDetail } from './api';
import { ArticleMarkdown } from './ArticleMarkdown';
import { ArticleRevisions } from './ArticleRevisions';
import { ArticleStatusBadge, ArticleVisibilityBadge } from './ArticleStatusBadge';

const META_SEPARATOR = ' · ';

const ACTIONS_BY_STATUS: Record<ArticleDetail['status'], readonly ArticleAction[]> = {
  draft: ['publish', 'archive'],
  published: ['archive'],
  archived: ['restore'],
};

function StaffActions({ article }: { article: ArticleDetail }) {
  const { t } = useTranslation('kb');
  const action = useArticleAction(article.id);
  return (
    <VStack gap={2}>
      <FormAlert message={action.error ? describeError(action.error) : null} />
      <HStack gap={2} wrap="wrap">
        {article.status !== 'archived' && <Button label={t('article.edit')} variant="secondary" href={articleEditPath(article.id)} />}
        {ACTIONS_BY_STATUS[article.status].map((name) => (
          <Button
            key={name}
            label={t(`article.${name}`)}
            variant="secondary"
            isLoading={action.isPending && action.variables.action === name}
            isDisabled={action.isPending}
            onClick={() => action.mutate({ action: name, version: article.version })}
          />
        ))}
      </HStack>
    </VStack>
  );
}

function ArticleView({ article }: { article: ArticleDetail }) {
  const { t } = useTranslation('kb');
  const me = useSession();
  const staff = isItStaffOrAdmin(me.role);
  const backTo = useReturnTo(ROUTES.kb);
  useBreadcrumbLabel('kbArticle', article.title);
  const meta = [
    article.categoryName,
    `${t('article.author')}: ${article.authorName}`,
    article.publishedAt ? `${t('article.publishedAt')}: ${formatDateTime(article.publishedAt)}` : null,
    `${t('article.updatedAt')}: ${formatDateTime(article.updatedAt)}`,
  ]
    .filter(Boolean)
    .join(META_SEPARATOR);
  return (
    // The article landmark lets assistive tech jump to the article itself.
    <article>
      <VStack gap={4}>
        <PageHeader
          icon={ROUTE_META.kbList.icon}
          titleOverride={article.title}
          actions={<Button label={t('article.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />}
        />
        <SectionCard>
          <VStack gap={3}>
            {staff && (
              <HStack gap={2} vAlign="center" wrap="wrap">
                <ArticleStatusBadge status={article.status} />
                <ArticleVisibilityBadge visibility={article.visibility} />
              </HStack>
            )}
            <Text type="supporting">{meta}</Text>
            {article.summary && (
              <Text type="large" wordBreak="break-word">
                {article.summary}
              </Text>
            )}
            {staff && <StaffActions article={article} />}
            <Divider />
            <ArticleMarkdown body={article.body} />
          </VStack>
        </SectionCard>
        {staff && (
          <SectionCard title={t('article.revisions')}>
            <ArticleRevisions articleId={article.id} />
          </SectionCard>
        )}
      </VStack>
    </article>
  );
}

export function ArticlePage() {
  const { id = '' } = useParams();
  const article = useArticle(id);
  if (article.isPending) {
    return <LoadingState />;
  }
  if (article.isError) {
    return <ErrorState error={article.error} onRetry={() => void article.refetch()} />;
  }
  return <ArticleView key={article.data.id} article={article.data} />;
}
