// Joins the screenshots of tools/ui-qa.sh into contact sheets, a few screens side by side, for a quick visual review.
// Usage: node tools/ui-qa-sheets.mjs <screenshot folder> <output folder> <screens per sheet> <column width px> <max height px>
import { mkdirSync, readdirSync, readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import path from 'node:path';

const require = createRequire(new URL('../frontend/package.json', import.meta.url));
const { chromium } = require('@playwright/test');

const [dir, out, perSheet, columnWidth, maxHeight] = process.argv.slice(2);
const per = Number(perSheet);
const width = Number(columnWidth);
const SHEET_PADDING_PX = 20;
const VIEWPORT_HEIGHT_PX = 800;

mkdirSync(out, { recursive: true });
const files = readdirSync(dir).filter((name) => name.endsWith('.png')).sort();
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: width * per + SHEET_PADDING_PX, height: VIEWPORT_HEIGHT_PX } });
for (let start = 0; start < files.length; start += per) {
  const cells = files
    .slice(start, start + per)
    .map((name) => {
      const data = readFileSync(path.join(dir, name)).toString('base64');
      return `<figure style="width:${width}px;max-height:${maxHeight}px;overflow:hidden;display:inline-block;vertical-align:top;margin:2px;border:1px solid #888"><figcaption style="font:11px sans-serif;background:#ff0;color:#000">${name}</figcaption><img alt="" style="width:${width}px" src="data:image/png;base64,${data}"></figure>`;
    })
    .join('');
  await page.setContent(`<body style="margin:0;background:#ccc;white-space:nowrap">${cells}</body>`);
  await page.screenshot({ path: path.join(out, `sheet-${String(start / per).padStart(2, '0')}.png`), fullPage: true });
}
await browser.close();
console.log(`${files.length} screenshots -> ${out}`);
