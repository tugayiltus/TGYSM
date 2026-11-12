import { describe, expect, it } from 'vitest';

import { fitFontSize, wrapText, type Measure } from './labelLayout';

/** A monospace stand-in: every character is 0.6 em wide. */
const CHAR_EM = 0.6;
const measure: Measure = (text, size) => text.length * size * CHAR_EM;

describe('label text layout', () => {
  it('shrinks the tag until it fits the column instead of cutting it', () => {
    const size = fitFontSize(measure, 'DMR-000123', 260, 52, 24);
    expect(measure('DMR-000123', size)).toBeLessThanOrEqual(260);
    expect(size).toBeGreaterThan(24);
  });

  it('keeps the largest size when the text already fits', () => {
    expect(fitFontSize(measure, 'A1', 260, 52, 24)).toBe(52);
  });

  it('wraps brand and model onto two lines before it shortens anything', () => {
    const lines = wrapText(measure, 'Lenovo ThinkCentre M70q Gen 4', 28, 340, 2);
    expect(lines).toHaveLength(2);
    expect(lines.every((line) => measure(line, 28) <= 340)).toBe(true);
    expect(lines.join(' ')).toBe('Lenovo ThinkCentre M70q Gen 4');
  });

  it('ends the last line with an ellipsis when the text needs more lines than allowed', () => {
    const lines = wrapText(measure, 'Hewlett Packard Enterprise ProLiant DL380 Gen11 Server', 28, 260, 2);
    expect(lines).toHaveLength(2);
    expect(lines[1]?.endsWith('…')).toBe(true);
    expect(lines.every((line) => measure(line, 28) <= 260)).toBe(true);
  });
});
