/**
 * @input Nothing.
 * @output An empty state that says the role may not open this page, with a link home.
 * @position Inside the app shell, for a page reached by a direct URL that the role may not open.
 */
import { Button } from '@astryxdesign/core/Button';
import { EmptyState } from '@astryxdesign/core/EmptyState';
import { useTranslation } from 'react-i18next';

import { ROUTES } from '../routes';

/** The page title is this state's heading, so it is the H1. */
const TITLE_LEVEL = 1;

export function ForbiddenPage() {
  const { t } = useTranslation();
  return <EmptyState title={t('forbidden.title')} headingLevel={TITLE_LEVEL} actions={<Button label={t('forbidden.back')} variant="primary" href={ROUTES.home} />} />;
}
