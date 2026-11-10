/**
 * @input A team id and name, and a close callback.
 * @output A dialog to add a member from the candidates and to remove members.
 * @position Opened from the team list.
 */
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { List, ListItem } from '@astryxdesign/core/List';
import { Selector } from '@astryxdesign/core/Selector';
import { VStack } from '@astryxdesign/core/VStack';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { FormAlert } from '../../../shared/feedback/FormAlert';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { useTeamCandidateOptions } from '../shared/referenceOptions';

import { useAddTeamMember, useRemoveTeamMember, useTeam } from './api';

const NO_SELECTION = '';
const DIALOG_WIDTH = 560;
const META_SEPARATOR = ' · ';

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
  const close = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };

  return (
    <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH}>
      <Layout
        header={<DialogHeader title={t('teams.membersTitle', { name: teamName })} onOpenChange={close} />}
        content={
          <LayoutContent>
            <VStack gap={4}>
              <FormAlert message={error ? describeError(error) : null} />
              <form onSubmit={addSelected}>
                <HStack gap={2} vAlign="end" wrap="wrap">
                  <Selector label={t('teams.memberToAdd')} value={selected} options={[...options]} placeholder={tc('none')} onChange={setSelected} />
                  <Button type="submit" label={t('teams.addMember')} variant="primary" isDisabled={selected === NO_SELECTION} isLoading={add.isPending} />
                </HStack>
              </form>
              {team.isPending && <LoadingState />}
              {team.isError && <ErrorState error={team.error} onRetry={() => void team.refetch()} />}
              {team.data && team.data.members.length === 0 && <EmptyState message={t('teams.noMembers')} />}
              {team.data && team.data.members.length > 0 && (
                <List hasDividers density="compact" aria-label={t('teams.membersTitle', { name: teamName })}>
                  {team.data.members.map((member) => {
                    const fullName = `${member.firstName} ${member.lastName}`;
                    return (
                      <ListItem
                        key={member.userId}
                        label={fullName}
                        description={`${member.email}${META_SEPARATOR}${tc(`roles.${member.role}`)}`}
                        endContent={
                          <Button
                            label={t('teams.removeMember')}
                            aria-label={recordActionLabel(t('teams.removeMember'), fullName)}
                            size="sm"
                            variant="ghost"
                            isLoading={remove.isPending && remove.variables?.userId === member.userId}
                            isDisabled={remove.isPending}
                            onClick={() => {
                              add.reset();
                              remove.mutate({ teamId, userId: member.userId });
                            }}
                          />
                        }
                      />
                    );
                  })}
                </List>
              )}
            </VStack>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack hAlign="end">
              <Button label={tc('actions.close')} variant="primary" onClick={onClose} />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
  );
}
