import { mkdir, mkdtemp, readdir, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

import { chromium } from '@playwright/test';
import GithubSlugger from 'github-slugger';
import { Marked, marked, type Token, type Tokens, type TokensList } from 'marked';

/**
 * Builds docs/user-guides/combined.md and combined.pdf, started by tools/build-user-guide.sh.
 *
 * 1. Page order: the numbered links of README.md. PROGRESS.md is a work log and is not part of the guide.
 * 2. combined.md: the pages concatenated; links between pages and links to http://localhost... become in-document anchors.
 * 3. Checks (exit 1 on failure): every `](#...)` link has a rendered `id`; every referenced image exists; every file in
 *    images/ is referenced; every bold `**…**` text is a UI label, i.e. a value in the Turkish locale files.
 * 4. combined.pdf: Chromium print of the rendered HTML, A4 through `@page { size: A4; margin: ${PAGE_MARGIN}; }`; other options stay default.
 */

const GUIDE_DIR = resolve(process.env.GUIDE_DIR ?? '../docs/user-guides');
const README_FILE = 'README.md';
const COMBINED_MD = 'combined.md';
const COMBINED_PDF = 'combined.pdf';
const IMAGES_DIR = 'images';
const PAGE_SEPARATOR = '\n\n';
const INDEX_LINK = /^\s*\d+\.\s+\[[^\]]*\]\(([^)#]+\.md)\)/gm;
const LINK = /(?<!!)\[([^\]]*)\]\(([^)\s]+)((?:\s+"[^"]*")?)\)/g;
const INLINE_CODE = /(`+)[\s\S]*?\1/g;
const MASK_OPEN = '\u0000';
const PAGE_MARGIN = '10mm';
const LOCALHOST_URL = /^https?:\/\/localhost(?::\d+)?(?:[/?#]|$)/;
const PAGE_LINK = /^([\w.-]+\.md)(#.*)?$/;
const EXTERNAL_URL = /^[a-z][a-z0-9+.-]*:/i;
const LOCALE_DIR = resolve(process.env.GUIDE_LOCALE_DIR ?? 'src/shared/i18n/locales/tr');
const LOCALE_EXTENSION = '.json';
const INTERPOLATION = /\{\{\s*\w+\s*\}\}/;
const REGEX_SPECIAL = /[.*+?^${}()|[\]\\]/g;
const MOBILE_IMAGE_WIDTH = '62mm';
const PRINT_CSS = `
@page { size: A4; margin: ${PAGE_MARGIN}; }
body { font-family: -apple-system, "Helvetica Neue", Arial, sans-serif; font-size: 11pt; line-height: 1.5; color: #1b1b1b; }
img { max-width: 100%; }
img[src$="-mobile.png"] { width: ${MOBILE_IMAGE_WIDTH}; border: 1px solid #ccc; }
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

/** Plain text of inline tokens (what a heading id is slugged from). */
function inlineText(tokens: Token[] | undefined, fallback: string): string {
  if (!tokens) return fallback;
  return tokens.map((token) => ('tokens' in token && token.tokens ? inlineText(token.tokens, token.raw) : 'text' in token ? String(token.text) : token.raw)).join('');
}

/** Headings of a document in order, from the same lexer tokens the renderer uses. */
function headings(tokens: TokensList): { level: number; text: string }[] {
  const found: { level: number; text: string }[] = [];
  new Marked().walkTokens(tokens, (token) => {
    if (token.type === 'heading') {
      const heading = token as Tokens.Heading;
      found.push({ level: heading.depth, text: inlineText(heading.tokens, heading.text) });
    }
  });
  return found;
}

/** Applies `edit` to the text of a block outside code: fenced/indented code blocks are skipped, inline code is masked. */
function editOutsideCode(markdown: string, edit: (text: string) => string): string {
  const spans: string[] = [];
  const masked = markdown.replace(INLINE_CODE, (span) => `${MASK_OPEN}${spans.push(span) - 1}${MASK_OPEN}`);
  return edit(masked).replace(new RegExp(`${MASK_OPEN}(\\d+)${MASK_OPEN}`, 'g'), (_whole, index: string) => spans[Number(index)] ?? '');
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

  // Pre-pass over the lexer tokens with the renderer's slugger: the id of every heading, in document order.
  const slugger = new GithubSlugger();
  const pageAnchor = new Map<string, string>();
  const textAnchor = new Map<string, string>();
  for (const page of pages) {
    for (const heading of headings(marked.lexer(sources.get(page) ?? ''))) {
      const id = slugger.slug(heading.text);
      if (heading.level === 1 && !pageAnchor.has(page)) pageAnchor.set(page, id);
      if (!textAnchor.has(heading.text.toLowerCase())) textAnchor.set(heading.text.toLowerCase(), id);
    }
  }

  const rewriteText = (page: string, text: string): string =>
    text.replace(LINK, (whole, label: string, target: string, title: string) => {
      const pageLink = PAGE_LINK.exec(target);
      if (pageLink?.[1]) {
        const firstHeading = pageAnchor.get(pageLink[1]);
        if (!pageLink[2] && !firstHeading) fail(`${page}: link to ${target} has no heading in a listed page.`);
        return `[${label}](${pageLink[2] ?? `#${firstHeading}`}${title})`;
      }
      if (LOCALHOST_URL.test(target)) {
        // The PDF has no web server: link to the heading named like the link text, else keep the text only.
        const anchor = textAnchor.get(label.toLowerCase());
        if (anchor) return `[${label}](#${anchor}${title})`;
        console.warn(`build-user-guide: ${page}: link ${target} has no heading named "${label}"; kept as plain text.`);
        return label;
      }
      return whole;
    });
  const rewrite = (page: string, markdown: string): string =>
    marked
      .lexer(markdown)
      .map((block) => (block.type === 'code' ? block.raw : editOutsideCode(block.raw, (text) => rewriteText(page, text))))
      .join('');

  const combined = `${pages.map((page) => rewrite(page, sources.get(page) ?? '')).join(PAGE_SEPARATOR)}\n`;
  const html = await render(combined);
  checkAnchors(combined, html);
  await checkImages(combined);
  checkLabels(combined, await localeLabels());
  await writeFile(join(GUIDE_DIR, COMBINED_MD), combined);
  await writePdf(html);
  console.log(`Built ${COMBINED_MD} and ${COMBINED_PDF} from ${pages.length} pages: ${pages.join(', ')}`);
}

async function render(markdown: string): Promise<string> {
  const slugger = new GithubSlugger();
  const renderer = new Marked({
    renderer: {
      heading(this, token: Tokens.Heading) {
        const id = slugger.slug(inlineText(token.tokens, token.text));
        return `<h${token.depth} id="${id}">${this.parser.parseInline(token.tokens)}</h${token.depth}>\n`;
      },
    },
  });
  const body = await renderer.parse(markdown);
  const base = pathToFileURL(`${GUIDE_DIR}/`).href;
  return `<!doctype html><html lang="tr"><head><meta charset="utf-8"><base href="${base}"><title>TGYSM Kullanıcı Rehberi</title><style>${PRINT_CSS}</style></head><body>${body}</body></html>`;
}

/** Every in-document link target (from the lexer, code excluded) is an `id` of the rendered HTML. */
function checkAnchors(markdown: string, html: string): void {
  const ids = new Set([...html.matchAll(/\sid="([^"]+)"/g)].map((match) => match[1]));
  const missing: string[] = [];
  marked.walkTokens(marked.lexer(markdown), (token) => {
    if (token.type === 'link' && token.href.startsWith('#') && !ids.has(token.href.slice(1))) missing.push(token.href);
  });
  if (missing.length > 0) fail(`links without a matching id: ${missing.join(', ')}`);
}

async function checkImages(markdown: string): Promise<void> {
  const referenced = new Set<string>();
  const targets: string[] = [];
  marked.walkTokens(marked.lexer(markdown), (token) => {
    if (token.type === 'image') targets.push(token.href);
  });
  for (const target of targets) {
    if (EXTERNAL_URL.test(target)) fail(`image ${target} is not a local file.`);
    referenced.add(target);
    const found = await stat(join(GUIDE_DIR, target)).then((info) => info.isFile(), () => false);
    if (!found) fail(`referenced image does not exist: ${target}`);
  }
  const files = await readdir(join(GUIDE_DIR, IMAGES_DIR)).catch(() => [] as string[]);
  const unused = files.filter((file) => !file.startsWith('.') && !referenced.has(`${IMAGES_DIR}/${file}`));
  if (unused.length > 0) fail(`images not referenced by any page: ${unused.join(', ')}`);
}

/** Every string value of the locale files; a value with `{{name}}` placeholders also matches any filled-in text. */
async function localeLabels(): Promise<{ exact: Set<string>; patterns: RegExp[] }> {
  const exact = new Set<string>();
  const patterns: RegExp[] = [];
  const collect = (value: unknown): void => {
    if (typeof value === 'string') {
      exact.add(value);
      // A value that is only placeholders (e.g. "{{name}}") has no label text and would match anything.
      if (INTERPOLATION.test(value) && value.split(INTERPOLATION).join('').trim() !== '') {
        const parts = value.split(INTERPOLATION).map((part) => part.replace(REGEX_SPECIAL, '\\$&'));
        patterns.push(new RegExp(`^${parts.join('.+')}$`));
      }
    } else if (value !== null && typeof value === 'object') {
      Object.values(value).forEach(collect);
    }
  };
  const files = (await readdir(LOCALE_DIR).catch(() => fail(`locale folder not found: ${LOCALE_DIR}`))).filter((file) => file.endsWith(LOCALE_EXTENSION));
  if (files.length === 0) fail(`no locale files in ${LOCALE_DIR}`);
  for (const file of files) collect(JSON.parse(await readFile(join(LOCALE_DIR, file), 'utf8')));
  return { exact, patterns };
}

/** Bold text marks a UI label in the guide: it must be a text the product shows (lexer tokens, code excluded). */
function checkLabels(markdown: string, labels: { exact: Set<string>; patterns: RegExp[] }): void {
  const unknown = new Set<string>();
  marked.walkTokens(marked.lexer(markdown), (token) => {
    if (token.type !== 'strong') return;
    const label = inlineText((token as Tokens.Strong).tokens, (token as Tokens.Strong).text);
    if (!labels.exact.has(label) && !labels.patterns.some((pattern) => pattern.test(label))) unknown.add(label);
  });
  if (unknown.size > 0) fail(`bold labels not found in ${LOCALE_DIR}: ${[...unknown].map((label) => `"${label}"`).join(', ')}`);
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
