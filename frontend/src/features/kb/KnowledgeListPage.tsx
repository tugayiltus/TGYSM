import CIcon from '@coreui/icons-react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useSearchParams } from 'react-router';

import { readPageParam } from '../../shared/lib/pageParam';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { articlePath, ROUTES } from '../../shared/routes';
import { DataCard } from '../../shared/ui/DataCard';
import { cilPlus, cilSearch } from '../../shared/ui/icons';
import { Pagination } from '../../shared/ui/Pagination';
import { SelectField } from '../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState, TABLE_SKELETON_ROWS } from '../../shared/ui/States';
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
  const location = useLocation();
  return (
    <li className="list-group-item py-3">
      <Link to={articlePath(article.id)} state={linkState(location)} className="fw-semibold text-break">
        {article.title}
      </Link>
      {article.summary && <p className="mt-1 small text-break">{article.summary}</p>}
      <div className="mt-2 d-flex flex-wrap align-items-center gap-2 small text-body-secondary">
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

  // First use (nothing written yet) and a filter that matches nothing are different situations (UX-11).
  const filtered = search !== '' || categoryId !== '' || status !== '' || visibility !== '';

  return (
    <>
      <PageHeader
        icon={ROUTE_META.kbList.icon}
        titleOverride={t('list.title')}
        actions={
          staff ? (
            <Link to={ROUTES.kbNew} className="btn btn-primary tgysm-touch d-inline-flex align-items-center gap-2">
              <CIcon icon={cilPlus} aria-hidden="true" />
              {t('list.new')}
            </Link>
          ) : undefined
        }
      />
      <DataCard
        filters={
          <div className="row g-3">
            <TextField
              className="col-12 col-sm-6 col-lg-3"
              type="search"
              startIcon={cilSearch}
              label={t('list.search')}
              value={search}
              onChange={(event) => setParam(PARAM.search, event.target.value)}
            />
            <SelectField
              className="col-12 col-sm-6 col-lg-3"
              label={t('list.category')}
              placeholder={t('list.allCategories')}
              options={(categories.data ?? []).map((c) => ({ value: c.id, label: c.path }))}
              value={categoryId}
              onChange={(event) => setParam(PARAM.category, event.target.value)}
            />
            {staff && (
              <>
                <SelectField
                  className="col-12 col-sm-6 col-lg-3"
                  label={t('list.status')}
                  placeholder={t('list.all')}
                  options={ARTICLE_STATUSES.map((s) => ({ value: s, label: t(`status.${s}`) }))}
                  value={status}
                  onChange={(event) => setParam(PARAM.status, event.target.value)}
                />
                <SelectField
                  className="col-12 col-sm-6 col-lg-3"
                  label={t('list.visibility')}
                  placeholder={t('list.all')}
                  options={ARTICLE_VISIBILITIES.map((v) => ({ value: v, label: t(`visibility.${v}`) }))}
                  value={visibility}
                  onChange={(event) => setParam(PARAM.visibility, event.target.value)}
                />
              </>
            )}
          </div>
        }
        footer={
          articles.data && articles.data.items.length > 0 ? (
            <Pagination page={articles.data.page} pageSize={articles.data.pageSize} totalCount={articles.data.totalCount} onPageChange={(next) => setParam(PARAM.page, String(next))} />
          ) : undefined
        }
      >
        {articles.isPending && <LoadingState rows={TABLE_SKELETON_ROWS} />}
        {articles.isError && <ErrorState error={articles.error} onRetry={() => void articles.refetch()} />}
        {articles.data && articles.data.items.length === 0 && <EmptyState message={filtered ? t('list.empty') : t('list.emptyFirst')} />}
        {articles.data && articles.data.items.length > 0 && (
          // keepPreviousData shows the last result while the next one loads: mark it busy and dim it, as reports do.
          <ul aria-label={t('list.caption')} aria-busy={articles.isPlaceholderData} className={`list-group list-group-flush ${articles.isPlaceholderData ? 'opacity-50' : ''}`}>
            {articles.data.items.map((article) => (
              <ArticleCard key={article.id} article={article} staff={staff} />
            ))}
          </ul>
        )}
      </DataCard>
    </>
  );
}
