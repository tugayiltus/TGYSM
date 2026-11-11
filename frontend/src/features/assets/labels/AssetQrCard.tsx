/**
 * @input An asset id and tag.
 * @output The "QR kod" card: the label preview, "İndir" (label PNG) and "Etiketi yazdır" (print view).
 * @position Side column of the asset detail page (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Thumbnail } from '@astryxdesign/core/Thumbnail';
import { useTranslation } from 'react-i18next';

import { ErrorState } from '../../../shared/feedback/States';
import { SectionCard } from '../../../shared/page/SectionCard';
import { assetLabelsPath } from '../../../shared/routes';
import { Icons } from '../../../shared/ui/icons';
import { useAssetLabels } from '../api';

import { useLabelImages } from './useLabelImages';

/** Saves a data URL as a file; the anchor exists only for the click. */
function saveFile(src: string, fileName: string) {
  const anchor = document.createElement('a');
  anchor.href = src;
  anchor.download = fileName;
  anchor.click();
}

export function AssetQrCard({ assetId, assetTag }: { assetId: string; assetTag: string }) {
  const { t } = useTranslation('assets');
  const labels = useAssetLabels([assetId]);
  const images = useLabelImages(labels.data?.publicBaseUrl, labels.data?.items);
  const image = images.data?.[0];
  const error = labels.error ?? images.error;

  return (
    <SectionCard title={t('labels.card')}>
      {error !== null ? (
        <ErrorState error={error} onRetry={() => void labels.refetch()} />
      ) : (
        <Thumbnail src={image?.src} alt={t('labels.image', { tag: assetTag })} isLoading={image === undefined} />
      )}
      <HStack gap={2} wrap="wrap">
        <Button
          label={t('labels.download')}
          variant="secondary"
          icon={<Icon icon={Icons.download} />}
          isDisabled={image === undefined}
          onClick={() => image !== undefined && saveFile(image.src, `${assetTag}.png`)}
        />
        <Button label={t('labels.print')} variant="secondary" icon={<Icon icon={Icons.printer} />} href={assetLabelsPath(assetId)} />
      </HStack>
    </SectionCard>
  );
}
