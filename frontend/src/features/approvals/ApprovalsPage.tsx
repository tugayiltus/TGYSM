/**
 * @input Nothing; loads the signed-in admin's open request approvals and the change approvals they may decide.
 * @output One list of both kinds, each row with a kind token and approve, reject, return or reassign actions confirmed in a dialog.
 * @position `/approvals`, inside the app shell (admins only).
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { List, ListItem } from '@astryxdesign/core/List';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { ticketPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { useAdminOptions } from '../admin/shared/referenceOptions';
import { useChangeApprovals } from '../changes/api';
import { useDecideApproval, useMyApprovals, useReassignApprover, type MyApprovalItem } from '../tickets/api';

import { ChangeApprovalDialog, type ChangeDecision } from './ChangeApprovalDialog';
import { ChangeApprovalRow } from './ChangeApprovalRow';
import { KindToken } from './KindToken';

const SKELETON_ROWS = 5;
const DIALOG_WIDTH = 480;
const META_SEPARATOR = ' · ';

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
  const close = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };

  return (
    <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH} purpose="form">
      <Layout
        header={<DialogHeader title={`${t(`approvals.${kind}`)} · ${item.ticketNumber}`} onOpenChange={close} />}
        content={
          <LayoutContent>
            <VStack gap={4}>
              <FormAlert message={mutation.error ? describeError(mutation.error) : null} />
              <Text type="body">{item.subject}</Text>
              {kind === 'reassign' && (
                <Selector label={t('approvals.approver')} placeholder={t('fields.select')} options={[...admins]} value={approverId} onChange={setApproverId} />
              )}
              {needsReason && <TextInput label={t('approvals.reason')} autoComplete="off" value={reason} onChange={setReason} />}
            </VStack>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack gap={2} hAlign="end">
              <Button label={tc('actions.cancel')} variant="secondary" onClick={onClose} />
              <Button
                label={kind === 'reassign' ? t('approvals.assignApprover') : tc('actions.confirm')}
                variant={kind === 'reject' ? 'destructive' : 'primary'}
                isDisabled={disabled}
                isLoading={mutation.isPending}
                onClick={confirm}
              />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
  );
}

function ApprovalRow({ item, onDecide }: { item: MyApprovalItem; onDecide: (pending: Pending) => void }) {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  const link = useRef<HTMLAnchorElement>(null);
  const meta = [item.ticketNumber, t('approvals.requestedBy', { name: item.requesterName }), item.requestTypeName, formatDateTime(item.createdAt)].filter(Boolean).join(META_SEPARATOR);
  const action = (kind: Pending['kind']) => () => onDecide({ item, kind });
  return (
    <ListItem
      startContent={<KindToken kind="ticket" />}
      label={
        <StateLink href={ticketPath(item.ticketNumber)} state={linkState(location)} ref={link}>
          {item.subject}
        </StateLink>
      }
      description={meta}
      interactiveRef={link}
      endContent={
        <HStack gap={2} wrap="wrap">
          {item.approverId === null ? (
            <Button label={t('approvals.reassign')} aria-label={recordActionLabel(t('approvals.reassign'), item.ticketNumber)} variant="secondary" onClick={action('reassign')} />
          ) : (
            <>
              <Button label={t('approvals.approve')} aria-label={recordActionLabel(t('approvals.approve'), item.ticketNumber)} variant="primary" icon={<Icon icon={Icons.check} />} onClick={action('approve')} />
              <Button label={t('approvals.reject')} aria-label={recordActionLabel(t('approvals.reject'), item.ticketNumber)} variant="destructive" icon={<Icon icon={Icons.close} />} onClick={action('reject')} />
            </>
          )}
        </HStack>
      }
    />
  );
}

export function ApprovalsPage() {
  const { t } = useTranslation('tickets');
  const approvals = useMyApprovals();
  const changeApprovals = useChangeApprovals();
  const [pending, setPending] = useState<Pending | null>(null);
  const [changePending, setChangePending] = useState<ChangeDecision | null>(null);
  const isPending = approvals.isPending || changeApprovals.isPending;
  const error = approvals.error ?? changeApprovals.error;
  const ticketItems = approvals.data ?? [];
  const changeItems = changeApprovals.data ?? [];
  const loaded = approvals.data !== undefined && changeApprovals.data !== undefined;
  const retry = () => {
    void approvals.refetch();
    void changeApprovals.refetch();
  };

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.approvals.icon} titleOverride={t('approvals.title')} />
      <Card padding={0}>
        {isPending && !error && <LoadingState rows={SKELETON_ROWS} />}
        {error && <ErrorState error={error} onRetry={retry} />}
        {loaded && ticketItems.length + changeItems.length === 0 && <EmptyState message={t('approvals.empty')} />}
        {loaded && ticketItems.length + changeItems.length > 0 && (
          <List hasDividers aria-label={t('approvals.title')}>
            {ticketItems.map((item) => (
              <ApprovalRow key={item.id} item={item} onDecide={setPending} />
            ))}
            {changeItems.map((item) => (
              <ChangeApprovalRow key={item.id} item={item} onDecide={setChangePending} />
            ))}
          </List>
        )}
      </Card>
      {pending !== null && <DecisionDialog key={`${pending.item.id}-${pending.kind}`} pending={pending} onClose={() => setPending(null)} />}
      {changePending !== null && (
        <ChangeApprovalDialog key={`${changePending.item.id}-${changePending.decision}`} pending={changePending} onClose={() => setChangePending(null)} />
      )}
    </VStack>
  );
}
