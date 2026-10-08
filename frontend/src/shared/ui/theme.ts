/** Light theme only (ADR-0021); CoreUI reads it from `data-coreui-theme` on the root element. */
export const THEME = 'light';

const THEME_ATTRIBUTE = 'data-coreui-theme';

export function applyTheme(root: HTMLElement = document.documentElement) {
  root.setAttribute(THEME_ATTRIBUTE, THEME);
}
