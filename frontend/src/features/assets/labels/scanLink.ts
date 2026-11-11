/**
 * @input The company address, an asset id and the label data of an asset.
 * @output The permanent scan link a QR label holds, and the text lines printed beside the QR.
 * @position Asset QR labels: detail card, PNG download and the print view (ADR-0028).
 */
import type { components } from '../../../shared/api/schema';

export type AssetLabelItem = components['schemas']['AssetLabelItem'];

/** Thermal label size; one label per page. */
export const LABEL_WIDTH_MM = 50;
export const LABEL_HEIGHT_MM = 30;

const SCAN_PATH = '/scan/asset';

/** The link stays valid as long as the asset exists: it is built from the id, nothing is stored. */
export const scanLink = (publicBaseUrl: string, assetId: string): string => `${publicBaseUrl}${SCAN_PATH}/${assetId}`;

/** Tag first (printed largest), then brand and model, then the serial number when there is one. */
export const labelLines = (item: AssetLabelItem): string[] =>
  [item.assetTag, `${item.brand} ${item.model}`, item.serialNumber ?? ''].filter((line) => line !== '');
