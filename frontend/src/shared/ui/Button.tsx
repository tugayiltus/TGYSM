import { CButton, CSpinner } from '@coreui/react';
import type { ButtonHTMLAttributes, Ref } from 'react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

/** CoreUI colour + style per variant: solid for main actions, outline for secondary, ghost for low-emphasis. */
const VARIANT_STYLE: Record<Variant, { color: string; variant?: 'outline' | 'ghost' }> = {
  primary: { color: 'primary' },
  secondary: { color: 'secondary', variant: 'outline' },
  danger: { color: 'danger' },
  ghost: { color: 'secondary', variant: 'ghost' },
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  isLoading?: boolean;
  ref?: Ref<HTMLButtonElement>;
}

/** 44 px minimum touch target; disabled while loading so a double tap cannot submit twice. */
export function Button({ variant = 'primary', isLoading = false, disabled, className = '', type = 'button', children, ...rest }: ButtonProps) {
  const style = VARIANT_STYLE[variant];
  return (
    <CButton
      type={type}
      color={style.color}
      variant={style.variant}
      disabled={disabled === true || isLoading}
      aria-busy={isLoading || undefined}
      className={`d-inline-flex align-items-center justify-content-center gap-2 tgysm-touch ${className}`.trim()}
      {...rest}
    >
      {isLoading && <CSpinner size="sm" aria-hidden="true" />}
      {children}
    </CButton>
  );
}
