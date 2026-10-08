import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ArticleMarkdown } from './ArticleMarkdown';

describe('ArticleMarkdown', () => {
  it('renders script tags and raw HTML as text', () => {
    const { container } = render(<ArticleMarkdown body={'<script>alert(1)</script>\n\n<b>kalın</b>'} />);

    expect(container.querySelector('script')).toBeNull();
    expect(container.querySelector('b')).toBeNull();
    expect(container).toHaveTextContent('<script>alert(1)</script>');
    expect(container).toHaveTextContent('<b>kalın</b>');
  });

  it('opens links in a new tab without opener or referrer', () => {
    render(<ArticleMarkdown body="[Rehber](https://example.com/rehber)" />);

    const link = screen.getByRole('link', { name: 'Rehber' });
    expect(link).toHaveAttribute('href', 'https://example.com/rehber');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('never loads a remote image: it becomes a link with the alt text', () => {
    const { container } = render(<ArticleMarkdown body="![Şema](https://example.com/sema.png)" />);

    expect(container.querySelector('img')).toBeNull();
    const link = screen.getByRole('link', { name: 'Şema' });
    expect(link).toHaveAttribute('href', 'https://example.com/sema.png');
  });

  it('drops javascript: links', () => {
    render(<ArticleMarkdown body="[Tıkla](javascript:alert(1))" />);

    expect(screen.getByText('Tıkla').closest('a')?.getAttribute('href') ?? '').not.toMatch(/javascript:/i);
  });

  it('renders headings one level below the page title, capped at h6, and lists', () => {
    render(<ArticleMarkdown body={'# Giriş\n\n## Adımlar\n\n###### Not\n\n- bir\n- iki'} />);

    expect(screen.getByRole('heading', { level: 2, name: 'Giriş' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 3, name: 'Adımlar' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 6, name: 'Not' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).toBeNull();
    expect(screen.getAllByRole('listitem')).toHaveLength(2);
  });
});
