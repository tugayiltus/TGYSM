/**
 * @input A text width measure, the text and the space it must fit.
 * @output A font size that fits, or lines wrapped to the width, ending with an ellipsis only when lines run out.
 * @position Label drawing (`drawLabel`); kept free of the canvas so it can be tested.
 */

/** Width of `text` at font size `size`, in the same unit as the available width. */
export type Measure = (text: string, size: number) => number;

const ELLIPSIS = '…';
const WORD_SEPARATOR = ' ';

/** The largest size from `max` down to `min` at which the text fits; `min` when none does. */
export function fitFontSize(measure: Measure, text: string, width: number, max: number, min: number): number {
  for (let size = max; size > min; size -= 1) {
    if (measure(text, size) <= width) {
      return size;
    }
  }
  return min;
}

/** Shortens text to the width, adding an ellipsis. */
export function truncate(measure: Measure, text: string, size: number, width: number): string {
  if (measure(text, size) <= width) {
    return text;
  }
  let end = text.length;
  while (end > 0 && measure(`${text.slice(0, end)}${ELLIPSIS}`, size) > width) {
    end -= 1;
  }
  return `${text.slice(0, end)}${ELLIPSIS}`;
}

/** Wraps text by words into at most `maxLines` lines; the last line takes the rest, shortened if needed. */
export function wrapText(measure: Measure, text: string, size: number, width: number, maxLines: number): string[] {
  const lines: string[] = [];
  let current = '';
  const words = text.split(WORD_SEPARATOR).filter((word) => word !== '');
  for (const [index, word] of words.entries()) {
    const candidate = current === '' ? word : `${current}${WORD_SEPARATOR}${word}`;
    if (measure(candidate, size) <= width || current === '') {
      current = candidate;
      continue;
    }
    if (lines.length === maxLines - 1) {
      return [...lines, truncate(measure, words.slice(index - current.split(WORD_SEPARATOR).length).join(WORD_SEPARATOR), size, width)];
    }
    lines.push(current);
    current = word;
  }
  return current === '' ? lines : [...lines, truncate(measure, current, size, width)];
}
