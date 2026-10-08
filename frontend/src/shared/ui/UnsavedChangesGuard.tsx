import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useBlocker } from 'react-router';

import { ConfirmDialog } from './ConfirmDialog';

/**
 * While `when` is true, in-app navigation asks for confirmation and closing or reloading the tab triggers
 * the browser's own leave prompt.
 */
export function UnsavedChangesGuard({ when }: { when: boolean }) {
  const { t } = useTranslation();
  const blocker = useBlocker(when);

  useEffect(() => {
    if (!when) {
      return undefined;
    }
    const onBeforeUnload = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [when]);

  if (blocker.state !== 'blocked') {
    return null;
  }
  return (
    <ConfirmDialog
      title={t('unsaved.title')}
      message={t('unsaved.message')}
      confirmLabel={t('unsaved.leave')}
      variant="danger"
      isPending={false}
      error={null}
      onConfirm={() => blocker.proceed()}
      onCancel={() => blocker.reset()}
    />
  );
}
