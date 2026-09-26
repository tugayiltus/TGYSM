/** Turkish text helpers (ADR-0006). */
export const TURKISH_LOCALE = 'tr-TR';

export const trUpper = (value: string): string => value.toLocaleUpperCase(TURKISH_LOCALE);
export const trLower = (value: string): string => value.toLocaleLowerCase(TURKISH_LOCALE);

export type SuffixKind = 'dative' | 'locative' | 'ablative' | 'accusative' | 'genitive' | 'plural';

export interface SuffixOptions {
  /** Proper nouns take an apostrophe before the suffix ("Ankara'ya"). */
  properNoun?: boolean;
}

const BACK_VOWELS = 'aıou';
const FRONT_VOWELS = 'eiöü';
const VOWELS = BACK_VOWELS + FRONT_VOWELS;
/** "FıSTıKÇı ŞaHaP": consonants that turn a following d into t. */
const HARD_CONSONANTS = 'fstkçşhp';
const APOSTROPHE = "'";
const ABBREVIATION_MAX_LENGTH = 5;
const LETTER_PATTERN = /\p{L}/u;

/**
 * How a word is read aloud, keyed by its Turkish lower-case form. The written form alone does not tell
 * the last vowel of these words. Note that Turkish lower-casing turns "IT" into "ıt".
 */
const PRONUNCIATION_OVERRIDES: Readonly<Record<string, string>> = {
  saat: 'saet',
  harf: 'herf',
  hal: 'hel',
  kalp: 'kelp',
  dikkat: 'dikket',
  rol: 'röl',
  gol: 'göl',
  kontrol: 'kontröl',
  alkol: 'alköl',
  ıt: 'ayti',
  pc: 'pisi',
  pdf: 'pedeef',
  csv: 'ceseve',
  vpn: 'vepeen',
  usb: 'usebe',
  sms: 'semes',
};

function isAbbreviation(word: string): boolean {
  return word.length <= ABBREVIATION_MAX_LENGTH && LETTER_PATTERN.test(word) && word === trUpper(word);
}

function pronunciationOf(word: string): string | null {
  const lower = trLower(word);
  const override = PRONUNCIATION_OVERRIDES[lower];
  if (override !== undefined) {
    return override;
  }
  return isAbbreviation(word) ? null : lower;
}

function lastVowelOf(pronunciation: string): string | null {
  for (let index = pronunciation.length - 1; index >= 0; index -= 1) {
    const character = pronunciation[index];
    if (character !== undefined && VOWELS.includes(character)) {
      return character;
    }
  }
  return null;
}

const twoWayVowel = (vowel: string): string => (BACK_VOWELS.includes(vowel) ? 'a' : 'e');

function fourWayVowel(vowel: string): string {
  switch (vowel) {
    case 'a':
    case 'ı':
      return 'ı';
    case 'e':
    case 'i':
      return 'i';
    case 'o':
    case 'u':
      return 'u';
    default:
      return 'ü';
  }
}

/**
 * Appends a case or plural suffix with vowel harmony, buffer letters (y, n) and d/t assimilation.
 * Returns null when the pronunciation is unknown; callers then use a suffix-free label instead.
 */
export function withSuffix(word: string, kind: SuffixKind, options: SuffixOptions = {}): string | null {
  const trimmed = word.trim();
  const pronunciation = pronunciationOf(trimmed);
  const vowel = pronunciation === null ? null : lastVowelOf(pronunciation);
  if (pronunciation === null || vowel === null) {
    return null;
  }

  const lastCharacter = pronunciation.at(-1) ?? '';
  const endsWithVowel = VOWELS.includes(lastCharacter);
  const a = twoWayVowel(vowel);
  const i = fourWayVowel(vowel);
  const d = HARD_CONSONANTS.includes(lastCharacter) ? 't' : 'd';

  const suffixes: Record<SuffixKind, string> = {
    dative: endsWithVowel ? `y${a}` : a,
    locative: `${d}${a}`,
    ablative: `${d}${a}n`,
    accusative: endsWithVowel ? `y${i}` : i,
    genitive: endsWithVowel ? `n${i}n` : `${i}n`,
    plural: `l${a}r`,
  };

  const separator = options.properNoun === true || isAbbreviation(trimmed) ? APOSTROPHE : '';
  return `${trimmed}${separator}${suffixes[kind]}`;
}
