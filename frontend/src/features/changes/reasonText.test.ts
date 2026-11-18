import { describe, expect, it } from 'vitest';

import '../../shared/i18n';
import { reasonText } from './reasonText';

describe('reasonText', () => {
  it('translates a reason code the backend writes', () => {
    expect(reasonText('changes.reason.plan_changed')).toBe('Plan değişti');
  });

  it('keeps text a person typed', () => {
    expect(reasonText('Kullanıcı vazgeçti')).toBe('Kullanıcı vazgeçti');
  });

  it('keeps an unknown code unchanged', () => {
    expect(reasonText('changes.reason.unknown')).toBe('changes.reason.unknown');
  });
});
