/**
 * @input A user-event session, the field label, the option text and an optional container.
 * @output Nothing; the option is chosen.
 * @position Test-only. Astryx `Selector` is a combobox with a popup list, not a native select.
 */
import { screen, within } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';

export async function chooseOption(user: UserEvent, label: string | RegExp, option: string | RegExp, container: HTMLElement = document.body) {
  await user.click(await within(container).findByRole('combobox', { name: label }));
  await user.click(await screen.findByRole('option', { name: option }));
}
