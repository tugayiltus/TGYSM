import type { AssetItem } from '../features/assets/api';

export const pageOf = <T,>(items: T[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });

export const LOCATIONS = [
  { id: 'l-1', path: 'Merkez Kampüs' },
  { id: 'l-2', path: 'Merkez Kampüs / A Blok' },
];

export const DEPARTMENTS = [{ id: 'd-1', name: 'Muhasebe' }];

export const LAPTOP: AssetItem = {
  id: 'a-1',
  assetTag: 'DT-0001',
  type: 'laptop',
  modelId: 'm-1',
  brand: 'Lenovo',
  model: 'T14',
  serialNumber: 'SN-1',
  status: 'in_stock',
  locationId: 'l-1',
  locationName: 'Merkez Kampüs',
  departmentId: null,
  departmentName: null,
  assignedUserId: null,
  assignedUserName: null,
  warrantyEndsOn: '2027-12-31',
  purchasedOn: null,
  notes: null,
  createdAt: '2026-09-01T08:00:00Z',
  version: 7,
};

/** Active catalog for asset forms: Lenovo T14 (laptop), Lenovo M70q (desktop), Apple iPhone 15 (phone). */
export const CATALOG = [
  { id: 'b-2', name: 'Apple', models: [{ id: 'm-3', type: 'phone', name: 'iPhone 15' }] },
  {
    id: 'b-1',
    name: 'Lenovo',
    models: [
      { id: 'm-1', type: 'laptop', name: 'T14' },
      { id: 'm-2', type: 'desktop', name: 'M70q' },
    ],
  },
];
