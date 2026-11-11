/**
 * @input The label data of an asset and its scan link.
 * @output A PNG data URL of the 50×30 mm label at print resolution: QR on the start side, text lines beside it.
 * @position Shared by the detail card, the PNG download and the print view, so all three show the same label.
 */
import QRCode from 'qrcode';

import { LABEL_HEIGHT_MM, LABEL_WIDTH_MM, labelLines, type AssetLabelItem } from './scanLink';

const DOTS_PER_MM = 300 / 25.4;
const WIDTH = Math.round(LABEL_WIDTH_MM * DOTS_PER_MM);
const HEIGHT = Math.round(LABEL_HEIGHT_MM * DOTS_PER_MM);
/** Outer margin and the gap between QR and text. */
const PADDING = Math.round(2 * DOTS_PER_MM);
/** Quiet zone around the QR, in modules; scanners need it. */
const QUIET_MODULES = 2;
/** Font size of each line in dots: tag, brand and model, serial. */
const LINE_SIZES: readonly [number, ...number[]] = [52, 30, 26];
const SMALLEST_LINE_SIZE = 26;
const LINE_GAP = 14;
const FONT_FAMILY = 'Figtree, sans-serif';
const INK = '#000000';
const PAPER = '#ffffff';
const ELLIPSIS = '…';

/** Shortens a line to the width with an ellipsis instead of wrapping it off the label. */
function fit(context: CanvasRenderingContext2D, text: string, width: number): string {
  if (context.measureText(text).width <= width) {
    return text;
  }
  let end = text.length;
  while (end > 0 && context.measureText(`${text.slice(0, end)}${ELLIPSIS}`).width > width) {
    end -= 1;
  }
  return `${text.slice(0, end)}${ELLIPSIS}`;
}

export async function drawLabel(item: AssetLabelItem, link: string): Promise<string> {
  const canvas = document.createElement('canvas');
  canvas.width = WIDTH;
  canvas.height = HEIGHT;
  const context = canvas.getContext('2d');
  if (context === null) {
    throw new Error('Canvas 2D is not available.');
  }
  await document.fonts.ready;
  context.fillStyle = PAPER;
  context.fillRect(0, 0, WIDTH, HEIGHT);

  const qr = QRCode.create(link, { errorCorrectionLevel: 'M' });
  const qrSide = HEIGHT - 2 * PADDING;
  const moduleSize = Math.floor(qrSide / (qr.modules.size + 2 * QUIET_MODULES));
  const offset = PADDING + QUIET_MODULES * moduleSize;
  context.fillStyle = INK;
  for (let row = 0; row < qr.modules.size; row += 1) {
    for (let column = 0; column < qr.modules.size; column += 1) {
      if (qr.modules.get(row, column)) {
        context.fillRect(offset + column * moduleSize, offset + row * moduleSize, moduleSize, moduleSize);
      }
    }
  }

  const textStart = PADDING + qrSide + PADDING;
  const textWidth = WIDTH - textStart - PADDING;
  let baseline = PADDING;
  labelLines(item).forEach((line, index) => {
    const size = LINE_SIZES[index] ?? SMALLEST_LINE_SIZE;
    context.font = `${index === 0 ? 'bold ' : ''}${size}px ${FONT_FAMILY}`;
    baseline += size;
    context.fillText(fit(context, line, textWidth), textStart, baseline);
    baseline += LINE_GAP;
  });
  return canvas.toDataURL('image/png');
}
