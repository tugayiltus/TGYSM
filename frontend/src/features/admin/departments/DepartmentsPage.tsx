/**
 * @input Nothing; search and page come from the URL query.
 * @output The department list with approver and status, create, edit and (de)activate.
 * @position `/admin/departments`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Pagination } from '@astryxdesign/core/Pagination';
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

import { useDepartments, useSetDepartmentActive, type DepartmentItem } from './api';
import { DepartmentFormDialog } from './DepartmentFormDialog';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const NO_FILTERS = [] as const;

type FormState = { department: DepartmentItem | null } | null;

export function DepartmentsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const list = useListParams(NO_FILTERS);
  const [form, setForm] = useState<FormState>(null);
  const [toggling, setToggling] = useState<DepartmentItem | null>(null);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const departments = useDepartments({ search, isActive: undefined, page: list.page, pageSize: PAGE_SIZE });
  const setActive = useSetDepartmentActive();

  const actions = (d: DepartmentItem) => (
    <RecordActions
      name={d.name}
      isActive={d.isActive}
      onEdit={() => setForm({ department: d })}
      onToggle={() => {
        setActive.reset();
        setToggling(d);
      }}
    />
  );
  const columns: DataColumn<DepartmentItem>[] = [
    { key: 'name', header: t('departments.columns.name'), renderCell: (d) => <Text type="label">{d.name}</Text> },
    { key: 'approver', header: t('departments.columns.approver'), renderCell: (d) => d.approverName ?? tc('none') },
    { key: 'status', header: t('departments.columns.status'), renderCell: (d) => <ActiveToken isActive={d.isActive} /> },
    { key: 'actions', header: t('departments.columns.actions'), renderCell: actions },
  ];
  const renderCard = (d: DepartmentItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <VStack gap={0}>
          <Text type="label" wordBreak="break-word">
            {d.name}
          </Text>
          <Text type="supporting">{d.approverName ?? tc('none')}</Text>
        </VStack>
        <ActiveToken isActive={d.isActive} />
      </HStack>
      {actions(d)}
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminDepartments.icon}
        titleOverride={t('departments.title')}
        actions={<Button label={t('departments.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ department: null })} />}
      />
      <ListToolbar title={t('departments.title')} search={list.search} searchLabel={t('departments.searchLabel')} onSearchChange={list.setSearch} activeCount={list.activeCount} onClear={list.clear} />
      <Card padding={0}>
        {departments.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {departments.isError && <ErrorState error={departments.error} onRetry={() => void departments.refetch()} />}
        {departments.data && departments.data.items.length === 0 && <EmptyState />}
        {departments.data && departments.data.items.length > 0 && (
          <DataList items={departments.data.items} columns={columns} getKey={(d) => d.id} renderCard={renderCard} caption={t('departments.listCaption')} />
        )}
      </Card>
      {departments.data && departments.data.items.length > 0 && (
        <Pagination page={departments.data.page} pageSize={departments.data.pageSize} totalItems={departments.data.totalCount} onChange={list.setPage} />
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
          onConfirm={() => setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })}
          onCancel={() => setToggling(null)}
        />
      )}
    </VStack>
  );
}
