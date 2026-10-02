import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ArticleRevisionTable } from './ArticleRevisions';

describe('ArticleRevisionTable', () => {
  it('renders one row per revision with who and when', () => {
    render(
      <ArticleRevisionTable
        revisions={[
          { revision: 2, title: 'İkinci', visibility: 'internal', editedByName: 'Can Demir', editedAt: '2026-10-01T09:00:00Z' },
          { revision: 1, title: 'Birinci', visibility: 'employees', editedByName: 'Deniz Yıldız', editedAt: '2026-09-30T09:00:00Z' },
        ]}
      />,
    );

    expect(screen.getAllByText('İkinci').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Deniz Yıldız').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Yalnızca BT personeli').length).toBeGreaterThan(0);
  });

  it('says so when there are no revisions', () => {
    render(<ArticleRevisionTable revisions={[]} />);

    expect(screen.getByText('Henüz sürüm kaydı yok.')).toBeInTheDocument();
  });
});
