/**
 * @input Title, message, optional content and a close callback.
 * @output A modal that informs and closes with one Kapat button.
 * @position Results that need no decision: a sent link, a one-time password.
 */
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

const DIALOG_WIDTH = 480;

export function InfoDialog({ title, message, onClose, children }: { title: string; message: string; onClose: () => void; children?: ReactNode }) {
  const { t } = useTranslation();
  const close = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };
  return (
    <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH}>
      <Layout
        header={<DialogHeader title={title} onOpenChange={close} />}
        content={
          <LayoutContent>
            <VStack gap={3}>
              <Text type="body">{message}</Text>
              {children}
            </VStack>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack hAlign="end">
              <Button label={t('actions.close')} variant="primary" onClick={onClose} />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
  );
}
