/**
 * @input Search value, filter controls, active filter count, and change and clear callbacks.
 * @output A search box with inline filters, or a "Filtreler (n)" button with a drawer on phones.
 * @position Above the `DataList` of every list page.
 */
import { Button } from '@astryxdesign/core/Button';
import { Drawer, DrawerHeader } from '@astryxdesign/core/Drawer';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { useIsPhone } from './useIsPhone';

export interface ListToolbarProps {
  /** Heading of the phone drawer. */
  title: string;
  search: string;
  onSearchChange: (value: string) => void;
  /** Filter controls; they apply as they change. */
  filters?: ReactNode;
  activeCount: number;
  onClear: () => void;
}

export function ListToolbar({ title, search, onSearchChange, filters, activeCount, onClear }: ListToolbarProps) {
  const { t } = useTranslation();
  const isPhone = useIsPhone();
  const [drawerOpen, setDrawerOpen] = useState(false);

  const searchBox = <TextInput type="search" label={t('filters.search')} isLabelHidden placeholder={t('filters.search')} startIcon="search" value={search} onChange={onSearchChange} />;
  const clearButton = activeCount > 0 && <Button label={t('filters.clear')} variant="ghost" onClick={onClear} />;

  if (!isPhone) {
    return (
      <HStack gap={2} wrap="wrap" vAlign="end">
        {searchBox}
        {filters}
        {clearButton}
      </HStack>
    );
  }
  return (
    <HStack gap={2} vAlign="end">
      {searchBox}
      <Button label={t('filters.button', { count: activeCount })} variant="secondary" icon={<Icon icon="funnel" />} onClick={() => setDrawerOpen(true)} />
      <Drawer label={title} isOpen={drawerOpen} onOpenChange={setDrawerOpen}>
        <DrawerHeader title={title} onOpenChange={setDrawerOpen} />
        <VStack gap={4} padding={4}>
          {filters}
          {clearButton}
        </VStack>
      </Drawer>
    </HStack>
  );
}
