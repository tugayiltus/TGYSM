import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { formatDateTime } from '../../shared/lib/format';
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
    <div className="flex flex-col gap-2">
      <FormAlert message={action.error ? describeError(action.error) : null} />
      <div className="flex flex-wrap gap-2">
        {article.status !== 'archived' && (
          <Link to={articleEditPath(article.id)} className="inline-flex min-h-11 items-center rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium hover:bg-slate-50">
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
  return (
    <article className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <Link to={ROUTES.kb} className="text-sm text-brand-900 underline">
          {t('article.back')}
        </Link>
        <h1 className="text-2xl font-semibold break-words">{article.title}</h1>
        {staff && (
          <div className="flex flex-wrap items-center gap-2">
            <ArticleStatusBadge status={article.status} />
            <ArticleVisibilityBadge visibility={article.visibility} />
          </div>
        )}
        <p className="text-sm text-slate-600">
          {[
            article.categoryName,
            `${t('article.author')}: ${article.authorName}`,
            article.publishedAt ? `${t('article.publishedAt')}: ${formatDateTime(article.publishedAt)}` : null,
            `${t('article.updatedAt')}: ${formatDateTime(article.updatedAt)}`,
          ]
            .filter(Boolean)
            .join(' · ')}
        </p>
        {article.summary && <p className="text-slate-700 break-words">{article.summary}</p>}
      </div>
      {staff && <StaffActions article={article} />}
      <ArticleMarkdown body={article.body} />
      {staff && (
        <section aria-labelledby="article-revisions" className="flex flex-col gap-3">
          <h2 id="article-revisions" className="text-lg font-semibold">
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
