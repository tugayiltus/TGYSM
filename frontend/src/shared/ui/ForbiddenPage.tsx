import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../routes';

/** Shown inside the app layout when the signed-in role may not open a page reached by a direct URL. */
export function ForbiddenPage() {
  const { t } = useTranslation();
  return (
    <section className="d-flex flex-column align-items-center gap-3 p-4 text-center">
      <h1 className="h4 mb-0">{t('forbidden.title')}</h1>
      <Link to={ROUTES.home} className="btn btn-primary tgysm-touch d-inline-flex align-items-center">
        {t('forbidden.back')}
      </Link>
    </section>
  );
}
