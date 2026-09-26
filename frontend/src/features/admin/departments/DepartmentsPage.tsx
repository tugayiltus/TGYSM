import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { Pagination } from '../../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TextField } from '../../../shared/ui/TextField';

import { useDepartments, useSetDepartmentActive, type DepartmentItem } from './api';
import { DepartmentFormDialog } from './DepartmentFormDialog';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SEARCH_DEBOUNCE_MS = 300;

type FormState = { department: DepartmentItem | null } | null;

interface RowActionsProps {
  department: DepartmentItem;
  onEdit: (department: DepartmentItem) => void;
  onToggle: (department: DepartmentItem) => void;
}

function DepartmentActions({ department, onEdit, onToggle }: RowActionsProps) {
  const { t: tc } = useTranslation();
  return (
    <div className="flex flex-wrap gap-2">
      <Button variant="secondary" onClick={() => onEdit(department)}>
        {tc('actions.edit')}
      </Button>
      <Button variant="secondary" onClick={() => onToggle(department)}>
        {department.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
      </Button>
    </div>
  );
}

export function DepartmentsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(FIRST_PAGE);
  const [form, setForm] = useState<FormState>(null);
  const [toggling, setToggling] = useState<DepartmentItem | null>(null);
  const debouncedSearch = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  const departments = useDepartments({ search: debouncedSearch, isActive: undefined, page, pageSize: PAGE_SIZE });
  const setActive = useSetDepartmentActive();

  const edit = (department: DepartmentItem) => setForm({ department });
  const openToggle = (department: DepartmentItem) => {
    setActive.reset();
    setToggling(department);
  };

  const columns: Column<DepartmentItem>[] = [
    { key: 'name', header: t('departments.columns.name'), renderCell: (d) => <span className="font-medium">{d.name}</span> },
    { key: 'approver', header: t('departments.columns.approver'), renderCell: (d) => d.approverName ?? tc('none') },
    { key: 'status', header: t('departments.columns.status'), renderCell: (d) => <StatusBadge isActive={d.isActive} /> },
    { key: 'actions', header: t('departments.columns.actions'), renderCell: (d) => <DepartmentActions department={d} onEdit={edit} onToggle={openToggle} /> },
  ];

  const renderCard = (d: DepartmentItem) => (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium break-words">{d.name}</p>
          <p className="text-sm text-slate-600">{d.approverName ?? tc('none')}</p>
        </div>
        <StatusBadge isActive={d.isActive} />
      </div>
      <DepartmentActions department={d} onEdit={edit} onToggle={openToggle} />
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('departments.title')}</h1>
        <Button onClick={() => setForm({ department: null })}>{t('departments.new')}</Button>
      </div>
      <TextField
        type="search"
        className="sm:max-w-sm"
        label={t('departments.searchLabel')}
        value={search}
        onChange={(event) => {
          setSearch(event.target.value);
          setPage(FIRST_PAGE);
        }}
      />

      {departments.isPending && <LoadingState />}
      {departments.isError && <ErrorState error={departments.error} onRetry={() => void departments.refetch()} />}
      {departments.data && departments.data.items.length === 0 && <EmptyState />}
      {departments.data && departments.data.items.length > 0 && (
        <>
          <ResponsiveList items={departments.data.items} columns={columns} getKey={(d) => d.id} renderCard={renderCard} caption={t('departments.listCaption')} />
          <Pagination page={departments.data.page} pageSize={departments.data.pageSize} totalCount={departments.data.totalCount} onPageChange={setPage} />
        </>
      )}

      {form !== null && <DepartmentFormDialog key={form.department?.id ?? 'new'} department={form.department} onClose={() => setForm(null)} />}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('departments.confirmDeactivate', { name: toggling.name }) : t('departments.confirmReactivate', { name: toggling.name })}
          confirmLabel={tc('actions.confirm')}
          variant={toggling.isActive ? 'danger' : 'primary'}
          isPending={setActive.isPending}
          error={setActive.error}
          onConfirm={() =>
            setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })
          }
          onCancel={() => setToggling(null)}
        />
      )}
    </section>
  );
}
