import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';

import { readPageParam } from '../../shared/lib/pageParam';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { articlePath, ROUTES } from '../../shared/routes';
import { Pagination } from '../../shared/ui/Pagination';
import { SelectField } from '../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';
import { TextField } from '../../shared/ui/TextField';
import { isItStaffOrAdmin, useSession } from '../auth/session';

import {
  ARTICLE_PAGE_SIZE,
  ARTICLE_STATUSES,
  ARTICLE_VISIBILITIES,
  useArticleCategories,
  useArticles,
  type ArticleListItem,
  type ArticleStatus,
  type ArticleVisibility,
} from './api';
import { ArticleStatusBadge, ArticleVisibilityBadge } from './ArticleStatusBadge';

const SEARCH_DEBOUNCE_MS = 300;
const PARAM = { search: 'q', category: 'category', status: 'status', visibility: 'visibility', page: 'page' } as const;

const asStatus = (value: string): ArticleStatus | '' => ARTICLE_STATUSES.find((s) => s === value) ?? '';
const asVisibility = (value: string): ArticleVisibility | '' => ARTICLE_VISIBILITIES.find((v) => v === value) ?? '';

function ArticleCard({ article, staff }: { article: ArticleListItem; staff: boolean }) {
  return (
    <li className="rounded-xl border border-slate-200 bg-white p-4">
      <Link to={articlePath(article.id)} className="font-medium text-brand-900 underline break-words">
        {article.title}
      </Link>
      {article.summary && <p className="mt-1 text-sm text-slate-700 break-words">{article.summary}</p>}
      <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-slate-600">
        {article.categoryName && <span>{article.categoryName}</span>}
        {staff && <ArticleStatusBadge status={article.status} />}
        {staff && <ArticleVisibilityBadge visibility={article.visibility} />}
      </div>
    </li>
  );
}

export function KnowledgeListPage() {
  const { t } = useTranslation('kb');
  const me = useSession();
  const staff = isItStaffOrAdmin(me.role);
  const [params, setParams] = useSearchParams();

  const search = params.get(PARAM.search) ?? '';
  const categoryId = params.get(PARAM.category) ?? '';
  const status = staff ? asStatus(params.get(PARAM.status) ?? '') : '';
  const visibility = staff ? asVisibility(params.get(PARAM.visibility) ?? '') : '';
  const page = readPageParam(params.get(PARAM.page));
  const debouncedSearch = useDebouncedValue(search.trim(), SEARCH_DEBOUNCE_MS);

  const categories = useArticleCategories();
  const articles = useArticles({ search: debouncedSearch, categoryId, status, visibility, page, pageSize: ARTICLE_PAGE_SIZE });

  /** Any filter change goes back to page 1; the search box replaces history so typing does not fill the back stack. */
  const setParam = (name: string, value: string) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value === '') {
          next.delete(name);
        } else {
          next.set(name, value);
        }
        if (name !== PARAM.page) {
          next.delete(PARAM.page);
        }
        return next;
      },
      { replace: true },
    );

  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('list.title')}</h1>
        {staff && (
          <Link to={ROUTES.kbNew} className="inline-flex min-h-11 items-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-white hover:bg-brand-700">
            {t('list.new')}
          </Link>
        )}
      </div>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 [&>*]:min-w-0">
        <TextField type="search" label={t('list.search')} value={search} onChange={(event) => setParam(PARAM.search, event.target.value)} />
        <SelectField
          label={t('list.category')}
          placeholder={t('list.allCategories')}
          options={(categories.data ?? []).map((c) => ({ value: c.id, label: c.path }))}
          value={categoryId}
          onChange={(event) => setParam(PARAM.category, event.target.value)}
        />
        {staff && (
          <>
            <SelectField
              label={t('list.status')}
              placeholder={t('list.all')}
              options={ARTICLE_STATUSES.map((s) => ({ value: s, label: t(`status.${s}`) }))}
              value={status}
              onChange={(event) => setParam(PARAM.status, event.target.value)}
            />
            <SelectField
              label={t('list.visibility')}
              placeholder={t('list.all')}
              options={ARTICLE_VISIBILITIES.map((v) => ({ value: v, label: t(`visibility.${v}`) }))}
              value={visibility}
              onChange={(event) => setParam(PARAM.visibility, event.target.value)}
            />
          </>
        )}
      </div>
      {articles.isPending && <LoadingState />}
      {articles.isError && <ErrorState error={articles.error} onRetry={() => void articles.refetch()} />}
      {articles.data && articles.data.items.length === 0 && <EmptyState message={t('list.empty')} />}
      {articles.data && articles.data.items.length > 0 && (
        <>
          {/* keepPreviousData shows the last result while the next one loads: mark it busy and dim it, as reports do. */}
          <ul
            aria-label={t('list.caption')}
            aria-busy={articles.isPlaceholderData}
            className={`flex flex-col gap-3 transition-opacity ${articles.isPlaceholderData ? 'opacity-60' : ''}`}
          >
            {articles.data.items.map((article) => (
              <ArticleCard key={article.id} article={article} staff={staff} />
            ))}
          </ul>
          <Pagination page={articles.data.page} pageSize={articles.data.pageSize} totalCount={articles.data.totalCount} onPageChange={(next) => setParam(PARAM.page, String(next))} />
        </>
      )}
    </section>
  );
}
