import { zodResolver } from '@hookform/resolvers/zod';
import { useId, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TextField } from '../../../shared/ui/TextField';

import { CATEGORY_SCOPES, useCategories, useSaveCategory, useSetCategoryActive, type CategoryItem, type CategoryScope } from './api';

/** Mirrors backend Category.NameMaxLength. */
const NAME_MAX_LENGTH = 150;
const NO_PARENT = '';
const PATH_SEPARATOR = ' / ';

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
}

/** Parents first, each followed by its children (two levels, spec §2.2). */
function toRows(items: readonly CategoryItem[]): Row[] {
  const byId = new Map(items.map((c) => [c.id, c]));
  const roots = items.filter((c) => c.parentId === null);
  return roots.flatMap((root) => [
    { item: root, path: root.name },
    ...items.filter((c) => c.parentId === root.id).map((child) => ({ item: child, path: `${byId.get(root.id)?.name ?? ''}${PATH_SEPARATOR}${child.name}` })),
  ]);
}

function CategoryFormDialog({ category, all, onClose }: { category: CategoryItem | null; all: readonly CategoryItem[]; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const save = useSaveCategory();
  const parents = useMemo(
    () => all.filter((c) => c.parentId === null && c.isActive && c.id !== category?.id).map((c) => ({ value: c.id, label: c.name })),
    [all, category],
  );
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<Values>({
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
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={category === null ? t('categories.new') : t('categories.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={save.isPending}>
            {category === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="flex flex-col gap-4" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('categories.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
        <SelectField label={t('categories.fields.parent')} placeholder={t('categories.noParent')} options={parents} error={fieldError(errors.parentId)} {...register('parentId')} />
        <SelectField
          label={t('categories.fields.scope')}
          options={CATEGORY_SCOPES.map((value) => ({ value, label: t(`categories.scopes.${value}`) }))}
          error={fieldError(errors.scope)}
          {...register('scope')}
        />
      </form>
    </Dialog>
  );
}

export function CategoriesPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const categories = useCategories();
  const setActive = useSetCategoryActive();
  const [form, setForm] = useState<{ category: CategoryItem | null } | null>(null);
  const [toggling, setToggling] = useState<CategoryItem | null>(null);
  const rows = useMemo(() => toRows(categories.data ?? []), [categories.data]);

  const actions = (row: Row) => (
    <div className="flex flex-wrap gap-2">
      <Button variant="secondary" aria-label={recordActionLabel(tc('actions.edit'), row.path)} onClick={() => setForm({ category: row.item })}>
        {tc('actions.edit')}
      </Button>
      <Button
        variant="secondary"
        aria-label={recordActionLabel(row.item.isActive ? tc('actions.deactivate') : tc('actions.reactivate'), row.path)}
        onClick={() => {
          setActive.reset();
          setToggling(row.item);
        }}
      >
        {row.item.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
      </Button>
    </div>
  );
  const columns: Column<Row>[] = [
    { key: 'name', header: t('categories.columns.name'), renderCell: (r) => <span className={r.item.parentId ? 'pl-4' : 'font-medium'}>{r.path}</span> },
    { key: 'scope', header: t('categories.columns.scope'), renderCell: (r) => t(`categories.scopes.${r.item.scope}`) },
    { key: 'status', header: t('categories.columns.status'), renderCell: (r) => <StatusBadge isActive={r.item.isActive} /> },
    { key: 'actions', header: t('categories.columns.actions'), renderCell: actions },
  ];
  const renderCard = (r: Row) => (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium break-words">{r.path}</p>
          <p className="text-sm text-slate-600">{t(`categories.scopes.${r.item.scope}`)}</p>
        </div>
        <StatusBadge isActive={r.item.isActive} />
      </div>
      {actions(r)}
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('categories.title')}</h1>
        <Button onClick={() => setForm({ category: null })}>{t('categories.new')}</Button>
      </div>
      {categories.isPending && <LoadingState />}
      {categories.isError && <ErrorState error={categories.error} onRetry={() => void categories.refetch()} />}
      {categories.data && rows.length === 0 && <EmptyState />}
      {rows.length > 0 && <ResponsiveList items={rows} columns={columns} getKey={(r) => r.item.id} renderCard={renderCard} caption={t('categories.listCaption')} />}
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
    </section>
  );
}
