import { CBreadcrumbItem } from '@coreui/react';
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import type { Crumb } from './crumbs';
import { useCrumbs } from './useCrumbs';

/** Root, "…", parent, current: the middle collapses into a menu below 768 px. */
const ALWAYS_SHOWN_TAIL = 2;
const COLLAPSE_FROM = ALWAYS_SHOWN_TAIL + 2;

function CrumbText({ label }: { label: string }) {
  return (
    <span className="d-inline-block text-truncate align-bottom tgysm-crumb" title={label}>
      {label}
    </span>
  );
}

function CrumbItem({ crumb, active = false, className }: { crumb: Crumb; active?: boolean; className?: string }) {
  return (
    <CBreadcrumbItem active={active} className={className}>
      {crumb.href === undefined || active ? (
        <CrumbText label={crumb.label} />
      ) : (
        <Link to={crumb.href}>
          <CrumbText label={crumb.label} />
        </Link>
      )}
    </CBreadcrumbItem>
  );
}

function CollapsedItems({ crumbs }: { crumbs: Crumb[] }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  return (
    <CBreadcrumbItem className="d-md-none position-relative">
      <button type="button" className="btn btn-link p-0 align-baseline" aria-haspopup="menu" aria-expanded={open} aria-label={t('breadcrumb.more')} onClick={() => setOpen((v) => !v)}>
        …
      </button>
      {open && (
        <ul role="menu" className="dropdown-menu show position-absolute">
          {crumbs.map((crumb) => (
            <li key={crumb.label} role="none">
              {crumb.href === undefined ? (
                <span className="dropdown-item-text">{crumb.label}</span>
              ) : (
                <Link role="menuitem" className="dropdown-item" to={crumb.href} onClick={() => setOpen(false)}>
                  {crumb.label}
                </Link>
              )}
            </li>
          ))}
        </ul>
      )}
    </CBreadcrumbItem>
  );
}

/** Where the user is in the app (spec §4.4); the last item is the current page. */
export function Breadcrumbs() {
  const { t } = useTranslation();
  const crumbs = useCrumbs();
  if (crumbs.length === 0) {
    return null;
  }
  const last = crumbs.length - 1;
  const collapse = crumbs.length >= COLLAPSE_FROM;
  const middle = collapse ? crumbs.slice(1, crumbs.length - ALWAYS_SHOWN_TAIL) : [];
  return (
    // Own <nav>: CoreUI's CBreadcrumb hard-codes an English aria-label.
    <nav aria-label={t('breadcrumb.label')} className="min-w-0 flex-grow-1">
      <ol className="breadcrumb my-0 small flex-nowrap min-w-0">
      {crumbs.map((crumb, index) => {
        const isMiddle = collapse && index >= 1 && index < crumbs.length - ALWAYS_SHOWN_TAIL;
        return (
          <FragmentWithCollapse key={`${index}-${crumb.label}`} showCollapse={collapse && index === 1} middle={middle}>
            <CrumbItem crumb={crumb} active={index === last} className={isMiddle ? 'd-none d-md-flex' : undefined} />
          </FragmentWithCollapse>
        );
      })}
      </ol>
    </nav>
  );
}

function FragmentWithCollapse({ showCollapse, middle, children }: { showCollapse: boolean; middle: Crumb[]; children: ReactNode }) {
  return (
    <>
      {showCollapse && <CollapsedItems crumbs={middle} />}
      {children}
    </>
  );
}
