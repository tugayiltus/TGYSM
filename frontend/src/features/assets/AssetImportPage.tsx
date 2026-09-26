import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { describeError, translateErrorCode } from '../../shared/api/errorMessage';
import { ApiError } from '../../shared/api/errors';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextField } from '../../shared/ui/TextField';

import { useCommitImport, usePreviewImport, type AssetImportJobItem } from './api';

const TEMPLATE_URL = '/api/v1/assets/import/template';
const FILE_FIELD = 'file';
const CSV_ACCEPT = '.csv,text/csv';
const TABLE_CLASS = 'w-full min-w-max border-collapse text-left text-sm';
const CELL_CLASS = 'border-b border-slate-100 px-3 py-2';
const HEAD_CLASS = 'border-b border-slate-200 px-3 py-2 font-medium text-slate-700';

/** A file-level problem (wrong header, too big) arrives as a field error on "file". */
function describeImportError(error: unknown): string {
  const fileCode = error instanceof ApiError ? error.fieldErrors[FILE_FIELD]?.[0] : undefined;
  return fileCode === undefined ? describeError(error) : translateErrorCode(fileCode);
}

function ErrorTable({ job }: { job: AssetImportJobItem }) {
  const { t } = useTranslation('assets');
  const fieldLabel = (field: string) => t(`fields.${field}` as 'fields.assetTag', { defaultValue: field });
  return (
    <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
      <table className={TABLE_CLASS}>
        <caption className="p-3 text-left font-semibold">{t('import.errorsCaption')}</caption>
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
              <td className={CELL_CLASS}>{e.row}</td>
              <td className={CELL_CLASS}>{fieldLabel(e.field)}</td>
              <td className={`${CELL_CLASS} whitespace-normal`}>{translateErrorCode(e.code)}</td>
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
    <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
      <table className={TABLE_CLASS}>
        <caption className="p-3 text-left font-semibold">{t('import.rowsCaption')}</caption>
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
              <td className={CELL_CLASS}>{r.assetTag}</td>
              <td className={CELL_CLASS}>{`${t(`types.${r.type}`)} · ${r.brand} ${r.model}`}</td>
              <td className={CELL_CLASS}>{r.locationPath}</td>
              <td className={CELL_CLASS}>{t(`statuses.${r.status}`)}</td>
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
    <section className="flex flex-col gap-4">
      <Link to={ROUTES.assets} className="text-sm text-brand-900 underline">
        {t('detail.back')}
      </Link>
      <h1 className="text-2xl font-semibold">{t('import.title')}</h1>
      <p className="text-slate-700">{t('import.intro')}</p>
      <a href={TEMPLATE_URL} download className="self-start text-brand-900 underline">
        {t('import.template')}
      </a>

      <form
        className="flex flex-col gap-3 sm:flex-row sm:items-end"
        onSubmit={(event) => {
          event.preventDefault();
          if (file !== null) {
            commit.reset();
            preview.mutate(file);
          }
        }}
      >
        <TextField type="file" accept={CSV_ACCEPT} label={t('import.file')} className="sm:flex-1" onChange={(event) => setFile(event.target.files?.[0] ?? null)} />
        <Button type="submit" variant="secondary" disabled={file === null} isLoading={preview.isPending}>
          {t('import.preview')}
        </Button>
      </form>

      <FormAlert message={preview.error ? describeImportError(preview.error) : null} />
      <FormAlert message={commit.error ? describeError(commit.error) : null} />

      {job && (
        <div className="flex flex-col gap-4">
          <p role="status" className="font-medium">
            {job.status === 'committed'
              ? t('import.done', { count: job.validRows })
              : t('import.summary', { total: job.totalRows, valid: job.validRows, errors: job.errorRows })}
          </p>
          {job.errorRows > 0 && <p className="text-slate-700">{t('import.fixAndRetry')}</p>}
          {job.errors.length > 0 && <ErrorTable job={job} />}
          {job.errorRows === 0 && job.rows.length > 0 && <RowTable job={job} />}
          {job.status === 'previewed' && (
            <Button className="self-start" disabled={job.errorRows > 0 || job.validRows === 0} isLoading={commit.isPending} onClick={() => commit.mutate(job.jobId)}>
              {t('import.commit')}
            </Button>
          )}
        </div>
      )}
    </section>
  );
}
