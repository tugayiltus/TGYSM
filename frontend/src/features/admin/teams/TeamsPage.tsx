/**
 * @input Nothing; search and page come from the URL query.
 * @output The team list with member count, status and intake marker; create, edit, members and (de)activate.
 * @position `/admin/teams`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
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

import { useSetTeamActive, useTeams, type TeamItem } from './api';
import { TeamFormDialog } from './TeamFormDialog';
import { TeamMembersDialog } from './TeamMembersDialog';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const NO_FILTERS = [] as const;

/** Active state plus the intake marker, rendered once per view (UX-08). */
function TeamTokens({ team }: { team: TeamItem }) {
  const { t } = useTranslation('admin');
  return (
    <HStack gap={1} wrap="wrap">
      <ActiveToken isActive={team.isActive} />
      {team.isDefaultIntake && <Token size="sm" color="blue" label={t('teams.intakeBadge')} />}
    </HStack>
  );
}

export function TeamsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const list = useListParams(NO_FILTERS);
  const [form, setForm] = useState<{ team: TeamItem | null } | null>(null);
  const [membersOf, setMembersOf] = useState<TeamItem | null>(null);
  const [toggling, setToggling] = useState<TeamItem | null>(null);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const teams = useTeams({ search, page: list.page, pageSize: PAGE_SIZE });
  const setActive = useSetTeamActive();

  const actions = (team: TeamItem) => (
    <RecordActions
      name={team.name}
      isActive={team.isActive}
      onEdit={() => setForm({ team })}
      onToggle={() => {
        setActive.reset();
        setToggling(team);
      }}
      extra={
        <Button
          label={t('teams.manageMembers')}
          aria-label={recordActionLabel(t('teams.manageMembers'), team.name)}
          size="sm"
          variant="secondary"
          onClick={() => setMembersOf(team)}
        />
      }
    />
  );
  const columns: DataColumn<TeamItem>[] = [
    { key: 'name', header: t('teams.columns.name'), renderCell: (team) => <Text type="label">{team.name}</Text> },
    { key: 'members', header: t('teams.columns.members'), renderCell: (team) => t('teams.memberCount', { count: team.memberCount }) },
    { key: 'status', header: t('teams.columns.status'), renderCell: (team) => <TeamTokens team={team} /> },
    { key: 'actions', header: t('teams.columns.actions'), renderCell: actions },
  ];
  const renderCard = (team: TeamItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <VStack gap={0}>
          <Text type="label" wordBreak="break-word">
            {team.name}
          </Text>
          <Text type="supporting">{t('teams.memberCount', { count: team.memberCount })}</Text>
        </VStack>
        <TeamTokens team={team} />
      </HStack>
      {actions(team)}
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminTeams.icon}
        titleOverride={t('teams.title')}
        actions={<Button label={t('teams.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setForm({ team: null })} />}
      />
      <ListToolbar title={t('teams.title')} search={list.search} searchLabel={t('teams.searchLabel')} onSearchChange={list.setSearch} activeCount={list.activeCount} onClear={list.clear} />
      <Card padding={0}>
        {teams.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {teams.isError && <ErrorState error={teams.error} onRetry={() => void teams.refetch()} />}
        {teams.data && teams.data.items.length === 0 && <EmptyState />}
        {teams.data && teams.data.items.length > 0 && (
          <DataList items={teams.data.items} columns={columns} getKey={(team) => team.id} renderCard={renderCard} caption={t('teams.listCaption')} />
        )}
      </Card>
      {teams.data && teams.data.items.length > 0 && <Pagination page={teams.data.page} pageSize={teams.data.pageSize} totalItems={teams.data.totalCount} onChange={list.setPage} />}

      {form !== null && <TeamFormDialog key={form.team?.id ?? 'new'} team={form.team} onClose={() => setForm(null)} />}
      {membersOf !== null && <TeamMembersDialog key={membersOf.id} teamId={membersOf.id} teamName={membersOf.name} onClose={() => setMembersOf(null)} />}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('teams.confirmDeactivate', { name: toggling.name }) : t('teams.confirmReactivate', { name: toggling.name })}
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
