/**
 * @input A change.
 * @output The approval state, the warning when nobody can approve, and the decision buttons for an eligible admin.
 * @position Change detail page, Onay section.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { FormTextArea } from '../../shared/form/formFields';
import { formatDateTime } from '../../shared/lib/format';

import { useDecideChangeApproval, type ApprovalDecision, type ChangeDetail } from './api';
import { reasonText } from './reasonText';
import { useCloseOnConflict } from './useCloseOnConflict';

/** Mirror backend ChangeApproval.ReasonMaxLength. */
const REASON_MAX_LENGTH = 500;
const DECISIONS: readonly ApprovalDecision[] = ['approve', 'reject', 'return'];
const DECISION_VARIANT = { approve: 'primary', reject: 'destructive', return: 'secondary' } as const;

const requiredSchema = z.object({ reason: z.string().trim().min(1, ERROR_CODES.required).max(REASON_MAX_LENGTH, ERROR_CODES.tooLong) });
const optionalSchema = z.object({ reason: z.string().trim().max(REASON_MAX_LENGTH, ERROR_CODES.tooLong) });

function DecisionDialog({ change, decision, onClose }: { change: ChangeDetail; decision: ApprovalDecision; onClose: () => void }) {
  const { t } = useTranslation('changes');
  const { t: tc } = useTranslation();
  // The approval version the person saw when opening the dialog.
  const [opened] = useState(change);
  const decide = useDecideChangeApproval(opened);
  const closeOnConflict = useCloseOnConflict(onClose);
  const { control, handleSubmit } = useForm<z.infer<typeof optionalSchema>>({
    resolver: zodResolver(decision === 'approve' ? optionalSchema : requiredSchema),
    defaultValues: { reason: '' },
  });
  const onSubmit = handleSubmit((v) => decide.mutate({ decision, reason: v.reason.trim() === '' ? null : v.reason.trim() }, { onSuccess: onClose, onError: closeOnConflict }));
  return (
    <FormDialog
      title={t('approval.decisionTitle', { decision: t(`approval.${decision}`), number: change.number })}
      submitLabel={tc('actions.confirm')}
      isPending={decide.isPending}
      alert={decide.error ? describeError(decide.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextArea control={control} name="reason" label={decision === 'approve' ? t('approval.reasonOptional') : t('approval.reasonField')} />
    </FormDialog>
  );
}

export function ChangeApprovalBox({ change }: { change: ChangeDetail }) {
  const { t } = useTranslation('changes');
  const { t: tc } = useTranslation();
  const [decision, setDecision] = useState<ApprovalDecision | null>(null);
  const approval = change.approval;
  const waitsForApproval = change.status === 'draft' || change.status === 'pending_approval';
  const canDecide = approval !== null && approval.status === 'pending' && approval.canDecide;
  return (
    <VStack gap={3}>
      {waitsForApproval && !change.hasEligibleApprover && <Banner status="warning" title={t('approval.noEligible')} description={t('approval.noEligibleDescription')} />}
      {approval === null ? (
        <Text type="supporting">{t('approval.none')}</Text>
      ) : (
        <MetadataList label={{ position: 'top' }}>
          <MetadataListItem label={t('approval.status')}>{t(`approval.statuses.${approval.status}`)}</MetadataListItem>
          <MetadataListItem label={t('approval.requestedAt')}>{formatDateTime(approval.createdAt)}</MetadataListItem>
          <MetadataListItem label={t('approval.decidedBy')}>{approval.decidedByName ?? tc('none')}</MetadataListItem>
          {approval.decidedAt && <MetadataListItem label={t('approval.decidedAt')}>{formatDateTime(approval.decidedAt)}</MetadataListItem>}
          {approval.reason && (
            <MetadataListItem label={t('approval.reason')}>
              <Text wordBreak="break-word">{reasonText(approval.reason)}</Text>
            </MetadataListItem>
          )}
        </MetadataList>
      )}
      {canDecide && (
        <HStack gap={2} wrap="wrap">
          {DECISIONS.map((kind) => (
            <Button key={kind} label={t(`approval.${kind}`)} variant={DECISION_VARIANT[kind]} onClick={() => setDecision(kind)} />
          ))}
        </HStack>
      )}
      {decision !== null && <DecisionDialog key={decision} change={change} decision={decision} onClose={() => setDecision(null)} />}
    </VStack>
  );
}
