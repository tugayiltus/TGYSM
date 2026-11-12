/**
 * @input The label data of an asset and its scan link.
 * @output A PNG data URL of the 50×30 mm label at print resolution: QR on the start side, text beside it.
 * @position Shared by the detail card, the PNG download and the print view, so all three show the same label.
 */
import QRCode from 'qrcode';

import { fitFontSize, truncate, wrapText, type Measure } from './labelLayout';
import { LABEL_HEIGHT_MM, LABEL_WIDTH_MM, labelLines, type AssetLabelItem } from './scanLink';

const DOTS_PER_MM = 300 / 25.4;
const mm = (value: number) => Math.round(value * DOTS_PER_MM);
const WIDTH = mm(LABEL_WIDTH_MM);
const HEIGHT = mm(LABEL_HEIGHT_MM);
const PADDING = mm(1.5);
/** QR side including its quiet zone; the rest of the width is left for the text. */
const QR_SIDE = mm(22);
/** Quiet zone in modules inside QR_SIDE; the label padding adds more on the outer edges. */
const QUIET_MODULES = 2;
/** Font sizes in dots. The tag shrinks from TAG_MAX to TAG_MIN before it is shortened. */
const TAG_MAX = 56;
const TAG_MIN = 28;
const NAME_SIZE = 28;
const NAME_LINES = 2;
const SERIAL_SIZE = 24;
const LINE_GAP = 10;
const FONT_FAMILY = 'Figtree';
const FALLBACK_FAMILY = 'sans-serif';
const BOLD = 'bold';
/** Thermal labels print black on white; theme colors do not apply on paper. */
const INK = '#000000';
const PAPER = '#ffffff';

const font = (size: number, weight = '') => `${weight === '' ? '' : `${weight} `}${size}px ${FONT_FAMILY}, ${FALLBACK_FAMILY}`;

export async function drawLabel(item: AssetLabelItem, link: string): Promise<string> {
  const canvas = document.createElement('canvas');
  canvas.width = WIDTH;
  canvas.height = HEIGHT;
  const context = canvas.getContext('2d');
  if (context === null) {
    throw new Error('Canvas 2D is not available.');
  }
  // A font is loaded only when something asks for it; the bare print view may not have used Figtree yet.
  await Promise.all([document.fonts.load(font(TAG_MAX, BOLD)), document.fonts.load(font(NAME_SIZE))]);
  const measureWith =
    (weight: string): Measure =>
    (text, size) => {
      context.font = font(size, weight);
      return context.measureText(text).width;
    };
  const regular = measureWith('');
  const bold = measureWith(BOLD);

  context.fillStyle = PAPER;
  context.fillRect(0, 0, WIDTH, HEIGHT);

  const qr = QRCode.create(link, { errorCorrectionLevel: 'M' });
  const moduleSize = Math.floor(QR_SIDE / (qr.modules.size + 2 * QUIET_MODULES));
  const qrTop = Math.round((HEIGHT - moduleSize * qr.modules.size) / 2);
  const qrStart = PADDING + QUIET_MODULES * moduleSize;
  context.fillStyle = INK;
  for (let row = 0; row < qr.modules.size; row += 1) {
    for (let column = 0; column < qr.modules.size; column += 1) {
      if (qr.modules.get(row, column)) {
        context.fillRect(qrStart + column * moduleSize, qrTop + row * moduleSize, moduleSize, moduleSize);
      }
    }
  }

  const textStart = PADDING + QR_SIDE + PADDING;
  const textWidth = WIDTH - textStart - PADDING;
  const [tag = '', name = '', serial] = labelLines(item);
  const lines: { text: string; size: number; weight: string }[] = [];
  const tagSize = fitFontSize(bold, tag, textWidth, TAG_MAX, TAG_MIN);
  lines.push({ text: truncate(bold, tag, tagSize, textWidth), size: tagSize, weight: BOLD });
  for (const text of wrapText(regular, name, NAME_SIZE, textWidth, NAME_LINES)) {
    lines.push({ text, size: NAME_SIZE, weight: '' });
  }
  if (serial !== undefined) {
    lines.push({ text: truncate(regular, serial, SERIAL_SIZE, textWidth), size: SERIAL_SIZE, weight: '' });
  }
  // The text block is centred vertically next to the QR.
  const blockHeight = lines.reduce((sum, line) => sum + line.size, 0) + LINE_GAP * (lines.length - 1);
  let baseline = Math.round((HEIGHT - blockHeight) / 2);
  for (const line of lines) {
    baseline += line.size;
    context.font = font(line.size, line.weight);
    context.fillText(line.text, textStart, baseline);
    baseline += LINE_GAP;
  }
  return canvas.toDataURL('image/png');
}
