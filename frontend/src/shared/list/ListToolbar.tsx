/**
 * @input Search value, filter controls, active filter count, and change and clear callbacks.
 * @output An optional search box with inline filters, or a "Filtreler (n)" button with a drawer on phones.
 * @position Above the `DataList` of every list page.
 */
import { Button } from '@astryxdesign/core/Button';
import { Drawer, DrawerHeader } from '@astryxdesign/core/Drawer';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { StackItem } from '@astryxdesign/core/Stack';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { useIsPhone } from './useIsPhone';

export interface ListToolbarProps {
  /** Heading of the phone drawer. */
  title: string;
  /** Search text; leave out search and onSearchChange for a toolbar with filters only. */
  search?: string;
  /** Label of the search box; defaults to "Ara". */
  searchLabel?: string;
  onSearchChange?: (value: string) => void;
  /** Filter controls; they apply as they change. */
  filters?: ReactNode;
  activeCount: number;
  onClear: () => void;
}

export function ListToolbar({ title, search, searchLabel, onSearchChange, filters, activeCount, onClear }: ListToolbarProps) {
  const { t } = useTranslation();
  const isPhone = useIsPhone();
  const [drawerOpen, setDrawerOpen] = useState(false);

  const label = searchLabel ?? t('filters.search');
  const searchBox = search !== undefined && onSearchChange !== undefined && (
    <TextInput type="search" label={label} isLabelHidden placeholder={label} startIcon="search" value={search} onChange={onSearchChange} />
  );
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
  // A toolbar with only a search box needs no drawer.
  if (filters === undefined) {
    return (
      <HStack gap={2} vAlign="end">
        <StackItem size="fill">{searchBox}</StackItem>
        {clearButton}
      </HStack>
    );
  }
  // The search box takes the free width, so the "Filtreler (n)" button keeps its full label.
  return (
    <HStack gap={2} vAlign="end">
      {searchBox && <StackItem size="fill">{searchBox}</StackItem>}
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
