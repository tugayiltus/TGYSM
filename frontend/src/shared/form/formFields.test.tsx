import { zodResolver } from '@hookform/resolvers/zod';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useForm } from 'react-hook-form';
import { describe, expect, it, vi } from 'vitest';
import { z } from 'zod';

import { AstryxProvider } from '../ui/AstryxProvider';

import { FormCheckbox, FormNumberInput, FormSelector, FormSwitch, FormTextArea, FormTextInput } from './formFields';

const REQUIRED = 'validation.required';
const REQUIRED_TEXT = 'Bu alan zorunludur.';
const LONG_TURKISH_LABEL = 'Şirket İçi Değerlendirme Ağırlığı Çok Uzun Bir Başlık';
const PRIORITIES = [
  { value: 'low', label: 'Düşük' },
  { value: 'high', label: 'Yüksek' },
] as const;
const DEFAULT_MINUTES = 30;
const NEW_MINUTES = 45;

const schema = z.object({
  title: z.string().min(1, REQUIRED),
  body: z.string(),
  priority: z.string(),
  urgent: z.boolean(),
  notify: z.boolean(),
  minutes: z.number(),
});
type Values = z.infer<typeof schema>;
const EMPTY: Values = { title: '', body: '', priority: 'low', urgent: false, notify: false, minutes: DEFAULT_MINUTES };

function Harness({ onSubmit, resetTo }: { onSubmit: (values: Values) => void; resetTo?: Partial<Values> }) {
  const { control, handleSubmit, reset } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: EMPTY });
  return (
    <AstryxProvider>
      <form onSubmit={handleSubmit(onSubmit)}>
        <FormTextInput control={control} name="title" label={LONG_TURKISH_LABEL} isRequired />
        <FormTextArea control={control} name="body" label="Açıklama" />
        <FormSelector control={control} name="priority" label="Öncelik" options={PRIORITIES} />
        <FormCheckbox control={control} name="urgent" label="Acil" />
        <FormSwitch control={control} name="notify" label="Bildir" />
        <FormNumberInput control={control} name="minutes" label="Süre" />
        <button type="submit">Kaydet</button>
        <button type="button" onClick={() => reset({ ...EMPTY, ...resetTo })}>
          Sıfırla
        </button>
      </form>
    </AstryxProvider>
  );
}

describe('form fields', () => {
  it('submits what the user typed and chose', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<Harness onSubmit={onSubmit} />);

    await user.type(screen.getByRole('textbox', { name: new RegExp(LONG_TURKISH_LABEL) }), 'Yazıcı çalışmıyor');
    await user.type(screen.getByRole('textbox', { name: 'Açıklama' }), 'Kâğıt sıkıştı');
    await user.click(screen.getByRole('combobox', { name: 'Öncelik' }));
    await user.click(await screen.findByRole('option', { name: 'Yüksek' }));
    await user.click(screen.getByRole('checkbox', { name: 'Acil' }));
    await user.click(screen.getByRole('switch', { name: 'Bildir' }));
    const minutes = screen.getByRole('spinbutton', { name: 'Süre' });
    await user.clear(minutes);
    await user.type(minutes, `${NEW_MINUTES}{Enter}`);
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    expect(onSubmit.mock.calls[0]?.[0]).toEqual({
      title: 'Yazıcı çalışmıyor',
      body: 'Kâğıt sıkıştı',
      priority: 'high',
      urgent: true,
      notify: true,
      minutes: NEW_MINUTES,
    });
  });

  it('shows the Turkish error and links it to the input', async () => {
    const user = userEvent.setup();
    render(<Harness onSubmit={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));

    const input = await screen.findByRole('textbox', { name: new RegExp(LONG_TURKISH_LABEL) });
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription(new RegExp(REQUIRED_TEXT));
  });

  it('marks a required field for assistive tech', () => {
    render(<Harness onSubmit={vi.fn()} />);
    expect(screen.getByRole('textbox', { name: new RegExp(LONG_TURKISH_LABEL) })).toHaveAttribute('aria-required', 'true');
  });

  it('shows the values a form reset puts in', async () => {
    const user = userEvent.setup();
    render(<Harness onSubmit={vi.fn()} resetTo={{ title: 'Yeni başlık', body: 'Yeni gövde' }} />);
    await user.click(screen.getByRole('button', { name: 'Sıfırla' }));
    expect(screen.getByRole('textbox', { name: new RegExp(LONG_TURKISH_LABEL) })).toHaveValue('Yeni başlık');
    expect(screen.getByRole('textbox', { name: 'Açıklama' })).toHaveValue('Yeni gövde');
  });
});
