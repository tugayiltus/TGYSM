import { describe, expect, it } from 'vitest';

import { trLower, trUpper, withSuffix } from './turkish';

describe('trUpper / trLower', () => {
  it('uses Turkish dotted and dotless i', () => {
    expect(trUpper('istanbul ılık')).toBe('İSTANBUL ILIK');
    expect(trLower('IŞIK İLKER')).toBe('ışık ilker');
  });
});

describe('withSuffix', () => {
  it.each([
    ['Ankara', 'dative', "Ankara'ya"],
    ['İzmir', 'dative', "İzmir'e"],
    ['Kars', 'locative', "Kars'ta"],
    ['İzmir', 'locative', "İzmir'de"],
    ['Ankara', 'ablative', "Ankara'dan"],
    ['Kars', 'ablative', "Kars'tan"],
    ['Ankara', 'accusative', "Ankara'yı"],
    ['Ordu', 'accusative', "Ordu'yu"],
    ['Bolu', 'genitive', "Bolu'nun"],
    ['Ümit', 'dative', "Ümit'e"],
  ] as const)('%s + %s = %s (proper noun)', (word, kind, expected) => {
    expect(withSuffix(word, kind, { properNoun: true })).toBe(expected);
  });

  it.each([
    ['tablet', 'plural', 'tabletler'],
    ['telefon', 'plural', 'telefonlar'],
    ['saat', 'dative', 'saate'],
    ['kontrol', 'locative', 'kontrolde'],
  ] as const)('%s + %s = %s (common noun, pronunciation exceptions)', (word, kind, expected) => {
    expect(withSuffix(word, kind)).toBe(expected);
  });

  it('uses the pronunciation of known abbreviations', () => {
    expect(withSuffix('IT', 'dative')).toBe("IT'ye");
    expect(withSuffix('PDF', 'locative')).toBe("PDF'te");
  });

  it('returns null when pronunciation is unknown', () => {
    expect(withSuffix('XYZ', 'dative')).toBeNull();
    expect(withSuffix('   ', 'dative')).toBeNull();
  });
});
