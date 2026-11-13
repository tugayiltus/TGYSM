/**
 * @input The kind of an approval row.
 * @output A token that tells a request approval from a change approval; color is never the only cue.
 * @position Onaylar page.
 */
import { Token } from '@astryxdesign/core/Token';
import { useTranslation } from 'react-i18next';

type ApprovalKind = 'ticket' | 'change';

export function KindToken({ kind }: { kind: ApprovalKind }) {
  const { t } = useTranslation('tickets');
  return <Token size="sm" color={kind === 'change' ? 'purple' : 'gray'} label={t(`approvals.kinds.${kind}`)} />;
}
