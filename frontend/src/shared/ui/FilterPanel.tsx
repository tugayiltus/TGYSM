import { CCollapse } from '@coreui/react';
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from './Button';
import { Sheet } from './Sheet';

export interface FilterPanelProps {
  title: string;
  activeCount: number;
  /** Filters always shown on wide screens and first in the phone panel. */
  primary: ReactNode;
  /** Further filters, collapsed under "Diğer filtreler" on wide screens. */
  more?: ReactNode;
  /** Called when the phone panel opens, so the caller can copy applied values into its draft. */
  onOpen?: () => void;
  onApply: () => void;
  onClear: () => void;
  /** Filters apply as they change: wide screens then show only Temizle, and the phone panel's Uygula just closes it. */
  live?: boolean;
}

/**
 * Filters inline from 992 px; on phones a "Filtreler (n)" button opens a side panel with Uygula / Temizle / Vazgeç.
 * Vazgeç closes without applying (the caller's draft is reset on the next open), Temizle clears and applies.
 */
export function FilterPanel({ title, activeCount, primary, more, onOpen, onApply, onClear, live = false }: FilterPanelProps) {
  const { t } = useTranslation();
  const [sheetOpen, setSheetOpen] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false);

  const actions = (close: () => void, withApply: boolean) => (
    <div className="d-flex flex-wrap gap-2">
      {withApply && (
        <Button
          onClick={() => {
            onApply();
            close();
          }}
        >
          {t('filters.apply')}
        </Button>
      )}
      <Button
        variant="secondary"
        onClick={() => {
          onClear();
          close();
        }}
      >
        {t('filters.clear')}
      </Button>
    </div>
  );

  return (
    <>
      <div className="d-lg-none">
        <Button
          variant="secondary"
          onClick={() => {
            onOpen?.();
            setSheetOpen(true);
          }}
        >
          {t('filters.button', { count: activeCount })}
        </Button>
      </div>
      <Sheet open={sheetOpen} onOpenChange={setSheetOpen} title={title} placement="end">
        <div className="d-flex flex-column gap-3">
          {primary}
          {more}
          {actions(() => setSheetOpen(false), true)}
          <Button variant="ghost" onClick={() => setSheetOpen(false)}>
            {t('actions.cancel')}
          </Button>
        </div>
      </Sheet>
      <section aria-label={title} className="d-none d-lg-flex flex-column gap-3">
        <div className="d-flex flex-wrap align-items-end gap-3">{primary}</div>
        {more !== undefined && (
          <>
            <Button variant="ghost" className="align-self-start" aria-expanded={moreOpen} onClick={() => setMoreOpen((current) => !current)}>
              {t('filters.more')}
            </Button>
            <CCollapse visible={moreOpen}>
              <div className="d-flex flex-wrap align-items-end gap-3">{more}</div>
            </CCollapse>
          </>
        )}
        {actions(() => undefined, !live)}
      </section>
    </>
  );
}
