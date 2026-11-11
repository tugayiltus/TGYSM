/**
 * @input Label data with the company address.
 * @output One PNG data URL per label, in the same order, once all are drawn.
 * @position The detail card and the print view.
 */
import { useQuery } from '@tanstack/react-query';

import { drawLabel } from './drawLabel';
import { scanLink, type AssetLabelItem } from './scanLink';

export interface LabelImage {
  item: AssetLabelItem;
  src: string;
}

export function useLabelImages(publicBaseUrl: string | undefined, items: readonly AssetLabelItem[] | undefined) {
  return useQuery({
    queryKey: ['assets', 'labelImages', publicBaseUrl, ...(items ?? []).map((item) => item.id)],
    queryFn: async (): Promise<LabelImage[]> =>
      Promise.all((items ?? []).map(async (item) => ({ item, src: await drawLabel(item, scanLink(publicBaseUrl ?? '', item.id)) }))),
    enabled: publicBaseUrl !== undefined && items !== undefined,
    staleTime: Infinity,
  });
}
