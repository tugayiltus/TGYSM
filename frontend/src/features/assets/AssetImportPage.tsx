import CIcon from '@coreui/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError, translateErrorCode } from '../../shared/api/errorMessage';
import { ApiError } from '../../shared/api/errors';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { FormSection } from '../../shared/ui/FormSection';
import { cilCloudDownload, cilCloudUpload, cilSearch } from '../../shared/ui/icons';
import { PageCard } from '../../shared/ui/PageCard';
import { StackTable } from '../../shared/ui/StackTable';
import { TextField } from '../../shared/ui/TextField';
import { UnsavedChangesGuard } from '../../shared/ui/UnsavedChangesGuard';

import { useCommitImport, usePreviewImport, type AssetImportJobItem } from './api';

const TEMPLATE_URL = '/api/v1/assets/import/template';
const FILE_FIELD = 'file';
const CSV_ACCEPT = '.csv,text/csv';

/** A file-level problem (wrong header, too big) arrives as a field error on "file". */
function describeImportError(error: unknown): string {
  const fileCode = error instanceof ApiError ? error.fieldErrors[FILE_FIELD]?.[0] : undefined;
  return fileCode === undefined ? describeError(error) : translateErrorCode(fileCode);
}

const HEAD_CLASS = 'text-body-secondary small text-uppercase fw-semibold';

function ErrorTable({ job }: { job: AssetImportJobItem }) {
  const { t } = useTranslation('assets');
  const fieldLabel = (field: string) => t(`fields.${field}` as 'fields.assetTag', { defaultValue: field });
  return (
    <PageCard title={t('import.errorsCaption')} flush>
      <StackTable
        caption={t('import.errorsCaption')}
        head={
          <tr>
            <th scope="col" className={HEAD_CLASS}>{t('import.columns.row')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('import.columns.field')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('import.columns.problem')}</th>
          </tr>
        }
      >
        {job.errors.map((e) => (
          <tr key={`${e.row}-${e.field}-${e.code}`}>
            <td data-label={t('import.columns.row')}>{e.row}</td>
            <td data-label={t('import.columns.field')}>{fieldLabel(e.field)}</td>
            <td data-label={t('import.columns.problem')}>{translateErrorCode(e.code)}</td>
          </tr>
        ))}
      </StackTable>
    </PageCard>
  );
}

function RowTable({ job }: { job: AssetImportJobItem }) {
  const { t } = useTranslation('assets');
  return (
    <PageCard title={t('import.rowsCaption')} flush>
      <StackTable
        caption={t('import.rowsCaption')}
        head={
          <tr>
            <th scope="col" className={HEAD_CLASS}>{t('list.columns.tag')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('list.columns.device')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('list.columns.location')}</th>
            <th scope="col" className={HEAD_CLASS}>{t('list.columns.status')}</th>
          </tr>
        }
      >
        {job.rows.map((r) => (
          <tr key={r.row}>
            <td data-label={t('list.columns.tag')}>{r.assetTag ?? t('import.autoTag')}</td>
            <td data-label={t('list.columns.device')}>{`${t(`types.${r.type}`)} · ${r.brand} ${r.model}`}</td>
            <td data-label={t('list.columns.location')}>{r.locationPath}</td>
            <td data-label={t('list.columns.status')}>{t(`statuses.${r.status}`)}</td>
          </tr>
        ))}
      </StackTable>
    </PageCard>
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
      <PageHeader icon={ROUTE_META.assets.icon} titleOverride={t('import.title')} />
      {/* A preview not yet imported is work the user would lose by leaving. */}
      <UnsavedChangesGuard when={job?.status === 'previewed'} />
      <div className="d-flex flex-column gap-3">
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (file !== null) {
              commit.reset();
              preview.mutate(file);
            }
          }}
        >
          <FormSection
            actions={
              <Button type="submit" variant="secondary" icon={cilSearch} disabled={file === null} isLoading={preview.isPending}>
                {t('import.preview')}
              </Button>
            }
          >
            <p className="col-12 mb-0">{t('import.intro')}</p>
            <div className="col-12">
              <a href={TEMPLATE_URL} download className="d-inline-flex align-items-center gap-2 link-primary text-decoration-underline">
                <CIcon icon={cilCloudDownload} aria-hidden="true" />
                {t('import.template')}
              </a>
            </div>
            <TextField
              type="file"
              accept={CSV_ACCEPT}
              label={t('import.file')}
              className="col-12 col-md-8"
              onChange={(event) => {
                // A new file invalidates the previous preview; its job must not be committable any more.
                preview.reset();
                commit.reset();
                setFile(event.target.files?.[0] ?? null);
              }}
            />
            <div className="col-12">
              <FormAlert message={preview.error ? describeImportError(preview.error) : null} />
              <FormAlert message={commit.error ? describeError(commit.error) : null} />
            </div>
          </FormSection>
        </form>

        {job && (
          <>
            <PageCard
              footer={
                job.status === 'previewed' ? (
                  <div className="d-flex justify-content-end">
                    <Button icon={cilCloudUpload} disabled={job.errorRows > 0 || job.validRows === 0} isLoading={commit.isPending} onClick={() => commit.mutate(job.jobId)}>
                      {t('import.commit')}
                    </Button>
                  </div>
                ) : undefined
              }
              bodyClassName="d-flex flex-column gap-2"
            >
              <p role="status" className="mb-0 fw-medium">
                {job.status === 'committed'
                  ? t('import.done', { count: job.validRows })
                  : t('import.summary', { total: job.totalRows, valid: job.validRows, errors: job.errorRows })}
              </p>
              {job.errorRows > 0 && <p className="mb-0">{t('import.fixAndRetry')}</p>}
            </PageCard>
            {job.errors.length > 0 && <ErrorTable job={job} />}
            {job.errorRows === 0 && job.rows.length > 0 && <RowTable job={job} />}
          </>
        )}
      </div>
    </>
  );
}
