/**
 * @input A user-event session, the field label, the option text and an optional container.
 * @output Nothing; the option is chosen.
 * @position Test-only. Astryx `Selector` is a combobox (a plain button when it has a search box) with a popup list.
 */
import { screen, waitFor, within } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';

const escapeRegExp = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/**
 * The trigger of an Astryx select: a combobox, or a button when the select has its own search box.
 * The button's name is the label followed by the shown value, so it is matched by its start.
 */
export function findSelect(label: string | RegExp, container: HTMLElement = document.body): Promise<HTMLElement> {
  const buttonName = typeof label === 'string' ? new RegExp(`^${escapeRegExp(label)}\\b`) : label;
  return waitFor(() => within(container).queryByRole('combobox', { name: label }) ?? within(container).getByRole('button', { name: buttonName }));
}

export async function chooseOption(user: UserEvent, label: string | RegExp, option: string | RegExp, container: HTMLElement = document.body) {
  await user.click(await findSelect(label, container));
  await user.click(await screen.findByRole('option', { name: option }));
}
