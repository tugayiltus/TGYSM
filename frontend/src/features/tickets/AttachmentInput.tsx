import { useId } from 'react';

/** Mirrors backend AttachmentTypes: allowed extensions for the file picker (the server checks the content). */
const ACCEPT = '.pdf,.png,.jpg,.jpeg,.txt,.csv';

export interface AttachmentInputProps {
  label: string;
  hint?: string;
  files: readonly File[];
  onChange: (files: File[]) => void;
}

export function AttachmentInput({ label, hint, files, onChange }: AttachmentInputProps) {
  const id = useId();
  const hintId = hint ? `${id}-hint` : undefined;
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm font-medium text-slate-800">
        {label}
      </label>
      <input
        id={id}
        type="file"
        multiple
        accept={ACCEPT}
        aria-describedby={hintId}
        className="min-h-11 text-base"
        onChange={(event) => onChange(Array.from(event.target.files ?? []))}
      />
      {hint && (
        <p id={hintId} className="text-xs text-slate-600">
          {hint}
        </p>
      )}
      {files.length > 0 && (
        <ul className="text-sm text-slate-700">
          {files.map((file) => (
            <li key={`${file.name}-${file.size}`}>{file.name}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
