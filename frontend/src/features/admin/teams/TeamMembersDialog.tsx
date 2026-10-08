import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { Button } from '../../../shared/ui/Button';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { useTeamCandidateOptions } from '../shared/referenceOptions';

import { useAddTeamMember, useRemoveTeamMember, useTeam } from './api';

const NO_SELECTION = '';

export function TeamMembersDialog({ teamId, teamName, onClose }: { teamId: string; teamName: string; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const team = useTeam(teamId);
  const candidates = useTeamCandidateOptions();
  const add = useAddTeamMember();
  const remove = useRemoveTeamMember();
  const [selected, setSelected] = useState(NO_SELECTION);

  const memberIds = new Set(team.data?.members.map((member) => member.userId) ?? []);
  const options = candidates.filter((option) => !memberIds.has(option.value));
  const error = add.error ?? remove.error;

  const addSelected = (event: FormEvent) => {
    event.preventDefault();
    if (selected === NO_SELECTION) {
      return;
    }
    remove.reset();
    add.mutate({ teamId, userId: selected }, { onSuccess: () => setSelected(NO_SELECTION) });
  };

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t('teams.membersTitle', { name: teamName })}
      footer={<Button onClick={onClose}>{tc('actions.close')}</Button>}
    >
      <div className="d-flex flex-column gap-3">
        <FormAlert message={error ? describeError(error) : null} />
        <form className="d-flex flex-column gap-2 flex-sm-row" onSubmit={addSelected}>
          <SelectField
            className="flex-grow-1"
            label={t('teams.memberToAdd')}
            value={selected}
            options={options}
            placeholder={tc('none')}
            onChange={(event) => setSelected(event.target.value)}
          />
          <Button type="submit" disabled={selected === NO_SELECTION} isLoading={add.isPending}>
            {t('teams.addMember')}
          </Button>
        </form>

        {team.isPending && <LoadingState />}
        {team.isError && <ErrorState error={team.error} onRetry={() => void team.refetch()} />}
        {team.data && team.data.members.length === 0 && <EmptyState message={t('teams.noMembers')} />}
        {team.data && team.data.members.length > 0 && (
          <ul className="list-unstyled mb-0 rounded border">
            {team.data.members.map((member) => (
              <li key={member.userId} className="d-flex align-items-center justify-content-between gap-2 p-2">
                <div className="min-w-0">
                  <p className="mb-0 fw-medium">{`${member.firstName} ${member.lastName}`}</p>
                  <p className="mb-0 small text-break text-body-secondary">
                    {member.email} · {tc(`roles.${member.role}`)}
                  </p>
                </div>
                <Button aria-label={recordActionLabel(t('teams.removeMember'), `${member.firstName} ${member.lastName}`)}
                  variant="ghost"
                  isLoading={remove.isPending && remove.variables?.userId === member.userId}
                  disabled={remove.isPending}
                  onClick={() => {
                    add.reset();
                    remove.mutate({ teamId, userId: member.userId });
                  }}
                >
                  {t('teams.removeMember')}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Dialog>
  );
}
