import { createContext, use } from 'react';

export const TOAST_TIMEOUT_MS = 5000;

export type ToastTone = 'success' | 'danger' | 'info';

export interface ToastApi {
  show: (message: string, tone?: ToastTone) => void;
}

export const ToastContext = createContext<ToastApi | null>(null);

export function useToast(): ToastApi {
  const api = use(ToastContext);
  if (api === null) {
    throw new Error('useToast must be used inside ToastProvider.');
  }
  return api;
}
