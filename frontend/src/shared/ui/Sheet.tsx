import { CCloseButton, COffcanvas, COffcanvasBody, COffcanvasHeader, COffcanvasTitle } from '@coreui/react';
import { useId, type KeyboardEvent, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';

const ESCAPE_KEY = 'Escape';
const OPEN_MODAL_SELECTOR = '.modal';

export interface SheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  placement?: 'start' | 'end' | 'bottom';
  children: ReactNode;
}

/** Side panel (navigation, filters) on small screens. */
export function Sheet({ open, onOpenChange, title, placement = 'start', children }: SheetProps) {
  const { t } = useTranslation();
  const titleId = useId();
  // Escape closes only the top-most layer: a dialog opened from this sheet handles its own Escape.
  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === ESCAPE_KEY && document.querySelector(OPEN_MODAL_SELECTOR) === null) {
      onOpenChange(false);
    }
  };
  // COffcanvas keeps a hidden panel in the DOM; unmount it so closed sheets are not extra dialogs.
  if (!open) {
    return null;
  }
  // Our own portal for the same reason as Dialog: CoreUI's portal remounts the panel one render after opening.
  return createPortal(
    <COffcanvas visible={open} onHide={() => onOpenChange(false)} placement={placement} portal={false} keyboard={false} onKeyDown={handleKeyDown} aria-labelledby={titleId}>
      <COffcanvasHeader>
        <COffcanvasTitle id={titleId}>{title}</COffcanvasTitle>
        <CCloseButton aria-label={t('actions.close')} onClick={() => onOpenChange(false)} />
      </COffcanvasHeader>
      <COffcanvasBody>{children}</COffcanvasBody>
    </COffcanvas>,
    document.body,
  );
}
