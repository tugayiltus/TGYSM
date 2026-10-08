import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { AuthCard } from '../layout/AuthCard';
import { ROUTES } from '../routes';

/** Unknown URL: a card outside the app, since the visitor may not be signed in. */
export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <AuthCard>
      <h1 className="h4 mb-0 text-center">{t('notFound.title')}</h1>
      <Link to={ROUTES.home} className="btn btn-primary tgysm-touch d-inline-flex align-items-center justify-content-center">
        {t('notFound.back')}
      </Link>
    </AuthCard>
  );
}
