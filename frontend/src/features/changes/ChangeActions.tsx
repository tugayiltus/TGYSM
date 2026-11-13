/**
 * @input A change.
 * @output The work actions the change's state allows: one primary step, the rest secondary, cancel apart.
 * @position Change detail page, first in the side column.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { useIsFetching } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { SectionCard } from '../../shared/page/SectionCard';

import { changeKeys, useChangeAction, type ChangeActionKind, type ChangeDetail } from './api';
import { ChangeActionDialog, OpenIncidentButton, type ChangeDialogKind } from './ChangeActionDialog';
import { ChangeFormDialog } from './ChangeFormDialog';

type DirectKind = Exclude<ChangeActionKind, ChangeDialogKind>;

interface Available {
  /** The one primary step, or null. */
  primary: DirectKind | ChangeDialogKind | null;
  direct: DirectKind[];
  dialogs: ChangeDialogKind[];
  canEdit: boolean;
}

const NONE: Available = { primary: null, direct: [], dialogs: [], canEdit: false };

/** Mirrors the domain state machine so only allowed actions show (the server still decides). */
function availableActions(change: ChangeDetail): Available {
  if (!change.canWork) {
    return NONE;
  }

  const backoutStarted = change.backoutStartedAt !== null;
  switch (change.status) {
    case 'draft':
      return { primary: 'submit', direct: ['submit'], dialogs: ['cancel'], canEdit: true };
    case 'pending_approval':
      return { primary: null, direct: ['withdraw'], dialogs: ['cancel'], canEdit: true };
    case 'scheduled':
      return { primary: 'start-implementation', direct: ['start-implementation'], dialogs: ['cancel'], canEdit: true };
    case 'implementing':
      return {
        primary: backoutStarted ? 'close' : 'finish-implementation',
        direct: backoutStarted ? [] : ['finish-implementation', 'start-backout'],
        dialogs: backoutStarted ? ['close'] : [],
        canEdit: false,
      };
    case 'verifying':
      return { primary: 'close', direct: backoutStarted ? [] : ['start-backout'], dialogs: ['close'], canEdit: false };
    default:
      return NONE;
  }
}

export function ChangeActions({ change }: { change: ChangeDetail }) {
  const { t } = useTranslation('changes');
  const direct = useChangeAction(change);
  const [dialog, setDialog] = useState<ChangeDialogKind | null>(null);
  const [editing, setEditing] = useState(false);
  const actions = availableActions(change);
  // Every action sends the version shown here; wait for a refetch so a dialog does not open with an old version.
  // A running action changes the version too, so the other buttons wait for its answer.
  const busy = useIsFetching({ queryKey: changeKeys.detail(change.number) }) > 0 || direct.isPending;
  const showIncident = change.status === 'closed' && change.outcome === 'failed';
  if (actions.direct.length === 0 && actions.dialogs.length === 0 && !actions.canEdit && !showIncident) {
    return null;
  }

  const variantOf = (kind: ChangeActionKind) => (kind === actions.primary ? 'primary' : kind === 'cancel' ? 'destructive' : 'secondary');
  return (
    <SectionCard title={t('actions.title')}>
      <FormAlert message={direct.error ? describeError(direct.error) : null} />
      <HStack gap={2} wrap="wrap">
        {actions.direct.map((kind) => (
          <Button
            key={kind}
            label={t(`actions.${kind}`)}
            variant={variantOf(kind)}
            isDisabled={busy}
            isLoading={direct.isPending && direct.variables?.kind === kind}
            onClick={() => direct.mutate({ kind })}
          />
        ))}
        {actions.dialogs.map((kind) => (
          <Button
            key={kind}
            label={t(`actions.${kind}`)}
            variant={variantOf(kind)}
            isDisabled={busy}
            onClick={() => {
              direct.reset();
              setDialog(kind);
            }}
          />
        ))}
        {actions.canEdit && <Button label={t('actions.edit')} variant="secondary" isDisabled={busy} onClick={() => setEditing(true)} />}
        {showIncident && <OpenIncidentButton />}
      </HStack>
      {dialog !== null && <ChangeActionDialog key={dialog} change={change} kind={dialog} onClose={() => setDialog(null)} />}
      {editing && <ChangeFormDialog change={change} onSaved={() => setEditing(false)} onClose={() => setEditing(false)} />}
    </SectionCard>
  );
}
