import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError, translateErrorCode } from '../../shared/api/errorMessage';
import { ApiError } from '../../shared/api/errors';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextField } from '../../shared/ui/TextField';
import { UnsavedChangesGuard } from '../../shared/ui/UnsavedChangesGuard';

import { useCommitImport, usePreviewImport, type AssetImportJobItem } from './api';

const TEMPLATE_URL = '/api/v1/assets/import/template';
const FILE_FIELD = 'file';
const CSV_ACCEPT = '.csv,text/csv';
const TABLE_CLASS = 'table table-sm small mb-0 text-nowrap';
const HEAD_CLASS = 'fw-medium';

/** A file-level problem (wrong header, too big) arrives as a field error on "file". */
function describeImportError(error: unknown): string {
  const fileCode = error instanceof ApiError ? error.fieldErrors[FILE_FIELD]?.[0] : undefined;
  return fileCode === undefined ? describeError(error) : translateErrorCode(fileCode);
}

function ErrorTable({ job }: { job: AssetImportJobItem }) {
  const { t } = useTranslation('assets');
  const fieldLabel = (field: string) => t(`fields.${field}` as 'fields.assetTag', { defaultValue: field });
  return (
    <div className="table-responsive border rounded bg-body">
      <table className={TABLE_CLASS}>
        <caption className="p-2 text-start fw-semibold">{t('import.errorsCaption')}</caption>
        <thead>
          <tr>
            <th scope="col" className={HEAD_CLASS}>{t('import.columns.row')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('import.columns.field')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('import.columns.problem')}</th>
          </tr>
        </thead>
        <tbody>
          {job.errors.map((e) => (
            <tr key={`${e.row}-${e.field}-${e.code}`}>
              <td>{e.row}</td>
              <td>{fieldLabel(e.field)}</td>
              <td className="text-wrap">{translateErrorCode(e.code)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function RowTable({ job }: { job: AssetImportJobItem }) {
  const { t } = useTranslation('assets');
  return (
    <div className="table-responsive border rounded bg-body">
      <table className={TABLE_CLASS}>
        <caption className="p-2 text-start fw-semibold">{t('import.rowsCaption')}</caption>
        <thead>
          <tr>
            <th scope="col" className={HEAD_CLASS}>{t('list.columns.tag')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('list.columns.device')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('list.columns.location')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('list.columns.status')}</th>
          </tr>
        </thead>
        <tbody>
          {job.rows.map((r) => (
            <tr key={r.row}>
              <td>{r.assetTag}</td>
              <td>{`${t(`types.${r.type}`)} · ${r.brand} ${r.model}`}</td>
              <td>{r.locationPath}</td>
              <td>{t(`statuses.${r.status}`)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function AssetImportPage() {
  const { t } = useTranslation('assets');
  const [file, setFile] = useState<File | null>(null);
  const preview = usePreviewImport();
  const commit = useCommitImport();
  const job = commit.data ?? preview.data;

  return (
    <>
      <PageHeader titleOverride={t('import.title')} />
      {/* A preview not yet imported is work the user would lose by leaving. */}
      <UnsavedChangesGuard when={job?.status === 'previewed'} />
      <div className="d-flex flex-column gap-3">
      <p className="mb-0">{t('import.intro')}</p>
      <a href={TEMPLATE_URL} download className="align-self-start link-primary text-decoration-underline">
        {t('import.template')}
      </a>

      <form
        className="d-flex flex-column gap-2 flex-sm-row align-items-sm-end"
        onSubmit={(event) => {
          event.preventDefault();
          if (file !== null) {
            commit.reset();
            preview.mutate(file);
          }
        }}
      >
        <TextField type="file" accept={CSV_ACCEPT} label={t('import.file')} className="flex-grow-1" onChange={(event) => {
            // A new file invalidates the previous preview; its job must not be committable any more.
            preview.reset();
            commit.reset();
            setFile(event.target.files?.[0] ?? null);
          }}
        />
        <Button type="submit" variant="secondary" disabled={file === null} isLoading={preview.isPending}>
          {t('import.preview')}
        </Button>
      </form>

      <FormAlert message={preview.error ? describeImportError(preview.error) : null} />
      <FormAlert message={commit.error ? describeError(commit.error) : null} />

      {job && (
        <div className="d-flex flex-column gap-3">
          <p role="status" className="mb-0 fw-medium">
            {job.status === 'committed'
              ? t('import.done', { count: job.validRows })
              : t('import.summary', { total: job.totalRows, valid: job.validRows, errors: job.errorRows })}
          </p>
          {job.errorRows > 0 && <p className="mb-0">{t('import.fixAndRetry')}</p>}
          {job.errors.length > 0 && <ErrorTable job={job} />}
          {job.errorRows === 0 && job.rows.length > 0 && <RowTable job={job} />}
          {job.status === 'previewed' && (
            <Button className="align-self-start" disabled={job.errorRows > 0 || job.validRows === 0} isLoading={commit.isPending} onClick={() => commit.mutate(job.jobId)}>
              {t('import.commit')}
            </Button>
          )}
        </div>
      )}
      </div>
    </>
  );
}
