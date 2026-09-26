import { describe, expect, it } from 'vitest';

import { describeError, translateMessage } from './errorMessage';
import { ApiError, NetworkError, toApiError } from './errors';

describe('toApiError', () => {
  it('reads code and field errors from a problem body', () => {
    const error = toApiError(400, { code: 'validation.failed', errors: { email: ['validation.invalid_email'] } });

    expect(error.status).toBe(400);
    expect(error.code).toBe('validation.failed');
    expect(error.fieldErrors.email).toEqual(['validation.invalid_email']);
  });

  it('falls back to common.unexpected for bodies without a code', () => {
    expect(toApiError(500, 'oops').code).toBe('common.unexpected');
  });
});

describe('describeError', () => {
  it('translates known codes, unknown codes and network failures', () => {
    expect(describeError(new ApiError(409, 'users.email_taken'))).toBe('Bu e-posta adresi başka bir hesapta kullanılıyor.');
    expect(describeError(new ApiError(409, 'made.up'))).toBe('Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.');
    expect(describeError(new NetworkError(new Error('offline')))).toBe('Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.');
    expect(describeError('not an error')).toBe('Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.');
  });
});

describe('translateMessage', () => {
  it('treats namespaced keys as i18n keys and plain values as error codes', () => {
    expect(translateMessage('auth:changePassword.mismatch')).toBe('Şifreler eşleşmiyor.');
    expect(translateMessage('validation.required')).toBe('Bu alan zorunludur.');
  });
});
