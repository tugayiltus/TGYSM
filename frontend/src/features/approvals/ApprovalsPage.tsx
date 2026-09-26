import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { formatDateTime } from '../../shared/lib/format';
import { ticketPath } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { Dialog } from '../../shared/ui/Dialog';
import { FormAlert } from '../../shared/ui/FormAlert';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { SelectField } from '../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';
import { TextField } from '../../shared/ui/TextField';
import { useAdminOptions } from '../admin/shared/referenceOptions';
import { useDecideApproval, useMyApprovals, useReassignApprover, type MyApprovalItem } from '../tickets/api';

type Pending = { item: MyApprovalItem; kind: 'approve' | 'reject' | 'reassign' };

function DecisionDialog({ pending, onClose }: { pending: Pending; onClose: () => void }) {
  const { t } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const decide = useDecideApproval();
  const reassign = useReassignApprover();
  const admins = useAdminOptions();
  const [reason, setReason] = useState('');
  const [approverId, setApproverId] = useState('');
  const { item, kind } = pending;
  const needsReason = kind !== 'approve';
  const mutation = kind === 'reassign' ? reassign : decide;
  const confirm = () => {
    if (kind === 'reassign') {
      reassign.mutate({ number: item.ticketNumber, approverId, reason: reason.trim() }, { onSuccess: onClose });
    } else if (kind === 'approve') {
      decide.mutate({ id: item.id, version: item.version, approve: true }, { onSuccess: onClose });
    } else {
      decide.mutate({ id: item.id, version: item.version, approve: false, reason: reason.trim() }, { onSuccess: onClose });
    }
  };
  const disabled = (needsReason && reason.trim() === '') || (kind === 'reassign' && approverId === '');

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={`${t(`approvals.${kind}`)} · ${item.ticketNumber}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button variant={kind === 'reject' ? 'danger' : 'primary'} disabled={disabled} isLoading={mutation.isPending} onClick={confirm}>
            {tc('actions.confirm')}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <FormAlert message={mutation.error ? describeError(mutation.error) : null} />
        <p>{item.subject}</p>
        {kind === 'reassign' && (
          <SelectField
            label={t('approvals.approver')}
            placeholder={t('fields.select')}
            options={admins}
            value={approverId}
            onChange={(event) => setApproverId(event.target.value)}
          />
        )}
        {needsReason && <TextField label={t('approvals.reason')} autoComplete="off" value={reason} onChange={(event) => setReason(event.target.value)} />}
      </div>
    </Dialog>
  );
}

export function ApprovalsPage() {
  const { t } = useTranslation('tickets');
  const approvals = useMyApprovals();
  const [pending, setPending] = useState<Pending | null>(null);

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('approvals.title')}</h1>
      {approvals.isPending && <LoadingState />}
      {approvals.isError && <ErrorState error={approvals.error} onRetry={() => void approvals.refetch()} />}
      {approvals.data && approvals.data.length === 0 && <EmptyState message={t('approvals.empty')} />}
      {approvals.data && approvals.data.length > 0 && (
        <ul className="flex flex-col gap-3">
          {approvals.data.map((item) => (
            <li key={item.id} className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4">
              <div>
                <Link to={ticketPath(item.ticketNumber)} className="text-sm text-brand-900 underline">
                  {item.ticketNumber}
                </Link>
                <p className="font-medium break-words">{item.subject}</p>
                <p className="text-sm text-slate-600">{t('approvals.requestedBy', { name: item.requesterName })}</p>
                {item.requestTypeName && <p className="text-sm text-slate-600">{item.requestTypeName}</p>}
                <p className="text-xs text-slate-600">{formatDateTime(item.createdAt)}</p>
              </div>
              <div className="flex flex-wrap gap-2">
                {item.approverId === null ? (
                  <Button variant="secondary" aria-label={recordActionLabel(t('approvals.reassign'), item.ticketNumber)} onClick={() => setPending({ item, kind: 'reassign' })}>
                    {t('approvals.reassign')}
                  </Button>
                ) : (
                  <>
                    <Button aria-label={recordActionLabel(t('approvals.approve'), item.ticketNumber)} onClick={() => setPending({ item, kind: 'approve' })}>
                      {t('approvals.approve')}
                    </Button>
                    <Button variant="danger" aria-label={recordActionLabel(t('approvals.reject'), item.ticketNumber)} onClick={() => setPending({ item, kind: 'reject' })}>
                      {t('approvals.reject')}
                    </Button>
                  </>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
      {pending !== null && <DecisionDialog key={`${pending.item.id}-${pending.kind}`} pending={pending} onClose={() => setPending(null)} />}
    </section>
  );
}
