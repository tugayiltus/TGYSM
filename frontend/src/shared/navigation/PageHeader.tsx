import { useEffect, useRef, type ReactNode } from 'react';
import { useLocation } from 'react-router';

import { useCrumbs } from './useCrumbs';

/** Pathname seen by the last mounted header; focus moves to the new H1 only on navigation, not on first load. */
let lastPathname: string | null = null;

export interface PageHeaderProps {
  /** Primary page actions, right of the title on wide screens. */
  actions?: ReactNode;
  /** Title when it differs from the breadcrumb's current item. */
  titleOverride?: string;
}

/** Page H1 and primary actions (spec §4.1); the layout renders the breadcrumb above it. The H1 takes focus after in-app navigation. */
export function PageHeader({ actions, titleOverride }: PageHeaderProps) {
  const crumbs = useCrumbs();
  const { pathname } = useLocation();
  const headingRef = useRef<HTMLHeadingElement>(null);
  const title = titleOverride ?? crumbs.at(-1)?.label ?? '';

  useEffect(() => {
    if (lastPathname !== null && lastPathname !== pathname) {
      headingRef.current?.focus();
    }
    lastPathname = pathname;
  }, [pathname]);

  return (
    <div className="mb-3">
      <div className="d-flex flex-wrap align-items-center justify-content-between gap-2">
        <h1 ref={headingRef} tabIndex={-1} className="h3 mb-0 text-break tgysm-page-title">
          {title}
        </h1>
        {actions !== undefined && <div className="d-flex flex-wrap gap-2">{actions}</div>}
      </div>
    </div>
  );
}
