/**
 * @input A pending change approval and the decision the person picked.
 * @output A confirmation dialog that posts the decision with the approval version of the row; a reason is required to reject or return.
 * @position Onaylar page, change rows.
 */
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { useDecideListedChangeApproval, type ApprovalDecision, type ChangeApprovalItem } from '../changes/api';

const DIALOG_WIDTH = 480;

export interface ChangeDecision {
  item: ChangeApprovalItem;
  decision: ApprovalDecision;
}

export function ChangeApprovalDialog({ pending, onClose }: { pending: ChangeDecision; onClose: () => void }) {
  const { t } = useTranslation('changes');
  const { t: tc } = useTranslation();
  const decide = useDecideListedChangeApproval();
  const [reason, setReason] = useState('');
  const { item, decision } = pending;
  const needsReason = decision !== 'approve';
  const confirm = () =>
    decide.mutate(
      { number: item.changeNumber, decision, approvalVersion: item.version, reason: reason.trim() === '' ? null : reason.trim() },
      { onSuccess: onClose },
    );
  const close = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };

  return (
    <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH} purpose="form">
      <Layout
        header={<DialogHeader title={t('approval.decisionTitle', { decision: t(`approval.${decision}`), number: item.changeNumber })} onOpenChange={close} />}
        content={
          <LayoutContent>
            <VStack gap={4}>
              <FormAlert message={decide.error ? describeError(decide.error) : null} />
              <Text type="body">{item.title}</Text>
              <TextInput label={needsReason ? t('approval.reasonField') : t('approval.reasonOptional')} autoComplete="off" value={reason} onChange={setReason} />
            </VStack>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack gap={2} hAlign="end">
              <Button label={tc('actions.cancel')} variant="secondary" onClick={onClose} />
              <Button
                label={tc('actions.confirm')}
                variant={decision === 'approve' ? 'primary' : 'destructive'}
                isDisabled={needsReason && reason.trim() === ''}
                isLoading={decide.isPending}
                onClick={confirm}
              />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
  );
}
