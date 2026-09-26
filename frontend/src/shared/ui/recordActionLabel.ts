/**
 * Accessible name for a row button: starts with the visible text (WCAG 2.5.3 label in name) and adds the record,
 * so screen readers announce "Düzenle İlker Işık" instead of a list of identical "Düzenle" buttons.
 */
export const recordActionLabel = (action: string, recordName: string): string => `${action} ${recordName}`;
