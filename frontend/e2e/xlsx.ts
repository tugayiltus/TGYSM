import { readFile } from 'node:fs/promises';
import { inflateRawSync } from 'node:zlib';

/**
 * A tiny `.xlsx` reader for the E2E checks: an `.xlsx` file is a ZIP archive of XML parts, so this reads the ZIP
 * central directory, inflates the parts it needs and counts rows. It understands only what ClosedXML writes.
 */

const END_OF_CENTRAL_DIRECTORY_SIGNATURE = 0x06054b50;
const CENTRAL_DIRECTORY_SIGNATURE = 0x02014b50;
const END_OF_CENTRAL_DIRECTORY_MIN_SIZE = 22;
const MAX_COMMENT_SIZE = 0xffff;
const EOCD_ENTRY_COUNT_OFFSET = 10;
const EOCD_DIRECTORY_OFFSET_OFFSET = 16;
const CENTRAL_METHOD_OFFSET = 10;
const CENTRAL_COMPRESSED_SIZE_OFFSET = 20;
const CENTRAL_NAME_LENGTH_OFFSET = 28;
const CENTRAL_EXTRA_LENGTH_OFFSET = 30;
const CENTRAL_COMMENT_LENGTH_OFFSET = 32;
const CENTRAL_LOCAL_HEADER_OFFSET = 42;
const CENTRAL_HEADER_SIZE = 46;
const LOCAL_NAME_LENGTH_OFFSET = 26;
const LOCAL_EXTRA_LENGTH_OFFSET = 28;
const LOCAL_HEADER_SIZE = 30;
const METHOD_STORED = 0;
const METHOD_DEFLATED = 8;

const WORKBOOK_PART = 'xl/workbook.xml';
const WORKBOOK_RELS_PART = 'xl/_rels/workbook.xml.rels';
const PART_ROOT = 'xl/';
// Whole start tags; attributes are read separately because writers order them differently.
const SHEET_TAG = /<(?:\w+:)?sheet\b[^>]*>/g;
const RELATIONSHIP_TAG = /<(?:\w+:)?Relationship\b[^>]*>/g;
const ROW = /<(?:\w+:)?row[\s>]/g;

function unzip(archive: Buffer): Map<string, Buffer> {
  const searchStart = Math.max(0, archive.length - END_OF_CENTRAL_DIRECTORY_MIN_SIZE - MAX_COMMENT_SIZE);
  let end = archive.length - END_OF_CENTRAL_DIRECTORY_MIN_SIZE;
  while (end >= searchStart && archive.readUInt32LE(end) !== END_OF_CENTRAL_DIRECTORY_SIGNATURE) {
    end--;
  }
  if (end < searchStart) {
    throw new Error('Not a ZIP archive: no end-of-central-directory record.');
  }
  const entries = archive.readUInt16LE(end + EOCD_ENTRY_COUNT_OFFSET);
  let offset = archive.readUInt32LE(end + EOCD_DIRECTORY_OFFSET_OFFSET);
  const parts = new Map<string, Buffer>();
  for (let entry = 0; entry < entries; entry++) {
    if (archive.readUInt32LE(offset) !== CENTRAL_DIRECTORY_SIGNATURE) {
      throw new Error('Corrupt ZIP central directory.');
    }
    const method = archive.readUInt16LE(offset + CENTRAL_METHOD_OFFSET);
    const compressedSize = archive.readUInt32LE(offset + CENTRAL_COMPRESSED_SIZE_OFFSET);
    const nameLength = archive.readUInt16LE(offset + CENTRAL_NAME_LENGTH_OFFSET);
    const extraLength = archive.readUInt16LE(offset + CENTRAL_EXTRA_LENGTH_OFFSET);
    const commentLength = archive.readUInt16LE(offset + CENTRAL_COMMENT_LENGTH_OFFSET);
    const localOffset = archive.readUInt32LE(offset + CENTRAL_LOCAL_HEADER_OFFSET);
    const name = archive.toString('utf8', offset + CENTRAL_HEADER_SIZE, offset + CENTRAL_HEADER_SIZE + nameLength);
    const dataStart =
      localOffset + LOCAL_HEADER_SIZE + archive.readUInt16LE(localOffset + LOCAL_NAME_LENGTH_OFFSET) + archive.readUInt16LE(localOffset + LOCAL_EXTRA_LENGTH_OFFSET);
    const data = archive.subarray(dataStart, dataStart + compressedSize);
    if (method === METHOD_DEFLATED) {
      parts.set(name, inflateRawSync(data));
    } else if (method === METHOD_STORED) {
      parts.set(name, Buffer.from(data));
    }
    offset += CENTRAL_HEADER_SIZE + nameLength + extraLength + commentLength;
  }
  return parts;
}

function attribute(tag: string, name: string): string {
  const value = new RegExp(`\\s${name}="([^"]*)"`).exec(tag)?.[1];
  if (value === undefined) {
    throw new Error(`No ${name} attribute in ${tag}`);
  }
  return value;
}

export interface Workbook {
  /** Sheet names in workbook order. */
  sheetNames: string[];
  /** Number of `<row>` elements of a sheet, header row included. */
  rowCount: (sheetName: string) => number;
}

export async function readWorkbook(path: string): Promise<Workbook> {
  const parts = unzip(await readFile(path));
  const text = (name: string) => {
    const part = parts.get(name);
    if (part === undefined) {
      throw new Error(`The workbook has no part ${name}.`);
    }
    return part.toString('utf8');
  };
  const targets = new Map((text(WORKBOOK_RELS_PART).match(RELATIONSHIP_TAG) ?? []).map((tag) => [attribute(tag, 'Id'), attribute(tag, 'Target')]));
  const sheets = (text(WORKBOOK_PART).match(SHEET_TAG) ?? []).map((tag) => ({ name: attribute(tag, 'name'), id: attribute(tag, 'r:id') }));
  return {
    sheetNames: sheets.map((sheet) => sheet.name),
    rowCount: (sheetName) => {
      const sheet = sheets.find((candidate) => candidate.name === sheetName);
      const target = sheet === undefined ? undefined : targets.get(sheet.id);
      if (target === undefined) {
        throw new Error(`The workbook has no sheet ${sheetName}.`);
      }
      // Targets are relative to xl/ ("worksheets/sheet1.xml") or absolute ("/xl/worksheets/sheet1.xml").
      const partName = target.startsWith('/') ? target.slice(1) : `${PART_ROOT}${target}`;
      return text(partName).match(ROW)?.length ?? 0;
    },
  };
}
