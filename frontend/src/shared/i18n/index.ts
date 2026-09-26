import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import admin from './locales/tr/admin.json';
import assets from './locales/tr/assets.json';
import auth from './locales/tr/auth.json';
import common from './locales/tr/common.json';
import errors from './locales/tr/errors.json';
import tickets from './locales/tr/tickets.json';

export const DEFAULT_LANGUAGE = 'tr';
export const DEFAULT_NAMESPACE = 'common';

export const resources = {
  tr: { common, auth, admin, assets, tickets, errors },
} as const;

void i18n.use(initReactI18next).init({
  lng: DEFAULT_LANGUAGE,
  fallbackLng: DEFAULT_LANGUAGE,
  defaultNS: DEFAULT_NAMESPACE,
  ns: Object.keys(resources.tr),
  resources,
  interpolation: { escapeValue: false },
  returnNull: false,
});

export default i18n;
