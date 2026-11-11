/**
 * @input Nothing; search, approval and status come from the URL query.
 * @output The request type list with create, edit and (de)activate.
 * @position `/admin/request-types`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { FormSwitch, FormTextInput } from '../../../shared/form/formFields';
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

import { useRequestTypes, useSaveRequestType, useSetRequestTypeActive, type RequestTypeItem } from './api';

/** Mirrors backend RequestType.NameMaxLength. */
const NAME_MAX_LENGTH = 150;
const SKELETON_ROWS = 5;
const APPROVAL_FILTER_KEY = 'approval';
const FILTER_KEYS = [APPROVAL_FILTER_KEY, STATUS_FILTER_KEY] as const;

type ApprovalFilter = 'required' | 'none';
const APPROVAL_FILTERS: readonly ApprovalFilter[] = ['required', 'none'];
const readApproval = (value: string): ApprovalFilter | undefined => APPROVAL_FILTERS.find((approval) => approval === value);

const schema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  requiresApproval: z.boolean(),
});
type Values = z.infer<typeof schema>;

const FORM_FIELDS = ['name', 'requiresApproval'] as const;
const CODE_FIELDS = { [ERROR_CODES.requestTypeNameTaken]: 'name' } as const;

function RequestTypeFormDialog({ type, onClose }: { type: RequestTypeItem | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const save = useSaveRequestType();
  const { control, handleSubmit, setError } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: type?.name ?? '', requiresApproval: type?.requiresApproval ?? false },
  });
  const onSubmit = handleSubmit((v) =>
    save.mutate(
      { id: type?.id ?? null, version: type?.version ?? 0, name: v.name, requiresApproval: v.requiresApproval },
      { onSuccess: onClose, onError: (error) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) },
    ),
  );
  const alert = save.error && !isHandledByFields(save.error, FORM_FIELDS, CODE_FIELDS) ? describeError(save.error) : null;

  return (
    <FormDialog
      title={type === null ? t('requestTypes.new') : t('requestTypes.edit')}
      submitLabel={type === null ? tc('actions.create') : tc('actions.save')}
      isPending={save.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="name" label={t('requestTypes.fields.name')} autoComplete="off" isRequired />
      <FormSwitch control={control} name="requiresApproval" label={t('requestTypes.fields.requiresApproval')} />
    </FormDialog>
  );
}

export function RequestTypesPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const list = useListParams(FILTER_KEYS);
  const approval = readApproval(list.filters.approval);
  const status = readStatusFilter(list.filters.status);
  const types = useRequestTypes();
  const setActive = useSetRequestTypeActive();
  const [form, setForm] = useState<{ type: RequestTypeItem | null } | null>(null);
  const [toggling, setToggling] = useState<RequestTypeItem | null>(null);
  const rows =
    types.data === undefined
      ? undefined
      : filterRows(types.data, list.search, status, (r) => r.name).filter((r) => approval === undefined || r.requiresApproval === (approval === 'required'));

  const approvalText = (r: RequestTypeItem) => (r.requiresApproval ? t('requestTypes.approval') : t('requestTypes.noApproval'));
  const actions = (r: RequestTypeItem) => (
    <RecordActions
      name={r.name}
      isActive={r.isActive}
      onEdit={() => setForm({ type: r })}
      onToggle={() => {
        setActive.reset();
        setToggling(r);
      }}
    />
  );
  const columns: DataColumn<RequestTypeItem>[] = [
    { key: 'name', header: t('requestTypes.columns.name'), renderCell: (r) => <Text type="label">{r.name}</Text> },
    { key: 'approval', header: t('requestTypes.columns.approval'), renderCell: approvalText },
    { key: 'status', header: t('requestTypes.columns.status'), renderCell: (r) => <ActiveToken isActive={r.isActive} /> },
    { key: 'actions', header: t('requestTypes.columns.actions'), renderCell: actions },
  ];
  const renderCard = (r: RequestTypeItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <VStack gap={0}>
          <Text type="label" wordBreak="break-word">
            {r.name}
          </Text>
          <Text type="supporting">{approvalText(r)}</Text>
        </VStack>
        <ActiveToken isActive={r.isActive} />
      </HStack>
      {actions(r)}
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminRequestTypes.icon}
        titleOverride={t('requestTypes.title')}
        actions={<Button label={t('requestTypes.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ type: null })} />}
      />
      <ListToolbar
        title={t('requestTypes.title')}
        search={list.search}
        searchLabel={t('requestTypes.searchLabel')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('requestTypes.approvalFilter')}
              placeholder={tc('status.all')}
              value={approval ?? ''}
              options={APPROVAL_FILTERS.map((value) => ({ value, label: value === 'required' ? t('requestTypes.approval') : t('requestTypes.noApproval') }))}
              onChange={(value) => list.setFilter(APPROVAL_FILTER_KEY, value)}
            />
            <StatusFilterSelector value={status} onChange={(value) => list.setFilter(STATUS_FILTER_KEY, value)} />
          </>
        }
      />
      <Card padding={0}>
        {types.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {types.isError && <ErrorState error={types.error} onRetry={() => void types.refetch()} />}
        {rows?.length === 0 && <EmptyState />}
        {rows !== undefined && rows.length > 0 && <DataList items={rows} columns={columns} getKey={(r) => r.id} renderCard={renderCard} caption={t('requestTypes.listCaption')} />}
      </Card>
      {form !== null && <RequestTypeFormDialog key={form.type?.id ?? 'new'} type={form.type} onClose={() => setForm(null)} />}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('requestTypes.confirmDeactivate', { name: toggling.name }) : t('requestTypes.confirmReactivate', { name: toggling.name })}
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
