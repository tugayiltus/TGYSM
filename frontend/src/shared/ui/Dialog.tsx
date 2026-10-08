import { CCloseButton, CModal, CModalBody, CModalFooter, CModalHeader, CModalTitle } from '@coreui/react';
import { useId, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';

export interface DialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  footer?: ReactNode;
  children?: ReactNode;
}

/**
 * Full screen on phones, centred modal from 576 px. CoreUI traps focus and returns it to the trigger.
 * The portal is ours: CoreUI's own portal renders in place first and moves to <body> one render later,
 * which remounts the dialog (lost focus and input state on open).
 */
export function Dialog({ open, onOpenChange, title, description, footer, children }: DialogProps) {
  const { t } = useTranslation();
  const titleId = useId();
  const descriptionId = useId();
  return createPortal(
    <CModal
      visible={open}
      onClose={() => onOpenChange(false)}
      alignment="center"
      fullscreen="sm"
      portal={false}
      scrollable
      aria-labelledby={titleId}
      {...(description === undefined ? {} : { 'aria-describedby': descriptionId })}
    >
      <CModalHeader closeButton={false}>
        <div className="flex-grow-1">
          <CModalTitle id={titleId}>{title}</CModalTitle>
          {description !== undefined && (
            <p id={descriptionId} className="mb-0 mt-1 small text-body-secondary">
              {description}
            </p>
          )}
        </div>
        <CCloseButton aria-label={t('actions.close')} onClick={() => onOpenChange(false)} />
      </CModalHeader>
      <CModalBody>{children}</CModalBody>
      {footer !== undefined && <CModalFooter className="flex-column-reverse flex-sm-row gap-2">{footer}</CModalFooter>}
    </CModal>,
    document.body,
  );
}
