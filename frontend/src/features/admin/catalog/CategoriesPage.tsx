/**
 * @input Nothing; search, scope and status come from the URL query.
 * @output The two-level category list with create, edit and (de)activate.
 * @position `/admin/categories`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Selector } from '@astryxdesign/core/Selector';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { FormSelector, FormTextInput } from '../../../shared/form/formFields';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { ListToolbar } from '../../../shared/list/ListToolbar';
import { useListParams } from '../../../shared/list/useListParams';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { ActiveToken } from '../../../shared/page/ActiveToken';
import { RecordActions } from '../../../shared/page/RecordActions';
import { Icons } from '../../../shared/ui/icons';
import { filterRows, readStatusFilter, STATUS_FILTER_KEY } from '../shared/statusFilter';
import { StatusFilterSelector } from '../shared/StatusFilterSelector';

import { CATEGORY_SCOPES, useCategories, useSaveCategory, useSetCategoryActive, type CategoryItem, type CategoryScope } from './api';

/** Mirrors backend Category.NameMaxLength. */
const NAME_MAX_LENGTH = 150;
const NO_PARENT = '';
const PATH_SEPARATOR = ' / ';
const SKELETON_ROWS = 5;
const SCOPE_FILTER_KEY = 'scope';
const FILTER_KEYS = [SCOPE_FILTER_KEY, STATUS_FILTER_KEY] as const;

const schema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  parentId: z.string(),
  scope: z.string().min(1, ERROR_CODES.required),
});
type Values = z.infer<typeof schema>;

const FORM_FIELDS = ['name', 'parentId', 'scope'] as const;
const CODE_FIELDS = {
  [ERROR_CODES.categoryNameTaken]: 'name',
  [ERROR_CODES.categoryTooDeep]: 'parentId',
  [ERROR_CODES.referenceInactive]: 'parentId',
} as const;

interface Row {
  item: CategoryItem;
  path: string;
  isActive: boolean;
}

/** Parents first, each followed by its children (two levels, spec §2.2). */
function toRows(items: readonly CategoryItem[]): Row[] {
  const roots = items.filter((c) => c.parentId === null);
  return roots.flatMap((root) => [
    { item: root, path: root.name, isActive: root.isActive },
    ...items.filter((c) => c.parentId === root.id).map((child) => ({ item: child, path: `${root.name}${PATH_SEPARATOR}${child.name}`, isActive: child.isActive })),
  ]);
}

const readScope = (value: string): CategoryScope | undefined => CATEGORY_SCOPES.find((scope) => scope === value);

function CategoryFormDialog({ category, all, onClose }: { category: CategoryItem | null; all: readonly CategoryItem[]; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const save = useSaveCategory();
  const parents = useMemo(
    () => all.filter((c) => c.parentId === null && c.isActive && c.id !== category?.id).map((c) => ({ value: c.id, label: c.name })),
    [all, category],
  );
  const { control, handleSubmit, setError } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: category?.name ?? '', parentId: category?.parentId ?? NO_PARENT, scope: category?.scope ?? 'both' },
  });
  const onSubmit = handleSubmit((v) =>
    save.mutate(
      { id: category?.id ?? null, version: category?.version ?? 0, name: v.name, parentId: v.parentId === NO_PARENT ? null : v.parentId, scope: v.scope as CategoryScope },
      { onSuccess: onClose, onError: (error) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) },
    ),
  );
  const alert = save.error && !isHandledByFields(save.error, FORM_FIELDS, CODE_FIELDS) ? describeError(save.error) : null;

  return (
    <FormDialog
      title={category === null ? t('categories.new') : t('categories.edit')}
      submitLabel={category === null ? tc('actions.create') : tc('actions.save')}
      isPending={save.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="name" label={t('categories.fields.name')} autoComplete="off" isRequired />
      <FormSelector control={control} name="parentId" label={t('categories.fields.parent')} placeholder={t('categories.noParent')} options={parents} isClearable />
      <FormSelector
        control={control}
        name="scope"
        label={t('categories.fields.scope')}
        isRequired
        options={CATEGORY_SCOPES.map((value) => ({ value, label: t(`categories.scopes.${value}`) }))}
      />
    </FormDialog>
  );
}

export function CategoriesPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const list = useListParams(FILTER_KEYS);
  const scope = readScope(list.filters.scope);
  const status = readStatusFilter(list.filters.status);
  const categories = useCategories();
  const setActive = useSetCategoryActive();
  const [form, setForm] = useState<{ category: CategoryItem | null } | null>(null);
  const [toggling, setToggling] = useState<CategoryItem | null>(null);
  const rows = useMemo(
    () => (categories.data === undefined ? undefined : filterRows(toRows(categories.data), list.search, status, (r) => r.path).filter((r) => scope === undefined || r.item.scope === scope)),
    [categories.data, list.search, status, scope],
  );

  const actions = (r: Row) => (
    <RecordActions
      name={r.path}
      isActive={r.isActive}
      onEdit={() => setForm({ category: r.item })}
      onToggle={() => {
        setActive.reset();
        setToggling(r.item);
      }}
    />
  );
  const columns: DataColumn<Row>[] = [
    { key: 'name', header: t('categories.columns.name'), renderCell: (r) => <Text type={r.item.parentId === null ? 'label' : 'body'}>{r.path}</Text> },
    { key: 'scope', header: t('categories.columns.scope'), renderCell: (r) => t(`categories.scopes.${r.item.scope}`) },
    { key: 'status', header: t('categories.columns.status'), renderCell: (r) => <ActiveToken isActive={r.isActive} /> },
    { key: 'actions', header: t('categories.columns.actions'), renderCell: actions },
  ];
  const renderCard = (r: Row) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <Text type="label" wordBreak="break-word">
              {r.path}
            </Text>
            <Text type="supporting">{t(`categories.scopes.${r.item.scope}`)}</Text>
          </VStack>
        </StackItem>
        <ActiveToken isActive={r.isActive} />
      </HStack>
      {actions(r)}
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminCategories.icon}
        titleOverride={t('categories.title')}
        actions={<Button label={t('categories.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ category: null })} />}
      />
      <ListToolbar
        title={t('categories.title')}
        search={list.search}
        searchLabel={t('categories.searchLabel')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('categories.scopeFilter')}
              placeholder={tc('status.all')}
              value={scope ?? ''}
              options={CATEGORY_SCOPES.map((value) => ({ value, label: t(`categories.scopes.${value}`) }))}
              onChange={(value) => list.setFilter(SCOPE_FILTER_KEY, value)}
            />
            <StatusFilterSelector value={status} onChange={(value) => list.setFilter(STATUS_FILTER_KEY, value)} />
          </>
        }
      />
      <Card padding={0}>
        {categories.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {categories.isError && <ErrorState error={categories.error} onRetry={() => void categories.refetch()} />}
        {rows?.length === 0 && <EmptyState />}
        {rows !== undefined && rows.length > 0 && <DataList items={rows} columns={columns} getKey={(r) => r.item.id} renderCard={renderCard} caption={t('categories.listCaption')} />}
      </Card>
      {form !== null && <CategoryFormDialog key={form.category?.id ?? 'new'} category={form.category} all={categories.data ?? []} onClose={() => setForm(null)} />}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('categories.confirmDeactivate', { name: toggling.name }) : t('categories.confirmReactivate', { name: toggling.name })}
          confirmLabel={tc('actions.confirm')}
          variant={toggling.isActive ? 'danger' : 'primary'}
          isPending={setActive.isPending}
          error={setActive.error}
          onConfirm={() => setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })}
          onCancel={() => setToggling(null)}
        />
      )}
    </VStack>
  );
}
