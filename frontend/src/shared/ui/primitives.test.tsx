import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { Button } from './Button';
import { FormAlert } from './FormAlert';
import { Pagination } from './Pagination';
import { ResponsiveList } from './ResponsiveList';
import { SelectField } from './SelectField';
import { EmptyState, ErrorState, LoadingState } from './States';
import { StatusBadge } from './StatusBadge';
import { TextAreaField } from './TextAreaField';
import { TextField } from './TextField';

describe('Button', () => {
  it('is a CoreUI button that does not submit by default', () => {
    render(<Button>Kaydet</Button>);
    const button = screen.getByRole('button', { name: 'Kaydet' });
    expect(button).toHaveAttribute('type', 'button');
    expect(button).toHaveClass('btn', 'btn-primary');
  });

  it('is busy and disabled while loading', () => {
    render(
      <Button isLoading variant="secondary">
        Kaydet
      </Button>,
    );
    const button = screen.getByRole('button', { name: /Kaydet/ });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('aria-busy', 'true');
    expect(button).toHaveClass('btn-outline-secondary');
  });

  it.each([
    ['danger', 'btn-danger'],
    ['ghost', 'btn-ghost-secondary'],
  ] as const)('maps the %s variant to a CoreUI colour', (variant, className) => {
    render(<Button variant={variant}>X</Button>);
    expect(screen.getByRole('button', { name: 'X' })).toHaveClass(className);
  });
});

describe('form fields', () => {
  it('links the label, hint and error of a text field', () => {
    render(<TextField label="E-posta" hint="Kurumsal adres" error="Zorunlu alan" />);
    const input = screen.getByLabelText('E-posta');
    expect(input).toHaveClass('form-control', 'is-invalid');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('Kurumsal adres Zorunlu alan');
    expect(screen.getByText('Zorunlu alan')).toHaveClass('invalid-feedback');
  });

  it('links the label, hint and error of a text area', () => {
    render(<TextAreaField label="Açıklama" hint="En az 10 karakter" error="Çok kısa" />);
    const area = screen.getByLabelText('Açıklama');
    expect(area).toHaveClass('form-control', 'is-invalid');
    expect(area).toHaveAccessibleDescription('En az 10 karakter Çok kısa');
  });

  it('links the label, hint and error of a select and keeps the placeholder option', () => {
    render(<SelectField label="Ekip" placeholder="Seçiniz" hint="Karşılama ekibi" error="Seçim yapın" options={[{ value: 't1', label: 'Ağ' }]} />);
    const select = screen.getByLabelText('Ekip');
    expect(select).toHaveClass('form-select', 'is-invalid');
    expect(select).toHaveAccessibleDescription('Karşılama ekibi Seçim yapın');
    expect(screen.getByRole('option', { name: 'Seçiniz' })).toHaveValue('');
  });

  it('has no description and is valid without hint or error', () => {
    render(<TextField label="Ad" />);
    const input = screen.getByLabelText('Ad');
    expect(input).not.toHaveAttribute('aria-describedby');
    expect(input).not.toHaveAttribute('aria-invalid');
  });
});

describe('states and feedback', () => {
  it('announces loading with a spinner', () => {
    render(<LoadingState />);
    expect(screen.getByRole('status')).toHaveTextContent('Yükleniyor');
  });

  it('shows an empty message and an optional action', () => {
    render(<EmptyState message="Kayıt yok" action={<Button>Yeni</Button>} />);
    expect(screen.getByText('Kayıt yok')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Yeni' })).toBeInTheDocument();
  });

  it('shows an error alert whose retry calls back', async () => {
    const onRetry = vi.fn();
    render(<ErrorState error={new Error('x')} onRetry={onRetry} />);
    expect(screen.getByRole('alert')).toHaveClass('alert', 'alert-danger');
    await userEvent.click(screen.getByRole('button', { name: 'Tekrar dene' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('renders a form alert only with a message', () => {
    const { rerender } = render(<FormAlert message={null} />);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    rerender(<FormAlert message="Hata" />);
    expect(screen.getByRole('alert')).toHaveClass('alert-danger');
  });

  it('always names the status in text, not only by colour', () => {
    render(<StatusBadge isActive={false} />);
    expect(screen.getByText('Pasif')).toHaveClass('badge');
  });
});

describe('Pagination', () => {
  it('moves between pages and disables the ends', async () => {
    const onPageChange = vi.fn();
    render(<Pagination page={1} pageSize={10} totalCount={25} onPageChange={onPageChange} />);
    expect(screen.getByRole('button', { name: 'Önceki' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Sonraki' }));
    expect(onPageChange).toHaveBeenCalledWith(2);
    expect(screen.getByText(/1–10/)).toBeInTheDocument();
  });
});

describe('ResponsiveList', () => {
  const items = [{ id: 'a', name: 'Yazıcı' }];
  it('renders cards for phones and a captioned table for wider screens from the same items', () => {
    render(
      <ResponsiveList
        items={items}
        getKey={(item) => item.id}
        caption="Cihazlar"
        columns={[{ key: 'name', header: 'Ad', renderCell: (item) => item.name }]}
        renderCard={(item) => <span>{item.name}</span>}
      />,
    );
    const table = screen.getByRole('table', { name: 'Cihazlar' });
    expect(table).toHaveClass('table');
    expect(table.closest('.d-none.d-md-block')).not.toBeNull();
    expect(screen.getByRole('list').closest('.d-md-none')).not.toBeNull();
  });
});
