/**
 * @input The dialog's close callback.
 * @output An `onError` handler for a dialog mutation: on a stale-version conflict it shows the conflict message
 *   and closes the dialog, so the next attempt opens on the fresh version instead of resending the old one.
 * @position Used by the change dialogs (change detail and Onaylar) that send the version they opened with.
 */
import { useToast } from '@astryxdesign/core/Toast';
import { useCallback } from 'react';

import { describeError } from '../../shared/api/errorMessage';
import { isConcurrencyConflict } from '../../shared/api/errors';

export function useCloseOnConflict(onClose: () => void): (error: unknown) => void {
  const showToast = useToast();
  return useCallback(
    (error: unknown) => {
      if (!isConcurrencyConflict(error)) {
        return;
      }
      showToast({ body: describeError(error), type: 'error' });
      onClose();
    },
    [showToast, onClose],
  );
}
