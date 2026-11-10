/**
 * @input Nothing; loads the signed-in admin's open approvals.
 * @output A list of approvals with approve, reject or reassign actions, each confirmed in a dialog.
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
import { useDecideApproval, useMyApprovals, useReassignApprover, type MyApprovalItem } from '../tickets/api';

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
  const [pending, setPending] = useState<Pending | null>(null);

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.approvals.icon} titleOverride={t('approvals.title')} />
      <Card padding={0}>
        {approvals.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {approvals.isError && <ErrorState error={approvals.error} onRetry={() => void approvals.refetch()} />}
        {approvals.data && approvals.data.length === 0 && <EmptyState message={t('approvals.empty')} />}
        {approvals.data && approvals.data.length > 0 && (
          <List hasDividers aria-label={t('approvals.title')}>
            {approvals.data.map((item) => (
              <ApprovalRow key={item.id} item={item} onDecide={setPending} />
            ))}
          </List>
        )}
      </Card>
      {pending !== null && <DecisionDialog key={`${pending.item.id}-${pending.kind}`} pending={pending} onClose={() => setPending(null)} />}
    </VStack>
  );
}
