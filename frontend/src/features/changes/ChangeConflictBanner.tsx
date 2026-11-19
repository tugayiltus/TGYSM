/**
 * @input The conflicts of a change (changes with an overlapping window and a shared asset or service).
 * @output A warning banner that lists each conflict with its window and reason; nothing when there is none.
 * @position Change detail page, above the plan.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { formatDateTime } from '../../shared/lib/format';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { changePath } from '../../shared/routes';

import type { ChangeDetail } from './api';

const RANGE_SEPARATOR = ' – ';
const META_SEPARATOR = ' · ';

export function ChangeConflictBanner({ conflicts }: { conflicts: ChangeDetail['conflicts'] }) {
  const { t } = useTranslation('changes');
  const location = useLocation();
  if (conflicts.length === 0) {
    return null;
  }

  return (
    <Banner
      status="warning"
      title={t('conflicts.title', { count: conflicts.length })}
      description={
        <VStack gap={1}>
          <List density="compact" aria-label={t('conflicts.title', { count: conflicts.length })}>
            {conflicts.map((conflict, index) => {
              const windowText = `${formatDateTime(conflict.windowStart)}${RANGE_SEPARATOR}${formatDateTime(conflict.windowEnd)}`;
              const detail = `${windowText}${META_SEPARATOR}${t(`conflicts.reasons.${conflict.reason}`)}`;
              return (
                <ListItem
                  // Hidden conflicts have no number; the position keeps their keys unique.
                  key={conflict.number ?? `hidden-${index}`}
                  label={
                    conflict.number === null ? (
                      <Text>{t('conflicts.hidden')}</Text>
                    ) : (
                      <StateLink href={changePath(conflict.number)} state={linkState(location)}>
                        {`${conflict.number}${META_SEPARATOR}${conflict.title ?? ''}`}
                      </StateLink>
                    )
                  }
                  description={detail}
                />
              );
            })}
          </List>
        </VStack>
      }
    />
  );
}
