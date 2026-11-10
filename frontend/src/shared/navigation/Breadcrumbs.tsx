/**
 * @input Nothing; reads the crumbs of the current route.
 * @output The page trail. On phones a long trail folds its middle into a menu.
 * @position Start of the top bar in the app shell.
 */
import { BreadcrumbItem, Breadcrumbs as AstryxBreadcrumbs } from '@astryxdesign/core/Breadcrumbs';
import { Icon } from '@astryxdesign/core/Icon';
import { Text } from '@astryxdesign/core/Text';
import { useTranslation } from 'react-i18next';

import { useIsPhone } from '../list/useIsPhone';

import type { Crumb } from './crumbs';
import { useCrumbs } from './useCrumbs';

/** Root, "…", parent, current: the middle folds on phones from four crumbs. */
const ALWAYS_SHOWN_TAIL = 2;
const COLLAPSE_FROM = ALWAYS_SHOWN_TAIL + 2;
/** A long title takes one line and shows in full on hover. */
const CRUMB_LINES = 1;

function CrumbLabel({ label }: { label: string }) {
  return (
    <Text type="inherit" maxLines={CRUMB_LINES}>
      {label}
    </Text>
  );
}

/** A menu row shows the whole title. A long title wraps, even inside one long word, so the menu stays on the screen. */
function WrappedLabel({ label }: { label: string }) {
  return (
    <Text type="inherit" textWrap="wrap" wordBreak="break-word">
      {label}
    </Text>
  );
}

export function Breadcrumbs() {
  const { t } = useTranslation();
  const crumbs = useCrumbs();
  const isPhone = useIsPhone();
  if (crumbs.length === 0) {
    return null;
  }
  const last = crumbs.length - 1;
  const collapse = isPhone && crumbs.length >= COLLAPSE_FROM;
  const middleEnd = crumbs.length - ALWAYS_SHOWN_TAIL;
  const item = (crumb: Crumb, index: number) => (
    <BreadcrumbItem key={`${index}-${crumb.label}`} href={index === last ? undefined : crumb.href} isCurrent={index === last}>
      <CrumbLabel label={crumb.label} />
    </BreadcrumbItem>
  );
  if (!collapse) {
    return <AstryxBreadcrumbs label={t('breadcrumb.label')}>{crumbs.map(item)}</AstryxBreadcrumbs>;
  }
  const [root, ...rest] = crumbs;
  const middle = crumbs.slice(1, middleEnd);
  return (
    <AstryxBreadcrumbs label={t('breadcrumb.label')}>
      {root !== undefined && item(root, 0)}
      <BreadcrumbItem menu={middle.map((crumb) => ({ label: <WrappedLabel label={crumb.label} />, href: crumb.href, isDisabled: crumb.href === undefined }))} isCurrent={false}>
        <Icon icon="moreHorizontal" label={t('breadcrumb.more')} />
      </BreadcrumbItem>
      {rest.slice(middle.length).map((crumb, offset) => item(crumb, middleEnd + offset))}
    </AstryxBreadcrumbs>
  );
}
