import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { Button } from './Button';
import { CheckField } from './CheckField';
import { ComboboxField } from './ComboboxField';
import { cilSearch } from './icons';
import { KpiCard } from './KpiCard';
import { SelectField } from './SelectField';
import { StatusBadge } from './StatusBadge';
import { TextAreaField } from './TextAreaField';
import { TextField } from './TextField';

describe('fields', () => {
  it('marks a required field visually and for assistive tech', () => {
    render(<TextField label="Ad" required />);
    expect(screen.getByRole('textbox', { name: /Ad/ })).toBeRequired();
    expect(screen.getByText('*')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.getByText('(zorunlu)')).toHaveClass('visually-hidden');
  });

  it('puts a start icon in an input group without changing the label link', () => {
    const { container } = render(<TextField label="Ara" type="search" startIcon={cilSearch} error="Hatalı" />);
    expect(container.querySelector('.input-group .input-group-text svg')).not.toBeNull();
    expect(screen.getByRole('searchbox', { name: 'Ara' })).toBeInTheDocument();
    // Invalid feedback must sit inside the input group to be shown next to an .is-invalid control.
    expect(container.querySelector('.input-group .invalid-feedback')).toHaveTextContent('Hatalı');
  });

  it('shows a unit after the control in the same input group', () => {
    const { container } = render(<TextField label="Hedef" type="number" endText="dk" />);
    const group = container.querySelector('.input-group');
    expect(group?.lastElementChild).toHaveTextContent('dk');
    expect(screen.getByRole('spinbutton', { name: 'Hedef' })).toBeInTheDocument();
  });

  it('uses the same label style for every field', () => {
    render(
      <>
        <TextField label="A" />
        <SelectField label="B" options={[]} />
        <TextAreaField label="C" />
        <ComboboxField label="D" options={[]} value="" emptyText="Yok" onChange={() => undefined} />
      </>,
    );
    for (const text of ['A', 'B', 'C', 'D']) {
      expect(screen.getByText(text)).toHaveClass('form-label', 'fw-medium', 'small');
    }
  });

  it('shows combobox options as a dropdown menu like other menus', async () => {
    const user = userEvent.setup();
    render(<ComboboxField label="Marka" options={[{ value: '1', label: 'Dell' }]} value="" emptyText="Yok" onChange={() => undefined} />);
    await user.click(screen.getByRole('combobox', { name: 'Marka' }));
    expect(screen.getByRole('listbox')).toHaveClass('dropdown-menu');
    expect(screen.getByRole('option', { name: 'Dell' })).toHaveClass('dropdown-item');
  });

  it('CheckField renders a CoreUI switch that toggles', async () => {
    const user = userEvent.setup();
    render(<CheckField type="switch" label="Onay gerekli" />);
    const box = screen.getByRole('switch', { name: 'Onay gerekli' });
    await user.click(box);
    expect(box).toBeChecked();
  });

  it('CheckField links its error to the control', () => {
    render(<CheckField label="Kabul" error="Zorunlu" />);
    expect(screen.getByRole('checkbox', { name: 'Kabul' })).toHaveAccessibleDescription('Zorunlu');
  });

  it('Button shows its icon before the text and hides it from screen readers', () => {
    render(<Button icon={cilSearch}>Ara</Button>);
    expect(screen.getByRole('button', { name: 'Ara' }).querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
  });

  it('StatusBadge uses subtle colors that work in both color modes', () => {
    render(<StatusBadge isActive />);
    expect(screen.getByText('Aktif')).toHaveClass('bg-success-subtle', 'text-success-emphasis');
  });

  it('KpiCard shows a fixed-size icon box in its color', () => {
    const { container } = render(<KpiCard title="Açık kayıtlar" value="12" period="Bugün" icon={cilSearch} color="danger" />);
    expect(container.querySelector('.tgysm-kpi-icon')).toHaveClass('bg-danger-subtle', 'text-danger-emphasis');
  });
});
