import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from './Button';
import { Dialog } from './Dialog';

export function TemporaryPasswordDialog({ password, onClose }: { password: string; onClose: () => void }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  const copy = () =>
    navigator.clipboard.writeText(password).then(
      () => setCopied(true),
      () => setCopied(false),
    );

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t('temporaryPassword.title')}
      description={t('temporaryPassword.description')}
      footer={<Button onClick={onClose}>{t('actions.close')}</Button>}
    >
      <p className="small text-body-secondary mb-1">{t('temporaryPassword.label')}</p>
      <p data-testid="temporary-password" className="rounded bg-body-tertiary p-3 font-monospace fs-5 text-break user-select-all mb-0">
        {password}
      </p>
      <Button variant="secondary" className="mt-3" onClick={() => void copy()}>
        {copied ? t('actions.copied') : t('actions.copy')}
      </Button>
    </Dialog>
  );
}
