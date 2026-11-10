/**
 * @input Nothing.
 * @output A card that says the page does not exist, with a link home.
 * @position Router fallback for unknown URLs, outside the app shell: the visitor may not be signed in.
 */
import { Button } from '@astryxdesign/core/Button';
import { Heading } from '@astryxdesign/core/Heading';
import { useTranslation } from 'react-i18next';

import { AuthCard } from '../layout/AuthCard';
import { ROUTES } from '../routes';

export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <AuthCard>
      <Heading level={1}>{t('notFound.title')}</Heading>
      <Button label={t('notFound.back')} variant="primary" href={ROUTES.home} />
    </AuthCard>
  );
}
