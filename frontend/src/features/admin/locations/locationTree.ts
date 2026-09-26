import { TURKISH_LOCALE } from '../../../shared/lib/turkish';

import type { LocationItem } from './api';

export interface LocationNode {
  item: LocationItem;
  depth: number;
  /** "Kampüs / Bina / Kat" — used as option labels. */
  path: string;
  children: LocationNode[];
}

const PATH_SEPARATOR = ' / ';
const ROOT_KEY = null;
const collator = new Intl.Collator(TURKISH_LOCALE);

/** Builds the tree; items whose parent is missing become roots; cyclic data is dropped instead of looping. */
export function buildLocationTree(items: readonly LocationItem[]): LocationNode[] {
  const ids = new Set(items.map((item) => item.id));
  const childrenOf = new Map<string | null, LocationItem[]>();
  for (const item of items) {
    const parentKey = item.parentId !== null && item.parentId !== undefined && ids.has(item.parentId) ? item.parentId : ROOT_KEY;
    childrenOf.set(parentKey, [...(childrenOf.get(parentKey) ?? []), item]);
  }

  const build = (parentKey: string | null, depth: number, parentPath: string, ancestors: ReadonlySet<string>): LocationNode[] =>
    [...(childrenOf.get(parentKey) ?? [])]
      .filter((item) => !ancestors.has(item.id))
      .sort((left, right) => collator.compare(left.name, right.name))
      .map((item) => {
        const path = parentPath === '' ? item.name : `${parentPath}${PATH_SEPARATOR}${item.name}`;
        return { item, depth, path, children: build(item.id, depth + 1, path, new Set([...ancestors, item.id])) };
      });

  return build(ROOT_KEY, 0, '', new Set());
}

export function flattenLocationTree(nodes: readonly LocationNode[]): LocationNode[] {
  return nodes.flatMap((node) => [node, ...flattenLocationTree(node.children)]);
}

export function findLocationNode(nodes: readonly LocationNode[], id: string): LocationNode | undefined {
  return flattenLocationTree(nodes).find((node) => node.item.id === id);
}

/** The node and all its descendants: invalid parents when moving this node. */
export function collectSubtreeIds(node: LocationNode): Set<string> {
  return new Set(flattenLocationTree([node]).map((entry) => entry.item.id));
}
