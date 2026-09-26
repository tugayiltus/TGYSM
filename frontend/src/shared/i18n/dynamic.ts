import i18n from '.';

// Keys built at runtime (audit actions, entity types) cannot use the typed key overloads.
const translateKey = i18n.t.bind(i18n) as unknown as (key: string) => string;

/** Translates a runtime-built key, or returns the fallback when the key does not exist. */
export function translateOr(key: string, fallback: string): string {
  return i18n.exists(key) ? translateKey(key) : fallback;
}
