import { describe, expect, it } from 'vitest';

import i18n from '.';

describe('i18n', () => {
  it('serves Turkish texts from every namespace', () => {
    expect(i18n.language).toBe('tr');
    expect(i18n.t('common:roles.it_staff')).toBe('IT personeli');
    expect(i18n.t('auth:login.submit')).toBe('Giriş yap');
    expect(i18n.t('admin:nav.users')).toBe('Kullanıcılar');
    expect(i18n.t('errors:auth.invalid_credentials')).toBe('E-posta veya şifre hatalı.');
  });
});
