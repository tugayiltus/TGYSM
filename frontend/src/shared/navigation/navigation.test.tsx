import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from 'i18next';
import { createMemoryRouter, Link, Outlet, RouterProvider, useLocation, type RouteObject } from 'react-router';
import { describe, expect, it } from 'vitest';

import { routes as appRoutes } from '../../app/router';
import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

import { useBreadcrumbLabel } from './breadcrumbLabels';
import { Breadcrumbs } from './Breadcrumbs';
import { buildCrumbs, type Crumb } from './crumbs';
import { linkState, parseReturn } from './returnTo';
import { ROUTE_IDS, ROUTE_META, type RouteId } from './routeMeta';

// Untyped key lookup, as the runtime builds keys from route metadata.
const t = (key: string): string => i18n.t(key as never);
const labels = (crumbs: Crumb[]) => crumbs.map((c) => c.label);
const hrefs = (crumbs: Crumb[]) => crumbs.map((c) => c.href ?? null);

/** Page routes (not layouts) with their full path; an index route takes its parent's path. */
function collectRoutes(routes: RouteObject[], parentPath = '/', into: { id?: string; path: string }[] = []) {
  for (const route of routes) {
    const fullPath = route.path ?? parentPath;
    if (route.children) {
      collectRoutes(route.children, fullPath, into);
    } else {
      into.push({ id: route.id, path: fullPath });
    }
  }
  return into;
}

describe('route metadata', () => {
  const PUBLIC_PATHS = ['/login', '/set-password', '/forgot-password', '*'];

  it('covers every signed-in route of the router with a matching path', () => {
    const signedIn = collectRoutes(appRoutes).filter((r) => !PUBLIC_PATHS.includes(r.path));
    for (const route of signedIn) {
      expect(route.id, `route ${route.path} needs an id`).toBeDefined();
      expect(ROUTE_IDS).toContain(route.id);
      expect(ROUTE_META[route.id as RouteId].path).toBe(route.path);
    }
  });

  it('has existing parents and no cycles', () => {
    for (const id of ROUTE_IDS) {
      const seen = new Set<string>([id]);
      let parent = ROUTE_META[id].parent;
      while (parent !== undefined) {
        expect(ROUTE_IDS).toContain(parent);
        expect(seen.has(parent), `cycle at ${id}`).toBe(false);
        seen.add(parent);
        parent = ROUTE_META[parent].parent;
      }
    }
  });
});

describe('buildCrumbs', () => {
  it('shows only the home page on home', () => {
    expect(labels(buildCrumbs({ routeId: 'home', params: {}, labels: {}, from: null, t }))).toEqual(['Ana sayfa']);
  });

  it('builds the ticket form trail with links to its parents', () => {
    const crumbs = buildCrumbs({ routeId: 'newIncident', params: {}, labels: {}, from: null, t });
    expect(labels(crumbs)).toEqual(['Ana sayfa', 'Kayıtlarım', 'Arıza bildir']);
    expect(hrefs(crumbs)).toEqual(['/', '/tickets', null]);
  });

  it('uses the loaded label for a detail and a neutral placeholder while loading', () => {
    expect(labels(buildCrumbs({ routeId: 'ticketDetail', params: { number: 'INC-1' }, labels: { ticketDetail: 'INC-000042' }, from: null, t }))).toEqual([
      'Ana sayfa',
      'Kayıtlarım',
      'INC-000042',
    ]);
    expect(labels(buildCrumbs({ routeId: 'ticketDetail', params: { number: 'INC-1' }, labels: {}, from: null, t })).at(-1)).toBe('…');
  });

  it('goes back to the filtered queue a ticket was opened from', () => {
    const from = parseReturn({ from: '/workspace/queue?view=team&priority=p1' });
    const crumbs = buildCrumbs({ routeId: 'ticketDetail', params: { number: 'INC-1' }, labels: { ticketDetail: 'INC-1' }, from, t });
    expect(labels(crumbs)).toEqual(['Ana sayfa', 'İş listem', 'Kuyruk', 'INC-1']);
    expect(crumbs[2]?.href).toBe('/workspace/queue?view=team&priority=p1');
  });

  it('falls back to the default parent for a direct asset URL', () => {
    const crumbs = buildCrumbs({ routeId: 'assetDetail', params: { id: 'a1' }, labels: { assetDetail: 'DMR-000001' }, from: null, t });
    expect(labels(crumbs)).toEqual(['Ana sayfa', 'Envanter', 'DMR-000001']);
    expect(crumbs[1]?.href).toBe('/assets');
  });

  it('links the article edit trail to the article page', () => {
    const crumbs = buildCrumbs({ routeId: 'kbEdit', params: { id: 'k1' }, labels: { kbArticle: 'VPN kurulumu' }, from: null, t });
    expect(labels(crumbs)).toEqual(['Ana sayfa', 'Bilgi bankası', 'VPN kurulumu', 'Düzenle']);
    expect(hrefs(crumbs)).toEqual(['/', '/kb', '/kb/k1', null]);
  });

  it('keeps the report filters on the way back from a metric list', () => {
    const from = parseReturn({ from: '/reports/incidents?from=2026-09-01&priority=p1' });
    const crumbs = buildCrumbs({ routeId: 'reportList', params: { report: 'incidents' }, labels: { reportList: 'Açılan kayıt' }, from, t });
    expect(labels(crumbs)).toEqual(['Ana sayfa', 'Raporlar', 'Olaylar', 'Açılan kayıt']);
    expect(crumbs[2]?.href).toBe('/reports/incidents?from=2026-09-01&priority=p1');
  });

  it('shows the admin group as plain text between Yönetim and the page', () => {
    const crumbs = buildCrumbs({ routeId: 'adminSlaPolicy', params: {}, labels: {}, from: null, t });
    expect(labels(crumbs)).toEqual(['Yönetim', 'SLA', 'SLA politikası']);
    expect(hrefs(crumbs)).toEqual(['/admin', null, null]);
    expect(labels(buildCrumbs({ routeId: 'adminOverview', params: {}, labels: {}, from: null, t }))).toEqual(['Yönetim', 'Genel bakış']);
  });
});

describe('parseReturn', () => {
  it.each(['//evil.example/x', 'https://evil.example/', '/\\evil.example', '/admin/users', '/tickets/INC-1', 42, null])('refuses %s', (from) => {
    expect(parseReturn({ from })).toBeNull();
  });

  it('accepts an in-app list with its query', () => {
    expect(parseReturn({ from: '/assets?status=assigned' })).toEqual({ routeId: 'assets', href: '/assets?status=assigned', params: {} });
  });

  it('builds link state from a location', () => {
    expect(linkState({ pathname: '/tickets', search: '?status=open' })).toEqual({ from: '/tickets?status=open' });
  });
});

function DetailPage({ label }: { label?: string }) {
  useBreadcrumbLabel('kbArticle', label);
  return <Breadcrumbs />;
}

function trailRouter(path: string, label?: string) {
  const routes: RouteObject[] = [
    {
      element: <Outlet />,
      children: [
        { id: 'kbList', path: '/kb', element: <Breadcrumbs /> },
        { id: 'kbArticle', path: '/kb/:id', element: <DetailPage label={label} /> },
        { id: 'kbEdit', path: '/kb/:id/edit', element: <EditPage /> },
      ],
    },
  ];
  return createMemoryRouter(routes, { initialEntries: [path] });
}

function EditPage() {
  useBreadcrumbLabel('kbArticle', 'Yazıcı kurulumu ve sürücü güncellemesi için çok uzun bir başlık '.repeat(2));
  const location = useLocation();
  return (
    <>
      <Breadcrumbs />
      <Link to="/kb" state={linkState(location)}>
        Liste
      </Link>
    </>
  );
}

describe('Breadcrumbs', () => {
  it('is a labelled navigation whose last item is the current page', () => {
    render(<RouterProvider router={trailRouter('/kb/k1', 'VPN kurulumu')} />);
    const nav = screen.getByRole('navigation', { name: 'Sayfa yolu' });
    expect(within(nav).getByRole('link', { name: 'Bilgi bankası' })).toHaveAttribute('href', '/kb');
    expect(within(nav).getByText('VPN kurulumu').closest('[aria-current="page"]')).not.toBeNull();
  });

  it('collapses the middle of a long trail into a working "…" menu on phones', async () => {
    const user = userEvent.setup();
    render(<RouterProvider router={trailRouter('/kb/k1/edit')} />);
    const nav = screen.getByRole('navigation', { name: 'Sayfa yolu' });
    const more = within(nav).getByRole('button', { name: 'Diğer üst sayfalar' });
    await user.click(more);
    expect(await screen.findByRole('menuitem', { name: 'Bilgi bankası' })).toHaveAttribute('href', '/kb');
  });

  it('truncates a very long title instead of widening the page', () => {
    render(<RouterProvider router={trailRouter('/kb/k1/edit')} />);
    const longItem = screen.getAllByText(/çok uzun bir başlık/)[0];
    expect(longItem).toHaveClass('text-truncate');
    expect(longItem).toHaveAttribute('title');
  });
});

describe('RequireSession', () => {
  it('keeps the query string when it sends a signed-out user to login and back', async () => {
    let signedIn = false;
    const mock = mockApi({ 'POST /api/v1/auth/login': { status: 204, body: null } });
    mock.set('GET /api/v1/auth/me', () => (signedIn ? { status: 200, body: EMPLOYEE_ME } : { status: 401, body: { code: 'auth.unauthenticated' } }));
    const { router } = renderRoute('/tickets?status=open');

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(router.state.location.state).toEqual({ from: '/tickets?status=open' });
    signedIn = true;
  });
});
