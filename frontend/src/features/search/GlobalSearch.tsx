/**
 * @input Nothing; the signed-in user's role limits what the server returns.
 * @output A top bar button that opens a search palette over tickets, changes, tasks, assets and articles.
 * @position `endContent` of `AppHeader`. The `/` key opens it.
 */
import { Button } from '@astryxdesign/core/Button';
import { CommandPalette } from '@astryxdesign/core/CommandPalette';
import { Icon } from '@astryxdesign/core/Icon';
import { Text } from '@astryxdesign/core/Text';
import type { SearchableItem, SearchSource } from '@astryxdesign/core/Typeahead';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';

import { useIsPhone } from '../../shared/list/useIsPhone';
import { Icons } from '../../shared/ui/icons';
import { useSession } from '../auth/session';

import { fetchSearch, SEARCH_DEBOUNCE_MS, SEARCH_MAX_LENGTH, SEARCH_MIN_LENGTH } from './api';
import { listPathForType, pathForHit } from './searchPaths';

const SHORTCUT_KEY = '/';
const NO_SELECTION = '';
/** Elements where a typed `/` is text, not a shortcut. */
const TEXT_FIELD_SELECTOR = 'input, textarea, select, [contenteditable=""], [contenteditable="true"], [role="textbox"]';
const DIALOG_SELECTOR = 'dialog[open], [role="dialog"], [role="alertdialog"]';

interface ResultItem extends SearchableItem<{ group: string }> {
  /** Route that opens when the row is chosen. */
  path: string;
}

function isShortcutBlocked(target: EventTarget | null): boolean {
  if (!(target instanceof Element)) {
    return false;
  }
  return target.closest(TEXT_FIELD_SELECTOR) !== null || document.querySelector(DIALOG_SELECTOR) !== null;
}

export function GlobalSearch() {
  const { t } = useTranslation('search');
  const navigate = useNavigate();
  const { role } = useSession();
  const isPhone = useIsPhone();
  const [isOpen, setIsOpen] = useState(false);
  const latest = useRef(0);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const release = useRef<(() => void) | undefined>(undefined);
  const paths = useRef(new Map<string, string>());

  const go = useCallback(
    (path: string) => {
      setIsOpen(false);
      void navigate(path);
    },
    [navigate],
  );

  // The document keydown listener is the only way to catch a key pressed outside any field.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== SHORTCUT_KEY || event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented || isShortcutBlocked(event.target)) {
        return;
      }
      event.preventDefault();
      setIsOpen(true);
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, []);

  const source = useMemo<SearchSource<ResultItem>>(() => {
    /** Settles the superseded wait with no items, so the palette never keeps a spinner for it. */
    const cancel = () => {
      latest.current += 1;
      clearTimeout(timer.current);
      release.current?.();
      release.current = undefined;
    };
    return {
      bootstrap: () => [],
      cancel,
      async search(raw) {
        cancel();
        const mine = latest.current;
        const query = raw.trim();
        if (query.length < SEARCH_MIN_LENGTH || query.length > SEARCH_MAX_LENGTH) {
          return [];
        }
        await new Promise<void>((resolve) => {
          release.current = resolve;
          timer.current = setTimeout(resolve, SEARCH_DEBOUNCE_MS);
        });
        if (mine !== latest.current) {
          return [];
        }
        const result = await fetchSearch(query).catch(() => null);
        if (result === null || mine !== latest.current) {
          return [];
        }
        if (result.exact !== null) {
          go(pathForHit(result.exact, role));
          return [];
        }
        const items: ResultItem[] = [];
        for (const group of result.groups) {
          const title = t(`groups.${group.type}`);
          for (const hit of group.hits) {
            const path = pathForHit(hit, role);
            items.push({ id: `${group.type}:${hit.key}`, label: `${hit.key} ${hit.title}`, path, auxiliaryData: { group: title } });
          }
          if (group.hasMore) {
            items.push({ id: `all:${group.type}`, label: t('seeAll'), path: listPathForType(group.type, query, role), auxiliaryData: { group: title } });
          }
        }
        paths.current = new Map(items.map((item) => [item.id, item.path]));
        return items;
      },
    };
  }, [t, go, role]);

  const label = t('label');
  return (
    <>
      {isPhone ? (
        <Button label={label} variant="ghost" isIconOnly icon={<Icon icon={Icons.search} />} onClick={() => setIsOpen(true)} />
      ) : (
        <Button label={label} variant="ghost" icon={<Icon icon={Icons.search} />} onClick={() => setIsOpen(true)} />
      )}
      <CommandPalette<ResultItem>
        isOpen={isOpen}
        onOpenChange={setIsOpen}
        searchSource={source}
        label={label}
        width={isPhone ? '100%' : undefined}
        value={NO_SELECTION}
        onValueChange={(id) => {
          const path = paths.current.get(id);
          if (path !== undefined) {
            go(path);
          }
        }}
        emptySearchText={t('empty')}
        emptyBootstrapText={<Text>{t('hint')}</Text>}
        renderItem={(item) => <Text>{item.label}</Text>}
      />
    </>
  );
}
