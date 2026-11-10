/**
 * @input Nothing, a message, or an error with an optional retry.
 * @output Loading, empty and error states with Turkish text.
 * @position Inside a page region or card, in place of its content.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { EmptyState as AstryxEmptyState } from '@astryxdesign/core/EmptyState';
import { Skeleton } from '@astryxdesign/core/Skeleton';
import { Spinner } from '@astryxdesign/core/Spinner';
import { VStack } from '@astryxdesign/core/VStack';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../api/errorMessage';

/** Height of one skeleton row: one table row, so the list does not jump when data arrives. */
const SKELETON_ROW_HEIGHT = 'var(--size-element-lg, 2.5rem)';

/** Without `rows`, a labelled spinner. With `rows`, skeleton rows that keep the space of a first page. */
export function LoadingState({ rows }: { rows?: number }) {
  const { t } = useTranslation();
  if (rows === undefined) {
    return (
      <VStack padding={4} hAlign="center">
        <Spinner label={t('states.loading')} />
      </VStack>
    );
  }
  return (
    <VStack role="status" aria-label={t('states.loading')} gap={2} padding={3}>
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} index={index} height={SKELETON_ROW_HEIGHT} />
      ))}
    </VStack>
  );
}

export function EmptyState({ message, action }: { message?: string; action?: ReactNode }) {
  const { t } = useTranslation();
  return <AstryxEmptyState title={message ?? t('states.empty')} actions={action} isCompact />;
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <Banner
      status="error"
      title={describeError(error)}
      endContent={onRetry && <Button label={t('actions.retry')} variant="secondary" onClick={onRetry} />}
    />
  );
}
