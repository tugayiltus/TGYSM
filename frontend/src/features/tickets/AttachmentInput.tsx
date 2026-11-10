/**
 * @input Label, optional hint, the chosen files and a change callback.
 * @output A multi-file picker that lists the chosen files.
 * @position New ticket form and the comment form.
 */
import { FileInput } from '@astryxdesign/core/FileInput';

/** Mirrors backend AttachmentTypes: allowed extensions for the file picker (the server checks the content). */
const ACCEPT = '.pdf,.png,.jpg,.jpeg,.txt,.csv';

export interface AttachmentInputProps {
  label: string;
  hint?: string;
  files: readonly File[];
  onChange: (files: File[]) => void;
}

const toList = (value: File | File[] | null): File[] => (value === null ? [] : Array.isArray(value) ? value : [value]);

export function AttachmentInput({ label, hint, files, onChange }: AttachmentInputProps) {
  return <FileInput label={label} description={hint} accept={ACCEPT} isMultiple value={[...files]} onChange={(value) => onChange(toList(value))} />;
}
