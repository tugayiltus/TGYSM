/**
 * @input Whether the form has unsaved changes, and the callback that closes the form.
 * @output `requestClose`, which closes a clean form at once, and the confirm dialog to render for a changed one.
 * @position Form dialogs: Vazgeç, the close button and Escape all go through `requestClose`.
 */
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { ConfirmDialog } from './ConfirmDialog';

/** Asks before a changed form is closed, so a stray Vazgeç, close click or Escape does not lose the input. */
export function useDiscardGuard(isDirty: boolean, onClose: () => void): { requestClose: () => void; confirm: ReactNode } {
  const { t } = useTranslation();
  const [isAsking, setIsAsking] = useState(false);
  const requestClose = () => (isDirty ? setIsAsking(true) : onClose());
  const confirm = isAsking && (
    <ConfirmDialog
      title={t('discard.title')}
      message={t('discard.message')}
      confirmLabel={t('discard.confirm')}
      variant="danger"
      isPending={false}
      error={null}
      onConfirm={() => {
        setIsAsking(false);
        onClose();
      }}
      onCancel={() => setIsAsking(false)}
    />
  );
  return { requestClose, confirm };
}
