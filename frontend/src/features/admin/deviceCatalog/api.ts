import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';

export type BrandItem = components['schemas']['BrandItem'];
export type AssetModelItem = components['schemas']['AssetModelItem'];
export type AssetType = components['schemas']['AssetType'];

const deviceCatalogKeys = {
  brands: ['admin', 'brands'],
  models: ['admin', 'models'],
  // Asset forms read the active catalog under the asset reference keys.
  reference: ['reference'],
} as const;

function useInvalidateCatalog() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: deviceCatalogKeys.brands });
    await queryClient.invalidateQueries({ queryKey: deviceCatalogKeys.models });
    await queryClient.invalidateQueries({ queryKey: deviceCatalogKeys.reference });
  };
}

export function useBrands() {
  return useQuery({ queryKey: deviceCatalogKeys.brands, queryFn: () => unwrap(api.GET('/api/v1/admin/brands')) });
}

export function useAssetModels(brandId: string) {
  return useQuery({
    queryKey: [...deviceCatalogKeys.models, brandId],
    queryFn: () => unwrap(api.GET('/api/v1/admin/models', { params: { query: { brandId: brandId === '' ? undefined : brandId } } })),
  });
}

export function useSaveBrand() {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: ({ id, version, name }: { id: string | null; version: number; name: string }) =>
      id === null
        ? unwrap(api.POST('/api/v1/admin/brands', { body: { name } }))
        : unwrap(api.PUT('/api/v1/admin/brands/{id}', { params: { path: { id } }, body: { version, name } })),
    onSuccess: invalidate,
  });
}

export function useSaveAssetModel() {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: (input: { id: null; brandId: string; type: AssetType; name: string } | { id: string; version: number; name: string }) =>
      input.id === null
        ? unwrap(api.POST('/api/v1/admin/models', { body: { brandId: input.brandId, type: input.type, name: input.name } }))
        : unwrap(api.PUT('/api/v1/admin/models/{id}', { params: { path: { id: input.id } }, body: { version: input.version, name: input.name } })),
    onSuccess: invalidate,
  });
}

export function useSetBrandActive() {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive ? unwrap(api.POST('/api/v1/admin/brands/{id}/reactivate', options)) : unwrap(api.POST('/api/v1/admin/brands/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}

export function useSetAssetModelActive() {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive ? unwrap(api.POST('/api/v1/admin/models/{id}/reactivate', options)) : unwrap(api.POST('/api/v1/admin/models/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}
