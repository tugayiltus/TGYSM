/**
 * @input A stored reason: text a person typed, or a reason code the backend writes itself (ADR-0006).
 * @output The text to show: the Turkish text of a known code, otherwise the reason unchanged.
 * @position Used wherever a change or task reason is shown.
 */
import i18n from '../../shared/i18n';

const NAMESPACE = 'changes';
const CODE_PREFIX = `${NAMESPACE}.reason.`;
const NAMESPACE_SEPARATOR = ':';

// Reason codes arrive at runtime, so they cannot use the typed key overloads.
const translate = i18n.t.bind(i18n) as unknown as (key: string) => string;

/**
 * Turns a backend reason code such as `changes.reason.not_needed` into its Turkish text.
 * @example
 * reasonText('changes.reason.plan_changed') // "Plan değişti"
 */
export function reasonText(reason: string): string {
  if (!reason.startsWith(CODE_PREFIX)) {
    return reason;
  }

  const key = `${NAMESPACE}${NAMESPACE_SEPARATOR}${reason.slice(NAMESPACE.length + 1)}`;
  return i18n.exists(key) ? translate(key) : reason;
}
