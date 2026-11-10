/**
 * @input A destination, the router state to carry, a ref, and the link text.
 * @output A link that keeps the router state, styled as Astryx text.
 * @position Rows and cards that open a record and must lead back to their list (breadcrumb return).
 */
import { useLinkComponent } from '@astryxdesign/core/Link';
import { Text } from '@astryxdesign/core/Text';
import type { ReactNode, Ref } from 'react';

import type { linkState } from './returnTo';

export interface StateLinkProps {
  href: string;
  state: ReturnType<typeof linkState>;
  children: ReactNode;
  ref?: Ref<HTMLAnchorElement>;
}

/** Astryx links take no router state. The resolved link component is React Router's, which accepts `state`. */
export function StateLink({ href, state, children, ref }: StateLinkProps) {
  const LinkComponent = useLinkComponent() as (props: { href: string; to: string; state: StateLinkProps['state']; ref?: Ref<HTMLAnchorElement>; children: ReactNode }) => ReactNode;
  return (
    <LinkComponent href={href} to={href} state={state} ref={ref}>
      <Text type="inherit" color="inherit">
        {children}
      </Text>
    </LinkComponent>
  );
}
