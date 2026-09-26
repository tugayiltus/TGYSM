import { describe, expect, it } from 'vitest';

import type { LocationItem } from './api';
import { buildLocationTree, collectSubtreeIds, findLocationNode, flattenLocationTree } from './locationTree';

const item = (id: string, name: string, parentId: string | null, isActive = true): LocationItem => ({ id, name, parentId, isActive, version: 1 });

describe('locationTree', () => {
  const items = [
    item('b', 'Ceyhan Kampüsü', null),
    item('a', 'Çankaya Kampüsü', null),
    item('a1', 'A Blok', 'a'),
    item('a1f', 'Kat 1', 'a1'),
    item('orphan', 'Kayıp', 'missing-parent'),
  ];

  it('nests children, sorts with Turkish collation and treats orphans as roots', () => {
    const roots = buildLocationTree(items);

    expect(roots.map((node) => node.item.name)).toEqual(['Ceyhan Kampüsü', 'Çankaya Kampüsü', 'Kayıp']);
    expect(roots[1]?.children[0]?.children[0]?.path).toBe('Çankaya Kampüsü / A Blok / Kat 1');
  });

  it('flattens depth-first with depth levels', () => {
    const flat = flattenLocationTree(buildLocationTree(items));

    expect(flat.map((node) => [node.item.id, node.depth])).toEqual([
      ['b', 0],
      ['a', 0],
      ['a1', 1],
      ['a1f', 2],
      ['orphan', 0],
    ]);
  });

  it('collects a subtree and survives cyclic data', () => {
    const roots = buildLocationTree(items);
    const campus = findLocationNode(roots, 'a');

    expect(campus && [...collectSubtreeIds(campus)].sort()).toEqual(['a', 'a1', 'a1f']);
    expect(buildLocationTree([item('x', 'X', 'y'), item('y', 'Y', 'x')])).toEqual([]);
  });
});
