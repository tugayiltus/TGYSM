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
