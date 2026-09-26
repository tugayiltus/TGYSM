import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';

import { useTicketAction, type TicketDetail } from './api';
import { TicketActionDialog, type DialogActionKind } from './TicketActionDialog';

type DirectKind = 'claim' | 'start' | 'resume';

const isClosed = (t: TicketDetail) => t.status === 'completed' || t.status === 'cancelled';

/** Mirrors the domain state machine so only allowed actions show (the server still decides). */
function availableActions(t: TicketDetail): { direct: DirectKind[]; dialogs: DialogActionKind[] } {
  if (!t.canWork || isClosed(t)) {
    return { direct: [], dialogs: [] };
  }
  const direct: DirectKind[] = [];
  if (t.assigneeId === null) {
    direct.push('claim');
  }
  if (t.status === 'open') {
    direct.push('start');
  }
  if (t.status === 'on_hold' && t.holdReason !== 'manager_approval') {
    direct.push('resume');
  }
  const dialogs: DialogActionKind[] = [];
  if (t.status === 'open' || t.status === 'in_progress') {
    dialogs.push('hold', 'complete');
  }
  dialogs.push('priority', 'transfer');
  if (t.kind === 'request' && t.approval?.status === 'approved') {
    dialogs.push('invalidate');
  }
  dialogs.push('cancel');
  return { direct, dialogs };
}

export function TicketActions({ ticket }: { ticket: TicketDetail }) {
  const { t } = useTranslation('tickets');
  const direct = useTicketAction(ticket);
  const [dialog, setDialog] = useState<DialogActionKind | null>(null);
  const actions = availableActions(ticket);

  if (actions.direct.length === 0 && actions.dialogs.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="ticket-actions" className="flex flex-col gap-3">
      <h2 id="ticket-actions" className="text-lg font-semibold">
        {t('actions.title')}
      </h2>
      <FormAlert message={direct.error ? describeError(direct.error) : null} />
      <div className="flex flex-wrap gap-2">
        {actions.direct.map((kind) => (
          <Button key={kind} isLoading={direct.isPending && direct.variables?.kind === kind} onClick={() => direct.mutate({ kind })}>
            {t(`actions.${kind}`)}
          </Button>
        ))}
        {actions.dialogs.map((kind) => (
          <Button
            key={kind}
            variant={kind === 'cancel' ? 'danger' : 'secondary'}
            onClick={() => {
              direct.reset();
              setDialog(kind);
            }}
          >
            {t(`actions.${kind}`)}
          </Button>
        ))}
      </div>
      {dialog !== null && <TicketActionDialog key={dialog} ticket={ticket} kind={dialog} onClose={() => setDialog(null)} />}
    </section>
  );
}
