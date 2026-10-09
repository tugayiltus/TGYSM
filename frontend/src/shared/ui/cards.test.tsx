import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DataCard } from './DataCard';
import { FormSection } from './FormSection';
import { PageCard } from './PageCard';
import { ResponsiveList } from './ResponsiveList';
import { LoadingState } from './States';

describe('card building blocks', () => {
  it('PageCard names its region by the title and shows actions and footer', () => {
    render(
      <PageCard title="Bilgiler" actions={<button type="button">Düzenle</button>} footer={<span>alt</span>}>
        içerik
      </PageCard>,
    );
    const region = screen.getByRole('region', { name: 'Bilgiler' });
    expect(within(region).getByRole('button', { name: 'Düzenle' })).toBeInTheDocument();
    expect(within(region).getByText('içerik')).toBeInTheDocument();
    expect(within(region).getByText('alt')).toBeInTheDocument();
    expect(region).toHaveClass('card');
  });

  it('PageCard without a title is not a landmark', () => {
    render(<PageCard>içerik</PageCard>);
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
  });

  it('DataCard puts filters in the header, the list in a flush body and paging in the footer', () => {
    const { container } = render(
      <DataCard filters={<span>filtre</span>} footer={<span>sayfa</span>}>
        <span>liste</span>
      </DataCard>,
    );
    expect(container.querySelector('.card-header')).toHaveTextContent('filtre');
    expect(container.querySelector('.card-body')).toHaveClass('p-0', 'tgysm-card-body-min');
    expect(container.querySelector('.card-footer')).toHaveTextContent('sayfa');
  });

  it('FormSection lays fields out in a grid and right-aligns its actions', () => {
    const { container } = render(
      <FormSection title="Hesap" actions={<button type="submit">Kaydet</button>}>
        <div className="col-12">alan</div>
      </FormSection>,
    );
    expect(container.querySelector('.card-body > .row.g-3')).not.toBeNull();
    expect(container.querySelector('.card-footer')).toHaveClass('justify-content-end');
  });

  it('LoadingState reserves the space of the rows it stands for', () => {
    render(<LoadingState rows={3} />);
    const status = screen.getByRole('status');
    expect(status.querySelectorAll('.tgysm-skeleton-row')).toHaveLength(3);
  });

  it('ResponsiveList sits flush in a card: list-group items on phones, a borderless table above', () => {
    const { container } = render(
      <ResponsiveList items={[{ id: '1', name: 'A' }]} columns={[{ key: 'name', header: 'Ad', renderCell: (item) => item.name }]} getKey={(item) => item.id} renderCard={(item) => item.name} caption="Liste" />,
    );
    expect(container.querySelector('li')).toHaveClass('list-group-item');
    expect(container.querySelector('li')).not.toHaveClass('card');
    expect(container.querySelector('.table-responsive')).not.toHaveClass('border');
    expect(container.querySelector('thead')).not.toHaveClass('table-light');
    expect(container.querySelector('table')).toHaveClass('align-middle');
  });
});
