/**
 * @input Asset ids from navigation state (bulk print) or the `ids` query (one asset).
 * @output One 50×30 mm label per page, then the browser print dialog.
 * @position `/assets/labels`, outside the app shell so only the labels print (IT staff and admins).
 */
import { Thumbnail } from '@astryxdesign/core/Thumbnail';
import { VStack } from '@astryxdesign/core/VStack';
import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useSearchParams } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { useAssetLabels } from '../api';

import { LABEL_HEIGHT_MM, LABEL_WIDTH_MM } from './scanLink';
import { useLabelImages } from './useLabelImages';

const IDS_PARAM = 'ids';
const ID_SEPARATOR = ',';
/** Print geometry in millimetres has no Astryx token; each label image fills one page of the label roll. */
const PRINT_STYLE = `
@page { size: ${LABEL_WIDTH_MM}mm ${LABEL_HEIGHT_MM}mm; margin: 0; }
@media print {
  body { margin: 0; }
  img { width: ${LABEL_WIDTH_MM}mm; height: ${LABEL_HEIGHT_MM}mm; display: block; break-after: page; }
}`;

const idsFromState = (state: unknown): string[] | undefined =>
  typeof state === 'object' && state !== null && 'ids' in state && Array.isArray(state.ids) && state.ids.every((id) => typeof id === 'string') ? state.ids : undefined;

/** Navigation state of a bulk print from the inventory list. */
export interface LabelPrintState {
  ids: string[];
}

export function AssetLabelsPrintPage() {
  const { t } = useTranslation('assets');
  const location = useLocation();
  const [params] = useSearchParams();
  const ids = useMemo(
    () => idsFromState(location.state) ?? (params.get(IDS_PARAM) ?? '').split(ID_SEPARATOR).filter((id) => id !== ''),
    [location.state, params],
  );
  const labels = useAssetLabels(ids);
  const images = useLabelImages(labels.data?.publicBaseUrl, labels.data?.items);

  // Synchronizes with the browser print dialog: open it once every label is drawn.
  useEffect(() => {
    if (images.data !== undefined && images.data.length > 0) {
      window.print();
    }
  }, [images.data]);

  const error = labels.error ?? images.error;
  return (
    <VStack gap={2}>
      <style>{PRINT_STYLE}</style>
      {error !== null && <ErrorState error={error} onRetry={() => void labels.refetch()} />}
      {ids.length === 0 && <EmptyState message={t('labels.none')} />}
      {ids.length > 0 && error === null && images.data === undefined && <LoadingState />}
      {images.data?.map((image) => <Thumbnail key={image.item.id} src={image.src} alt={t('labels.image', { tag: image.item.assetTag })} />)}
    </VStack>
  );
}
