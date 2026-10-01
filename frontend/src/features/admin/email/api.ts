import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type EmailTemplateItem = components['schemas']['EmailTemplateItem'];
export type EmailDeliveryItem = components['schemas']['EmailDeliveryItem'];
export type DeliveryStatus = components['schemas']['DeliveryStatus'];

export interface UpdateEmailTemplateInput {
  eventType: string;
  version: number;
  subject: string;
  body: string;
}

export function useEmailTemplates() {
  return useQuery({
    queryKey: adminKeys.emailTemplates,
    queryFn: () => unwrap(api.GET('/api/v1/admin/email-templates')),
  });
}

export function useUpdateEmailTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ eventType, ...body }: UpdateEmailTemplateInput) =>
      unwrap(api.PUT('/api/v1/admin/email-templates/{eventType}', { params: { path: { eventType } }, body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.emailTemplates }),
  });
}

export interface DeliveryListParams {
  status: DeliveryStatus | undefined;
  eventType: string | undefined;
  recipient: string | undefined;
  page: number;
  pageSize: number;
}

export function useEmailDeliveries(params: DeliveryListParams) {
  return useQuery({
    queryKey: [...adminKeys.emailDeliveries, params],
    queryFn: () => unwrap(api.GET('/api/v1/admin/email-deliveries', { params: { query: params } })),
    placeholderData: keepPreviousData,
  });
}

export function useRetryEmailDelivery() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.POST('/api/v1/admin/email-deliveries/{id}/retry', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.emailDeliveries }),
  });
}
