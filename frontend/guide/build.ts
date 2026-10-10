import { mkdir, mkdtemp, readdir, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

import { chromium } from '@playwright/test';
import GithubSlugger from 'github-slugger';
import { Marked, type Tokens } from 'marked';

/**
 * Builds docs/user-guides/combined.md and combined.pdf, started by tools/build-user-guide.sh.
 *
 * 1. Page order: the numbered links of README.md. PROGRESS.md is a work log and is not part of the guide.
 * 2. combined.md: the pages concatenated; links between pages and links to http://localhost... become in-document anchors.
 * 3. Checks (exit 1 on failure): every `](#...)` link has a rendered `id`; every referenced image exists; every file in
 *    images/ is referenced.
 * 4. combined.pdf: Chromium print of the rendered HTML, A4 through `@page { size: A4; }`; other options stay default.
 */

const GUIDE_DIR = resolve(process.env.GUIDE_DIR ?? '../docs/user-guides');
const README_FILE = 'README.md';
const COMBINED_MD = 'combined.md';
const COMBINED_PDF = 'combined.pdf';
const IMAGES_DIR = 'images';
const PAGE_SEPARATOR = '\n\n';
const INDEX_LINK = /^\s*\d+\.\s+\[[^\]]*\]\(([^)#]+\.md)\)/gm;
const LINK = /(?<!!)\[([^\]]*)\]\(([^)\s]+)\)/g;
const IMAGE = /!\[[^\]]*\]\(([^)\s]+)\)/g;
const LOCALHOST_URL = /^https?:\/\/localhost(?::\d+)?(?:[/?#]|$)/;
const PAGE_LINK = /^([\w.-]+\.md)(#.*)?$/;
const EXTERNAL_URL = /^[a-z][a-z0-9+.-]*:/i;
const HEADING_LINE = /^(#{1,6})\s+(.+?)\s*#*\s*$/;
const FENCE = /^(```|~~~)/;
const PRINT_CSS = `
@page { size: A4; }
body { font-family: -apple-system, "Helvetica Neue", Arial, sans-serif; font-size: 11pt; line-height: 1.5; color: #1b1b1b; }
img { max-width: 100%; }
pre, code { font-family: Menlo, monospace; font-size: 9.5pt; }
pre { background: #f4f4f4; padding: 8px; white-space: pre-wrap; }
table { border-collapse: collapse; }
th, td { border: 1px solid #bbb; padding: 4px 8px; }
h1 { break-before: page; }
h1:first-of-type { break-before: auto; }
img, pre, table { break-inside: avoid; }
`;

function fail(message: string): never {
  console.error(`build-user-guide: ${message}`);
  process.exit(1);
}

/** Heading text as the renderer slugs it: the link/emphasis markup is dropped, like marked's `text` of the token. */
function plainHeading(raw: string): string {
  return raw.replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1').replace(/[*_`]/g, '');
}

/** Heading lines of a page outside code fences, in order. */
function headings(markdown: string): { level: number; text: string }[] {
  const found: { level: number; text: string }[] = [];
  let inFence = false;
  for (const line of markdown.split('\n')) {
    if (FENCE.test(line)) inFence = !inFence;
    const match = inFence ? null : HEADING_LINE.exec(line);
    if (match?.[1] && match[2]) found.push({ level: match[1].length, text: plainHeading(match[2]) });
  }
  return found;
}

async function pageOrder(): Promise<string[]> {
  const readme = await readFile(join(GUIDE_DIR, README_FILE), 'utf8');
  const pages = [...readme.matchAll(INDEX_LINK)].map((match) => match[1] ?? '');
  if (pages.length === 0) fail(`${README_FILE} lists no pages.`);
  return pages;
}

async function main(): Promise<void> {
  const pages = await pageOrder();
  const sources = new Map<string, string>();
  for (const page of pages) {
    sources.set(page, (await readFile(join(GUIDE_DIR, page), 'utf8')).trim());
  }

  // Pre-pass with the renderer's slugger over the whole document: the id of every heading, in order.
  const slugger = new GithubSlugger();
  const pageAnchor = new Map<string, string>();
  const textAnchor = new Map<string, string>();
  for (const page of pages) {
    for (const heading of headings(sources.get(page) ?? '')) {
      const id = slugger.slug(heading.text);
      if (heading.level === 1 && !pageAnchor.has(page)) pageAnchor.set(page, id);
      if (!textAnchor.has(heading.text.toLowerCase())) textAnchor.set(heading.text.toLowerCase(), id);
    }
  }

  const rewrite = (page: string, markdown: string): string =>
    markdown.replace(LINK, (whole, text: string, target: string) => {
      const pageLink = PAGE_LINK.exec(target);
      if (pageLink?.[1]) {
        const anchor = pageLink[2] ? pageLink[2] : pageAnchor.has(pageLink[1]) ? `#${pageAnchor.get(pageLink[1])}` : '';
        if (!pageAnchor.has(pageLink[1]) && !pageLink[2]) fail(`${page}: link to ${target} has no heading in a listed page.`);
        return `[${text}](${anchor})`;
      }
      if (LOCALHOST_URL.test(target)) {
        // The PDF has no web server: link to the heading named like the link text, else keep the text only.
        const anchor = textAnchor.get(text.toLowerCase());
        return anchor ? `[${text}](#${anchor})` : text;
      }
      return whole;
    });

  const combined = `${pages.map((page) => rewrite(page, sources.get(page) ?? '')).join(PAGE_SEPARATOR)}\n`;
  await writeFile(join(GUIDE_DIR, COMBINED_MD), combined);

  const html = await render(combined);
  checkAnchors(combined, html);
  await checkImages(combined);
  await writePdf(html);
  console.log(`Built ${COMBINED_MD} and ${COMBINED_PDF} from ${pages.length} pages: ${pages.join(', ')}`);
}

async function render(markdown: string): Promise<string> {
  const slugger = new GithubSlugger();
  const marked = new Marked({
    renderer: {
      heading(this, token: Tokens.Heading) {
        const id = slugger.slug(plainHeading(token.text));
        return `<h${token.depth} id="${id}">${this.parser.parseInline(token.tokens)}</h${token.depth}>\n`;
      },
    },
  });
  const body = await marked.parse(markdown);
  const base = pathToFileURL(`${GUIDE_DIR}/`).href;
  return `<!doctype html><html lang="tr"><head><meta charset="utf-8"><base href="${base}"><title>TGYSM Kullanıcı Rehberi</title><style>${PRINT_CSS}</style></head><body>${body}</body></html>`;
}

function checkAnchors(markdown: string, html: string): void {
  const ids = new Set([...html.matchAll(/\sid="([^"]+)"/g)].map((match) => match[1]));
  const missing = [...markdown.matchAll(/\]\(#([^)]*)\)/g)].map((match) => match[1] ?? '').filter((anchor) => !ids.has(anchor));
  if (missing.length > 0) fail(`links without a matching id: ${missing.map((anchor) => `#${anchor}`).join(', ')}`);
}

async function checkImages(markdown: string): Promise<void> {
  const referenced = new Set<string>();
  for (const match of markdown.matchAll(IMAGE)) {
    const target = match[1] ?? '';
    if (EXTERNAL_URL.test(target)) fail(`image ${target} is not a local file.`);
    referenced.add(target);
    const found = await stat(join(GUIDE_DIR, target)).then((info) => info.isFile(), () => false);
    if (!found) fail(`referenced image does not exist: ${target}`);
  }
  const files = await readdir(join(GUIDE_DIR, IMAGES_DIR)).catch(() => [] as string[]);
  const unused = files.filter((file) => !file.startsWith('.') && !referenced.has(`${IMAGES_DIR}/${file}`));
  if (unused.length > 0) fail(`images not referenced by any page: ${unused.join(', ')}`);
}

async function writePdf(html: string): Promise<void> {
  const work = await mkdtemp(join(tmpdir(), 'tgysm-guide-'));
  const browser = await chromium.launch();
  try {
    const htmlFile = join(work, 'combined.html');
    await writeFile(htmlFile, html);
    const page = await browser.newPage();
    await page.goto(pathToFileURL(htmlFile).href);
    await page.evaluate(() => Promise.all(Array.from(document.images).map((image) => image.decode().catch(() => undefined))));
    await mkdir(GUIDE_DIR, { recursive: true });
    await page.pdf({ path: join(GUIDE_DIR, COMBINED_PDF), preferCSSPageSize: true });
  } finally {
    await browser.close();
    await rm(work, { recursive: true, force: true });
  }
}

await main();
