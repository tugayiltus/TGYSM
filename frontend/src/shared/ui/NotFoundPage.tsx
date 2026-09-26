import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../routes';

export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-4 p-4 text-center">
      <h1 className="text-xl font-semibold">{t('notFound.title')}</h1>
      <Link to={ROUTES.home} className="text-brand-700 underline">
        {t('notFound.back')}
      </Link>
    </main>
  );
}
