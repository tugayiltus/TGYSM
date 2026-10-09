import { TextField } from '../../shared/ui/TextField';

/** Mirrors backend AttachmentTypes: allowed extensions for the file picker (the server checks the content). */
const ACCEPT = '.pdf,.png,.jpg,.jpeg,.txt,.csv';

export interface AttachmentInputProps {
  label: string;
  hint?: string;
  files: readonly File[];
  onChange: (files: File[]) => void;
}

/** Multi-file picker in the shared field look; the chosen names are listed under it. */
export function AttachmentInput({ label, hint, files, onChange }: AttachmentInputProps) {
  return (
    <div className="d-flex flex-column gap-1">
      <TextField type="file" multiple accept={ACCEPT} label={label} hint={hint} onChange={(event) => onChange(Array.from(event.target.files ?? []))} />
      {files.length > 0 && (
        <ul className="small mb-0">
          {files.map((file) => (
            <li key={`${file.name}-${file.size}`}>{file.name}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
