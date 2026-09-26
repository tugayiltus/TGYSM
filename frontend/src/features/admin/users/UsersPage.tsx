import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { Pagination } from '../../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TemporaryPasswordDialog } from '../../../shared/ui/TemporaryPasswordDialog';
import { TextField } from '../../../shared/ui/TextField';

import { useResetUserPassword, useSetUserActive, useUsers, type UserListItem, type UserRole } from './api';
import { UserFormDialog } from './UserFormDialog';
import { USER_ROLES } from './userSchema';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SEARCH_DEBOUNCE_MS = 300;
const ALL_ROLES = '';

type StatusFilter = 'all' | 'active' | 'inactive';
const STATUS_FILTERS: readonly StatusFilter[] = ['all', 'active', 'inactive'];
const STATUS_TO_IS_ACTIVE: Record<StatusFilter, boolean | undefined> = { all: undefined, active: true, inactive: false };

type PendingAction = { kind: 'deactivate' | 'reactivate' | 'reset'; user: UserListItem };
type FormState = { mode: 'create' } | { mode: 'edit'; user: UserListItem } | null;

const fullName = (user: UserListItem) => `${user.firstName} ${user.lastName}`;

interface UserActionsProps {
  user: UserListItem;
  onEdit: (user: UserListItem) => void;
  onAction: (action: PendingAction) => void;
}

function UserActions({ user, onEdit, onAction }: UserActionsProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  return (
    <div className="flex flex-wrap gap-2">
      <Button variant="secondary" onClick={() => onEdit(user)}>
        {tc('actions.edit')}
      </Button>
      <Button variant="secondary" onClick={() => onAction({ kind: user.isActive ? 'deactivate' : 'reactivate', user })}>
        {user.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
      </Button>
      <Button variant="ghost" onClick={() => onAction({ kind: 'reset', user })}>
        {t('users.resetPassword')}
      </Button>
    </div>
  );
}

export function UsersPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<StatusFilter>('all');
  const [role, setRole] = useState<UserRole | typeof ALL_ROLES>(ALL_ROLES);
  const [page, setPage] = useState(FIRST_PAGE);
  const [form, setForm] = useState<FormState>(null);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [temporaryPassword, setTemporaryPassword] = useState<string | null>(null);
  const debouncedSearch = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  const users = useUsers({
    search: debouncedSearch,
    isActive: STATUS_TO_IS_ACTIVE[status],
    role: role === ALL_ROLES ? undefined : role,
    page,
    pageSize: PAGE_SIZE,
  });
  const setActive = useSetUserActive();
  const resetPassword = useResetUserPassword();

  const editUser = (user: UserListItem) => setForm({ mode: 'edit', user });
  const openAction = (action: PendingAction) => {
    setActive.reset();
    resetPassword.reset();
    setPending(action);
  };
  const confirmAction = () => {
    if (pending === null) {
      return;
    }
    if (pending.kind === 'reset') {
      resetPassword.mutate(pending.user.id, {
        onSuccess: (result) => {
          setPending(null);
          setTemporaryPassword(result.temporaryPassword);
        },
      });
      return;
    }
    setActive.mutate(
      { id: pending.user.id, version: pending.user.version, isActive: pending.kind === 'reactivate' },
      { onSuccess: () => setPending(null) },
    );
  };

  const confirmText = pending === null ? null : {
    deactivate: { message: t('users.confirmDeactivate', { name: fullName(pending.user) }), label: tc('actions.deactivate') },
    reactivate: { message: t('users.confirmReactivate', { name: fullName(pending.user) }), label: tc('actions.reactivate') },
    reset: { message: t('users.confirmReset', { name: fullName(pending.user) }), label: t('users.resetPassword') },
  }[pending.kind];

  const columns: Column<UserListItem>[] = [
    { key: 'name', header: t('users.columns.name'), renderCell: (user) => <span className="font-medium">{fullName(user)}</span> },
    { key: 'email', header: t('users.columns.email'), renderCell: (user) => <span className="break-all">{user.email}</span> },
    { key: 'role', header: t('users.columns.role'), renderCell: (user) => tc(`roles.${user.role}`) },
    { key: 'department', header: t('users.columns.department'), renderCell: (user) => user.departmentName ?? tc('none') },
    { key: 'status', header: t('users.columns.status'), renderCell: (user) => <StatusBadge isActive={user.isActive} /> },
    { key: 'actions', header: t('users.columns.actions'), renderCell: (user) => <UserActions user={user} onEdit={editUser} onAction={openAction} /> },
  ];

  const renderCard = (user: UserListItem) => (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium">{fullName(user)}</p>
          <p className="text-sm break-all text-slate-600">{user.email}</p>
        </div>
        <StatusBadge isActive={user.isActive} />
      </div>
      <p className="text-sm text-slate-700">
        {tc(`roles.${user.role}`)} · {user.departmentName ?? tc('none')}
      </p>
      <UserActions user={user} onEdit={editUser} onAction={openAction} />
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('users.title')}</h1>
        <Button onClick={() => setForm({ mode: 'create' })}>{t('users.new')}</Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <TextField
          type="search"
          label={t('users.searchLabel')}
          placeholder={t('users.searchPlaceholder')}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            setPage(FIRST_PAGE);
          }}
        />
        <SelectField
          label={t('users.statusFilter')}
          value={status}
          options={STATUS_FILTERS.map((value) => ({ value, label: tc(`status.${value}`) }))}
          onChange={(event) => {
            setStatus(event.target.value as StatusFilter);
            setPage(FIRST_PAGE);
          }}
        />
        <SelectField
          label={t('users.roleFilter')}
          value={role}
          options={[{ value: ALL_ROLES, label: tc('roles.all') }, ...USER_ROLES.map((value) => ({ value, label: tc(`roles.${value}`) }))]}
          onChange={(event) => {
            setRole(event.target.value as UserRole | typeof ALL_ROLES);
            setPage(FIRST_PAGE);
          }}
        />
      </div>

      {users.isPending && <LoadingState />}
      {users.isError && <ErrorState error={users.error} onRetry={() => void users.refetch()} />}
      {users.data && users.data.items.length === 0 && <EmptyState />}
      {users.data && users.data.items.length > 0 && (
        <>
          <ResponsiveList items={users.data.items} columns={columns} getKey={(user) => user.id} renderCard={renderCard} caption={t('users.listCaption')} />
          <Pagination page={users.data.page} pageSize={users.data.pageSize} totalCount={users.data.totalCount} onPageChange={setPage} />
        </>
      )}

      {form !== null && (
        <UserFormDialog
          key={form.mode === 'edit' ? form.user.id : 'new'}
          user={form.mode === 'edit' ? form.user : null}
          onClose={() => setForm(null)}
          onCreated={(password) => {
            setForm(null);
            setTemporaryPassword(password);
          }}
        />
      )}
      {pending !== null && confirmText !== null && (
        <ConfirmDialog
          title={confirmText.label}
          message={confirmText.message}
          confirmLabel={tc('actions.confirm')}
          variant={pending.kind === 'deactivate' ? 'danger' : 'primary'}
          isPending={setActive.isPending || resetPassword.isPending}
          error={setActive.error ?? resetPassword.error}
          onConfirm={confirmAction}
          onCancel={() => setPending(null)}
        />
      )}
      {temporaryPassword !== null && <TemporaryPasswordDialog password={temporaryPassword} onClose={() => setTemporaryPassword(null)} />}
    </section>
  );
}
