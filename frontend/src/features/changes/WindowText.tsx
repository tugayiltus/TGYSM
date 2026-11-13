/**
 * @input A maintenance window start and end (either may be missing while a change is a draft).
 * @output One line of text with the window, or a note that none is set.
 * @position Change lists, detail and the summary view.
 */
import { Text } from '@astryxdesign/core/Text';
import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';

const RANGE_SEPARATOR = ' – ';

export function WindowText({ start, end }: { start: string | null; end: string | null }) {
  const { t } = useTranslation('changes');
  if (start === null || end === null) {
    return <Text type="supporting">{t('detail.noWindow')}</Text>;
  }

  return <Text>{`${formatDateTime(start)}${RANGE_SEPARATOR}${formatDateTime(end)}`}</Text>;
}
