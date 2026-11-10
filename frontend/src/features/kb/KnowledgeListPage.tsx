/**
 * @input Nothing; filters come from the URL query.
 * @output The article list with search and category filters (and status and visibility for IT staff), and paging.
 * @position `/kb`, inside the app shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { articlePath, ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
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
const SKELETON_ROWS = 5;
const FILTER_KEYS = ['category', 'status', 'visibility'] as const;

const asStatus = (value: string): ArticleStatus | '' => ARTICLE_STATUSES.find((s) => s === value) ?? '';
const asVisibility = (value: string): ArticleVisibility | '' => ARTICLE_VISIBILITIES.find((v) => v === value) ?? '';

function ArticleRow({ article, staff }: { article: ArticleListItem; staff: boolean }) {
  const location = useLocation();
  const link = useRef<HTMLAnchorElement>(null);
  return (
    <ListItem
      label={
        <StateLink href={articlePath(article.id)} state={linkState(location)} ref={link}>
          {article.title}
        </StateLink>
      }
      description={
        <VStack gap={1}>
          {article.summary && <Text type="supporting" wordBreak="break-word">{article.summary}</Text>}
          <HStack gap={2} vAlign="center" wrap="wrap">
            {article.categoryName && <Text type="supporting">{article.categoryName}</Text>}
            {staff && <ArticleStatusBadge status={article.status} />}
            {staff && <ArticleVisibilityBadge visibility={article.visibility} />}
          </HStack>
        </VStack>
      }
      interactiveRef={link}
    />
  );
}

export function KnowledgeListPage() {
  const { t } = useTranslation('kb');
  const me = useSession();
  const staff = isItStaffOrAdmin(me.role);
  const list = useListParams(FILTER_KEYS);
  const categoryId = list.filters.category;
  const status = staff ? asStatus(list.filters.status) : '';
  const visibility = staff ? asVisibility(list.filters.visibility) : '';
  const debouncedSearch = useDebouncedValue(list.search.trim(), SEARCH_DEBOUNCE_MS);

  const categories = useArticleCategories();
  const articles = useArticles({ search: debouncedSearch, categoryId, status, visibility, page: list.page, pageSize: ARTICLE_PAGE_SIZE });

  // First use (nothing written yet) and a filter that matches nothing are different situations (UX-11).
  const filtered = list.search !== '' || categoryId !== '' || status !== '' || visibility !== '';

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.kbList.icon}
        titleOverride={t('list.title')}
        actions={staff ? <Button label={t('list.new')} variant="primary" href={ROUTES.kbNew} icon={<Icon icon={Icons.plus} />} /> : undefined}
      />
      <ListToolbar
        title={t('list.title')}
        search={list.search}
        searchLabel={t('list.search')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('list.category')}
              placeholder={t('list.allCategories')}
              options={(categories.data ?? []).map((c) => ({ value: c.id, label: c.path }))}
              value={categoryId}
              onChange={(value) => list.setFilter('category', value)}
            />
            {staff && (
              <>
                <Selector
                  label={t('list.status')}
                  placeholder={t('list.all')}
                  options={ARTICLE_STATUSES.map((s) => ({ value: s, label: t(`status.${s}`) }))}
                  value={status}
                  onChange={(value) => list.setFilter('status', value)}
                />
                <Selector
                  label={t('list.visibility')}
                  placeholder={t('list.all')}
                  options={ARTICLE_VISIBILITIES.map((v) => ({ value: v, label: t(`visibility.${v}`) }))}
                  value={visibility}
                  onChange={(value) => list.setFilter('visibility', value)}
                />
              </>
            )}
          </>
        }
      />
      <Card padding={0}>
        {articles.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {articles.isError && <ErrorState error={articles.error} onRetry={() => void articles.refetch()} />}
        {articles.data && articles.data.items.length === 0 && <EmptyState message={filtered ? t('list.empty') : t('list.emptyFirst')} />}
        {articles.data && articles.data.items.length > 0 && (
          // keepPreviousData shows the last result while the next one loads: mark it busy for screen readers.
          <List hasDividers aria-label={t('list.caption')} aria-busy={articles.isPlaceholderData}>
            {articles.data.items.map((article) => (
              <ArticleRow key={article.id} article={article} staff={staff} />
            ))}
          </List>
        )}
      </Card>
      {articles.data && articles.data.items.length > 0 && <Pagination page={articles.data.page} pageSize={articles.data.pageSize} totalItems={articles.data.totalCount} onChange={list.setPage} />}
    </VStack>
  );
}
