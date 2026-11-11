/**
 * @input A one-time temporary password and a close callback.
 * @output The password shown once, with a copy button.
 * @position After creating a user or resetting a password with the temporary-password delivery.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Text } from '@astryxdesign/core/Text';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { InfoDialog } from '../../../shared/feedback/InfoDialog';

export function TemporaryPasswordDialog({ password, onClose }: { password: string; onClose: () => void }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  const copy = () =>
    navigator.clipboard.writeText(password).then(
      () => setCopied(true),
      () => setCopied(false),
    );

  return (
    <InfoDialog title={t('temporaryPassword.title')} message={t('temporaryPassword.description')} onClose={onClose}>
      <Text type="supporting">{t('temporaryPassword.label')}</Text>
      <Card variant="muted" padding={3}>
        <Text type="code" wordBreak="break-all" data-testid="temporary-password">
          {password}
        </Text>
      </Card>
      <HStack>
        <Button label={copied ? t('actions.copied') : t('actions.copy')} variant="secondary" onClick={() => void copy()} />
      </HStack>
    </InfoDialog>
  );
}
