import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../routes';

/** Shown inside the app shell when the signed-in role may not open a page reached by a direct URL. */
export function ForbiddenPage() {
  const { t } = useTranslation();
  return (
    <section className="flex flex-col items-center gap-4 p-6 text-center">
      <h1 className="text-xl font-semibold">{t('forbidden.title')}</h1>
      <Link to={ROUTES.home} className="text-brand-700 underline">
        {t('forbidden.back')}
      </Link>
    </section>
  );
}
