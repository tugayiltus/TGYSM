import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { useSession } from '../auth/session';

export function ProfilePage() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const me = useSession();
  const rows = [
    { label: t('profile.name'), value: `${me.firstName} ${me.lastName}` },
    { label: t('profile.email'), value: me.email },
    { label: t('profile.role'), value: tc(`roles.${me.role}`) },
    { label: t('profile.department'), value: me.departmentName ?? t('profile.notSet') },
    { label: t('profile.location'), value: me.locationName ?? t('profile.notSet') },
  ];

  return (
    <section className="flex max-w-xl flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('profile.title')}</h1>
      <dl className="divide-y divide-slate-100 rounded-2xl border border-slate-200 bg-white">
        {rows.map((row) => (
          <div key={row.label} className="flex flex-col gap-1 p-4 sm:flex-row sm:justify-between">
            <dt className="text-sm text-slate-600">{row.label}</dt>
            <dd className="font-medium break-words">{row.value}</dd>
          </div>
        ))}
      </dl>
      <Link to={ROUTES.changePassword} className="self-start text-brand-700 underline">
        {t('profile.changePassword')}
      </Link>
    </section>
  );
}
