/**
 * @input Nothing; search, status, role and page come from the URL query.
 * @output The user list with create, edit, (de)activate and password reset; a credential notice after create or reset.
 * @position `/admin/users`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { ListToolbar } from '../../../shared/list/ListToolbar';
import { useListParams } from '../../../shared/list/useListParams';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { ActiveToken } from '../../../shared/page/ActiveToken';
import { RecordActions } from '../../../shared/page/RecordActions';
import { Icons } from '../../../shared/ui/icons';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';

import { useResetUserPassword, useSetUserActive, useUsers, type CredentialDelivery, type UserListItem, type UserRole } from './api';
import { LinkSentDialog, ResetPasswordDialog } from './CredentialNoticeDialogs';
import { TemporaryPasswordDialog } from './TemporaryPasswordDialog';
import { UserFormDialog } from './UserFormDialog';
import { USER_ROLES } from './userSchema';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['status', 'role'] as const;
const META_SEPARATOR = ' · ';

type StatusFilter = 'active' | 'inactive';
const STATUS_FILTERS: readonly StatusFilter[] = ['active', 'inactive'];
const STATUS_TO_IS_ACTIVE: Record<StatusFilter, boolean> = { active: true, inactive: false };

const pick = <T extends string>(allowed: readonly T[], value: string): T | undefined => allowed.find((item) => item === value);

type PendingAction = { kind: 'deactivate' | 'reactivate' | 'reset'; user: UserListItem };
type CredentialNotice = { kind: 'password'; password: string } | { kind: 'link'; email: string };
type FormState = { mode: 'create' } | { mode: 'edit'; user: UserListItem } | null;

const fullName = (user: UserListItem) => `${user.firstName} ${user.lastName}`;

function UserActions({ user, onEdit, onAction }: { user: UserListItem; onEdit: (user: UserListItem) => void; onAction: (action: PendingAction) => void }) {
  const { t } = useTranslation('admin');
  return (
    <RecordActions
      name={fullName(user)}
      isActive={user.isActive}
      onEdit={() => onEdit(user)}
      onToggle={() => onAction({ kind: user.isActive ? 'deactivate' : 'reactivate', user })}
      extra={
        <Button
          label={t('users.resetPassword')}
          aria-label={recordActionLabel(t('users.resetPassword'), fullName(user))}
          size="sm"
          variant="ghost"
          onClick={() => onAction({ kind: 'reset', user })}
        />
      }
    />
  );
}

export function UsersPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const list = useListParams(FILTER_KEYS);
  const status = pick(STATUS_FILTERS, list.filters.status);
  const role: UserRole | undefined = pick(USER_ROLES, list.filters.role);
  const page = list.page;
  const [form, setForm] = useState<FormState>(null);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [notice, setNotice] = useState<CredentialNotice | null>(null);
  const debouncedSearch = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const users = useUsers({
    search: debouncedSearch,
    isActive: status === undefined ? undefined : STATUS_TO_IS_ACTIVE[status],
    role,
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
  const showCredential = (email: string, temporaryPassword: string | null) =>
    setNotice(temporaryPassword === null ? { kind: 'link', email } : { kind: 'password', password: temporaryPassword });
  const confirmReset = (user: UserListItem, delivery: CredentialDelivery) =>
    resetPassword.mutate(
      { id: user.id, delivery },
      {
        onSuccess: (result) => {
          setPending(null);
          showCredential(user.email, result.temporaryPassword);
        },
      },
    );
  const confirmAction = () => {
    if (pending === null || pending.kind === 'reset') {
      return;
    }
    const leavesFilter = (pending.kind === 'deactivate' && status === 'active') || (pending.kind === 'reactivate' && status === 'inactive');
    const wasLastOnPage = users.data?.items.length === 1 && page > FIRST_PAGE;
    setActive.mutate(
      { id: pending.user.id, version: pending.user.version, isActive: pending.kind === 'reactivate' },
      {
        onSuccess: () => {
          setPending(null);
          // The row leaves the filtered list; do not strand the admin on an empty page.
          if (leavesFilter && wasLastOnPage) {
            list.setPage(page - 1);
          }
        },
      },
    );
  };

  const confirmText = pending === null || pending.kind === 'reset' ? null : {
    deactivate: { message: t('users.confirmDeactivate', { name: fullName(pending.user) }), label: tc('actions.deactivate') },
    reactivate: { message: t('users.confirmReactivate', { name: fullName(pending.user) }), label: tc('actions.reactivate') },
  }[pending.kind];

  const columns: DataColumn<UserListItem>[] = [
    { key: 'name', header: t('users.columns.name'), renderCell: (user) => <Text type="label">{fullName(user)}</Text> },
    { key: 'email', header: t('users.columns.email'), renderCell: (user) => <Text wordBreak="break-word">{user.email}</Text> },
    { key: 'role', header: t('users.columns.role'), renderCell: (user) => tc(`roles.${user.role}`) },
    { key: 'department', header: t('users.columns.department'), renderCell: (user) => user.departmentName ?? tc('none') },
    { key: 'status', header: t('users.columns.status'), renderCell: (user) => <ActiveToken isActive={user.isActive} /> },
    { key: 'actions', header: t('users.columns.actions'), renderCell: (user) => <UserActions user={user} onEdit={editUser} onAction={openAction} /> },
  ];

  const renderCard = (user: UserListItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <VStack gap={0}>
          <Text type="label">{fullName(user)}</Text>
          <Text type="supporting" wordBreak="break-word">
            {user.email}
          </Text>
        </VStack>
        <ActiveToken isActive={user.isActive} />
      </HStack>
      <Text type="supporting">{`${tc(`roles.${user.role}`)}${META_SEPARATOR}${user.departmentName ?? tc('none')}`}</Text>
      <UserActions user={user} onEdit={editUser} onAction={openAction} />
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminUsers.icon}
        titleOverride={t('users.title')}
        actions={<Button label={t('users.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ mode: 'create' })} />}
      />
      <ListToolbar
        title={t('users.title')}
        search={list.search}
        searchLabel={t('users.searchLabel')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('users.statusFilter')}
              placeholder={tc('status.all')}
              value={status ?? ''}
              options={STATUS_FILTERS.map((value) => ({ value, label: tc(`status.${value}`) }))}
              onChange={(value) => list.setFilter('status', value)}
            />
            <Selector
              label={t('users.roleFilter')}
              placeholder={tc('roles.all')}
              value={role ?? ''}
              options={USER_ROLES.map((value) => ({ value, label: tc(`roles.${value}`) }))}
              onChange={(value) => list.setFilter('role', value)}
            />
          </>
        }
      />
      <Card padding={0}>
        {users.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {users.isError && <ErrorState error={users.error} onRetry={() => void users.refetch()} />}
        {users.data && users.data.items.length === 0 && <EmptyState />}
        {users.data && users.data.items.length > 0 && (
          <DataList items={users.data.items} columns={columns} getKey={(user) => user.id} renderCard={renderCard} caption={t('users.listCaption')} />
        )}
      </Card>
      {users.data && users.data.items.length > 0 && <Pagination page={users.data.page} pageSize={users.data.pageSize} totalItems={users.data.totalCount} onChange={list.setPage} />}

      {form !== null && (
        <UserFormDialog
          key={form.mode === 'edit' ? form.user.id : 'new'}
          user={form.mode === 'edit' ? form.user : null}
          onClose={() => setForm(null)}
          onCreated={({ email, temporaryPassword }) => {
            setForm(null);
            showCredential(email, temporaryPassword);
          }}
        />
      )}
      {pending !== null && confirmText !== null && (
        <ConfirmDialog
          title={confirmText.label}
          message={confirmText.message}
          confirmLabel={tc('actions.confirm')}
          variant={pending.kind === 'deactivate' ? 'danger' : 'primary'}
          isPending={setActive.isPending}
          error={setActive.error}
          onConfirm={confirmAction}
          onCancel={() => setPending(null)}
        />
      )}
      {pending?.kind === 'reset' && (
        <ResetPasswordDialog name={fullName(pending.user)} isPending={resetPassword.isPending} error={resetPassword.error} onConfirm={(delivery) => confirmReset(pending.user, delivery)} onCancel={() => setPending(null)} />
      )}
      {notice?.kind === 'password' && <TemporaryPasswordDialog password={notice.password} onClose={() => setNotice(null)} />}
      {notice?.kind === 'link' && <LinkSentDialog email={notice.email} onClose={() => setNotice(null)} />}
    </VStack>
  );
}
