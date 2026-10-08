import { CToast, CToastBody } from '@coreui/react';
import { useCallback, useMemo, useRef, useState, type PropsWithChildren } from 'react';
import { useTranslation } from 'react-i18next';

import { TOAST_TIMEOUT_MS, ToastContext, type ToastTone } from './useToast';

interface ToastItem {
  id: number;
  message: string;
  tone: ToastTone;
}

/** One polite live region for short confirmations (e.g. the asset tag assigned on save). */
export function ToastProvider({ children }: PropsWithChildren) {
  const { t } = useTranslation();
  const [items, setItems] = useState<ToastItem[]>([]);
  const nextId = useRef(0);

  const show = useCallback((message: string, tone: ToastTone = 'success') => {
    nextId.current += 1;
    const id = nextId.current;
    setItems((current) => [...current, { id, message, tone }]);
    setTimeout(() => setItems((current) => current.filter((item) => item.id !== id)), TOAST_TIMEOUT_MS);
  }, []);
  const api = useMemo(() => ({ show }), [show]);

  return (
    <ToastContext value={api}>
      {children}
      <div role="status" aria-live="polite" aria-label={t('toast.label')} className="toast-container position-fixed bottom-0 end-0 p-3">
        {items.map((item) => (
          <CToast key={item.id} visible autohide={false} color={item.tone} className="text-white border-0">
            <CToastBody>{item.message}</CToastBody>
          </CToast>
        ))}
      </div>
    </ToastContext>
  );
}
