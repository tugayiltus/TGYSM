import { Dialog as RadixDialog } from 'radix-ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

export interface DialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  footer?: ReactNode;
  children?: ReactNode;
}

/** Bottom sheet on phones, centered modal from 640 px. Focus is trapped and returned by Radix. */
export function Dialog({ open, onOpenChange, title, description, footer, children }: DialogProps) {
  const { t } = useTranslation();
  return (
    <RadixDialog.Root open={open} onOpenChange={onOpenChange}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 z-40 bg-slate-900/40" />
        <RadixDialog.Content
          {...(description === undefined ? { 'aria-describedby': undefined } : {})}
          className="fixed inset-x-0 bottom-0 z-50 flex max-h-[90dvh] flex-col rounded-t-2xl bg-white shadow-xl sm:inset-x-auto sm:top-1/2 sm:bottom-auto sm:left-1/2 sm:w-full sm:max-w-lg sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-2xl"
        >
          <div className="flex items-start justify-between gap-4 border-b border-slate-100 p-4">
            <div>
              <RadixDialog.Title className="text-lg font-semibold">{title}</RadixDialog.Title>
              {description !== undefined && <RadixDialog.Description className="mt-1 text-sm text-slate-600">{description}</RadixDialog.Description>}
            </div>
            <RadixDialog.Close className="min-h-11 min-w-11 rounded-lg text-slate-500 hover:bg-slate-100" aria-label={t('actions.close')}>
              <span aria-hidden="true">×</span>
            </RadixDialog.Close>
          </div>
          <div className="overflow-y-auto p-4">{children}</div>
          {footer !== undefined && <div className="flex flex-col-reverse gap-2 border-t border-slate-100 p-4 sm:flex-row sm:justify-end">{footer}</div>}
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  );
}
