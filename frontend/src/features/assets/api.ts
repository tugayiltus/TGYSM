import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';

export type AssetItem = components['schemas']['AssetItem'];
export type AssetType = components['schemas']['AssetType'];
export type AssetStatus = components['schemas']['AssetStatus'];
export type AssetMovementItem = components['schemas']['AssetMovementItem'];
export type MyAssetItem = components['schemas']['MyAssetItem'];
export type AssetImportJobItem = components['schemas']['AssetImportJobItem'];
export type CreateAssetInput = components['schemas']['CreateAssetCommand'];
export type UpdateAssetInput = components['schemas']['UpdateAssetRequest'] & { id: string };
export type ReferenceUser = components['schemas']['ReferenceUser'];

export const ASSET_TYPES: readonly AssetType[] = ['desktop', 'laptop', 'phone', 'tablet'];
export const ASSET_STATUSES: readonly AssetStatus[] = ['in_stock', 'assigned', 'maintenance', 'lost', 'retired'];

export const assetKeys = {
  all: ['assets'],
  list: (params: AssetListParams) => ['assets', 'list', params],
  detail: (id: string) => ['assets', 'detail', id],
  history: (id: string) => ['assets', 'history', id],
  mine: ['assets', 'mine'],
  reference: ['reference'],
} as const;

export interface AssetListParams {
  search: string;
  type: AssetType | undefined;
  status: AssetStatus | undefined;
  page: number;
  pageSize: number;
}

export function useAssets(params: AssetListParams, enabled = true) {
  return useQuery({
    enabled,
    queryKey: assetKeys.list(params),
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/assets', {
          params: { query: { search: params.search === '' ? undefined : params.search, type: params.type, status: params.status, page: params.page, pageSize: params.pageSize } },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useAsset(id: string) {
  return useQuery({ queryKey: assetKeys.detail(id), queryFn: () => unwrap(api.GET('/api/v1/assets/{id}', { params: { path: { id } } })) });
}

export function useAssetHistory(id: string) {
  return useQuery({ queryKey: assetKeys.history(id), queryFn: () => unwrap(api.GET('/api/v1/assets/{id}/history', { params: { path: { id } } })) });
}

export function useMyAssets(enabled = true) {
  return useQuery({ queryKey: assetKeys.mine, queryFn: () => unwrap(api.GET('/api/v1/me/assets')), enabled });
}

export function useReferenceLocations() {
  return useQuery({ queryKey: [...assetKeys.reference, 'locations'], queryFn: () => unwrap(api.GET('/api/v1/reference/locations')) });
}

export function useReferenceDepartments() {
  return useQuery({ queryKey: [...assetKeys.reference, 'departments'], queryFn: () => unwrap(api.GET('/api/v1/reference/departments')) });
}

export function useUserSearch(search: string) {
  return useQuery({
    queryKey: [...assetKeys.reference, 'users', search],
    queryFn: () => unwrap(api.GET('/api/v1/reference/users', { params: { query: { search } } })),
    enabled: search.trim() !== '',
  });
}

function useInvalidateAssets() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: assetKeys.all });
}

export function useCreateAsset() {
  const invalidate = useInvalidateAssets();
  return useMutation({ mutationFn: (body: CreateAssetInput) => unwrap(api.POST('/api/v1/assets', { body })), onSuccess: invalidate });
}

export function useUpdateAsset() {
  const invalidate = useInvalidateAssets();
  return useMutation({
    mutationFn: ({ id, ...body }: UpdateAssetInput) => unwrap(api.PUT('/api/v1/assets/{id}', { params: { path: { id } }, body })),
    onSuccess: invalidate,
  });
}

/** One lifecycle operation; the body always carries the asset's version (ADR-0010). */
export type AssetOperation =
  | { kind: 'assign'; userId: string; note: string | null }
  | { kind: 'return'; locationId: string }
  | { kind: 'transfer'; userId: string; note: string | null }
  | { kind: 'maintenanceStart'; retainResponsibility: boolean }
  | { kind: 'maintenanceEnd' }
  | { kind: 'lost'; reason: string }
  | { kind: 'found'; locationId: string }
  | { kind: 'retire'; reason: string };

export type AssetOperationKind = AssetOperation['kind'];

/** Operations the domain allows per status (Asset.Require* in the backend). */
export const OPERATIONS_BY_STATUS: Readonly<Record<AssetStatus, readonly AssetOperationKind[]>> = {
  in_stock: ['assign', 'maintenanceStart', 'lost', 'retire'],
  assigned: ['return', 'transfer', 'maintenanceStart', 'lost', 'retire'],
  maintenance: ['maintenanceEnd', 'lost', 'retire'],
  lost: ['found', 'retire'],
  retired: [],
};

function runOperation(id: string, version: number, operation: AssetOperation) {
  const path = { params: { path: { id } } };
  switch (operation.kind) {
    case 'assign':
      return api.POST('/api/v1/assets/{id}/assign', { ...path, body: { version, userId: operation.userId, requestTicketId: null, note: operation.note } });
    case 'return':
      return api.POST('/api/v1/assets/{id}/return', { ...path, body: { version, locationId: operation.locationId } });
    case 'transfer':
      return api.POST('/api/v1/assets/{id}/transfer', { ...path, body: { version, userId: operation.userId, note: operation.note } });
    case 'maintenanceStart':
      return api.POST('/api/v1/assets/{id}/maintenance/start', { ...path, body: { version, retainResponsibility: operation.retainResponsibility } });
    case 'maintenanceEnd':
      return api.POST('/api/v1/assets/{id}/maintenance/end', { ...path, body: { version } });
    case 'lost':
      return api.POST('/api/v1/assets/{id}/lost', { ...path, body: { version, reason: operation.reason } });
    case 'found':
      return api.POST('/api/v1/assets/{id}/found', { ...path, body: { version, locationId: operation.locationId } });
    case 'retire':
      return api.POST('/api/v1/assets/{id}/retire', { ...path, body: { version, reason: operation.reason } });
  }
}

export function useAssetOperation(asset: AssetItem) {
  const invalidate = useInvalidateAssets();
  return useMutation({ mutationFn: (operation: AssetOperation) => unwrap(runOperation(asset.id, asset.version, operation)), onSuccess: invalidate });
}

export function usePreviewImport() {
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append('file', file);
      return unwrap(
        api.POST('/api/v1/assets/import/preview', {
          // The schema types the part as IFormFile; the browser sends the File and sets the multipart boundary.
          body: { file: file as unknown as components['schemas']['IFormFile'] },
          bodySerializer: () => form,
        }),
      );
    },
  });
}

export function useCommitImport() {
  const invalidate = useInvalidateAssets();
  return useMutation({
    mutationFn: (jobId: string) => unwrap(api.POST('/api/v1/assets/import/{jobId}/commit', { params: { path: { jobId } } })),
    onSuccess: invalidate,
  });
}
