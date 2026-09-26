import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { Pagination } from '../../../shared/ui/Pagination';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TextField } from '../../../shared/ui/TextField';

import { useSetTeamActive, useTeams, type TeamItem } from './api';
import { TeamFormDialog } from './TeamFormDialog';
import { TeamMembersDialog } from './TeamMembersDialog';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SEARCH_DEBOUNCE_MS = 300;

interface TeamActionsProps {
  team: TeamItem;
  onEdit: (team: TeamItem) => void;
  onMembers: (team: TeamItem) => void;
  onToggle: (team: TeamItem) => void;
}

function TeamActions({ team, onEdit, onMembers, onToggle }: TeamActionsProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  return (
    <div className="flex flex-wrap gap-2">
      <Button aria-label={recordActionLabel(t('teams.manageMembers'), team.name)} variant="secondary" onClick={() => onMembers(team)}>
        {t('teams.manageMembers')}
      </Button>
      <Button aria-label={recordActionLabel(tc('actions.edit'), team.name)} variant="secondary" onClick={() => onEdit(team)}>
        {tc('actions.edit')}
      </Button>
      <Button aria-label={recordActionLabel(team.isActive ? tc('actions.deactivate') : tc('actions.reactivate'), team.name)} variant="ghost" onClick={() => onToggle(team)}>
        {team.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
      </Button>
    </div>
  );
}

export function TeamsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(FIRST_PAGE);
  const [form, setForm] = useState<{ team: TeamItem | null } | null>(null);
  const [membersOf, setMembersOf] = useState<TeamItem | null>(null);
  const [toggling, setToggling] = useState<TeamItem | null>(null);
  const debouncedSearch = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  const teams = useTeams({ search: debouncedSearch, page, pageSize: PAGE_SIZE });
  const setActive = useSetTeamActive();

  const edit = (team: TeamItem) => setForm({ team });
  const openToggle = (team: TeamItem) => {
    setActive.reset();
    setToggling(team);
  };

  const columns: Column<TeamItem>[] = [
    { key: 'name', header: t('teams.columns.name'), renderCell: (team) => <span className="font-medium">{team.name}</span> },
    { key: 'members', header: t('teams.columns.members'), renderCell: (team) => t('teams.memberCount', { count: team.memberCount }) },
    { key: 'status', header: t('teams.columns.status'), renderCell: (team) => <StatusBadge isActive={team.isActive} /> },
    { key: 'actions', header: t('teams.columns.actions'), renderCell: (team) => <TeamActions team={team} onEdit={edit} onMembers={setMembersOf} onToggle={openToggle} /> },
  ];

  const renderCard = (team: TeamItem) => (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium break-words">{team.name}</p>
          <p className="text-sm text-slate-600">{t('teams.memberCount', { count: team.memberCount })}</p>
        </div>
        <StatusBadge isActive={team.isActive} />
      </div>
      <TeamActions team={team} onEdit={edit} onMembers={setMembersOf} onToggle={openToggle} />
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('teams.title')}</h1>
        <Button onClick={() => setForm({ team: null })}>{t('teams.new')}</Button>
      </div>
      <TextField
        type="search"
        className="sm:max-w-sm"
        label={t('teams.searchLabel')}
        value={search}
        onChange={(event) => {
          setSearch(event.target.value);
          setPage(FIRST_PAGE);
        }}
      />

      {teams.isPending && <LoadingState />}
      {teams.isError && <ErrorState error={teams.error} onRetry={() => void teams.refetch()} />}
      {teams.data && teams.data.items.length === 0 && <EmptyState />}
      {teams.data && teams.data.items.length > 0 && (
        <>
          <ResponsiveList items={teams.data.items} columns={columns} getKey={(team) => team.id} renderCard={renderCard} caption={t('teams.listCaption')} />
          <Pagination page={teams.data.page} pageSize={teams.data.pageSize} totalCount={teams.data.totalCount} onPageChange={setPage} />
        </>
      )}

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
          onConfirm={() =>
            setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })
          }
          onCancel={() => setToggling(null)}
        />
      )}
    </section>
  );
}
