import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { formatDateTime } from '../../shared/lib/format';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { useReturnTo } from '../../shared/navigation/returnTo';
import { articleEditPath, ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { ErrorState, LoadingState } from '../../shared/ui/States';
import { isItStaffOrAdmin, useSession } from '../auth/session';

import { useArticle, useArticleAction, type ArticleAction, type ArticleDetail } from './api';
import { ArticleMarkdown } from './ArticleMarkdown';
import { ArticleRevisions } from './ArticleRevisions';
import { ArticleStatusBadge, ArticleVisibilityBadge } from './ArticleStatusBadge';

const ACTIONS_BY_STATUS: Record<ArticleDetail['status'], readonly ArticleAction[]> = {
  draft: ['publish', 'archive'],
  published: ['archive'],
  archived: ['restore'],
};

function StaffActions({ article }: { article: ArticleDetail }) {
  const { t } = useTranslation('kb');
  const action = useArticleAction(article.id);
  return (
    <div className="d-flex flex-column gap-2">
      <FormAlert message={action.error ? describeError(action.error) : null} />
      <div className="d-flex flex-wrap gap-2">
        {article.status !== 'archived' && (
          <Link to={articleEditPath(article.id)} className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center">
            {t('article.edit')}
          </Link>
        )}
        {ACTIONS_BY_STATUS[article.status].map((name) => (
          <Button key={name} variant="secondary" isLoading={action.isPending && action.variables.action === name} disabled={action.isPending} onClick={() => action.mutate({ action: name, version: article.version })}>
            {t(`article.${name}`)}
          </Button>
        ))}
      </div>
    </div>
  );
}

function ArticleView({ article }: { article: ArticleDetail }) {
  const { t } = useTranslation('kb');
  const me = useSession();
  const staff = isItStaffOrAdmin(me.role);
  const backTo = useReturnTo(ROUTES.kb);
  useBreadcrumbLabel('kbArticle', article.title);
  return (
    <article className="d-flex flex-column gap-3">
      <PageHeader
        titleOverride={article.title}
        actions={
          <Link to={backTo} className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center">
            {t('article.back')}
          </Link>
        }
      />
      <div className="d-flex flex-column gap-2">
        {staff && (
          <div className="d-flex flex-wrap align-items-center gap-2">
            <ArticleStatusBadge status={article.status} />
            <ArticleVisibilityBadge visibility={article.visibility} />
          </div>
        )}
        <p className="small text-body-secondary">
          {[
            article.categoryName,
            `${t('article.author')}: ${article.authorName}`,
            article.publishedAt ? `${t('article.publishedAt')}: ${formatDateTime(article.publishedAt)}` : null,
            `${t('article.updatedAt')}: ${formatDateTime(article.updatedAt)}`,
          ]
            .filter(Boolean)
            .join(' · ')}
        </p>
        {article.summary && <p className="text-break">{article.summary}</p>}
      </div>
      {staff && <StaffActions article={article} />}
      <ArticleMarkdown body={article.body} />
      {staff && (
        <section aria-labelledby="article-revisions" className="d-flex flex-column gap-2">
          <h2 id="article-revisions" className="fs-5 fw-semibold">
            {t('article.revisions')}
          </h2>
          <ArticleRevisions articleId={article.id} />
        </section>
      )}
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
