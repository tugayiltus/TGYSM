/**
 * @input Nothing; the person chooses a CSV file.
 * @output The CSV import: template link, file choice, preview with row errors or rows, then the import.
 * @position `/assets/import`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { FileInput } from '@astryxdesign/core/FileInput';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Table } from '@astryxdesign/core/Table';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError, translateErrorCode } from '../../shared/api/errorMessage';
import { ApiError } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { UnsavedChangesGuard } from '../../shared/feedback/UnsavedChangesGuard';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { SectionCard } from '../../shared/page/SectionCard';
import { Icons } from '../../shared/ui/icons';

import { useCommitImport, usePreviewImport, type AssetImportJobItem } from './api';

const TEMPLATE_URL = '/api/v1/assets/import/template';
const FILE_FIELD = 'file';
const CSV_ACCEPT = '.csv,text/csv';

/** A file-level problem (wrong header, too big) arrives as a field error on "file". */
function describeImportError(error: unknown): string {
  const fileCode = error instanceof ApiError ? error.fieldErrors[FILE_FIELD]?.[0] : undefined;
  return fileCode === undefined ? describeError(error) : translateErrorCode(fileCode);
}

const META_SEPARATOR = ' · ';
/** Readable width for the file form. */
const FORM_MAX_WIDTH = 720;

const toFile = (value: File | File[] | null): File | null => (Array.isArray(value) ? (value[0] ?? null) : value);

function ErrorTable({ job }: { job: AssetImportJobItem }) {
  const { t } = useTranslation('assets');
  const fieldLabel = (field: string) => t(`fields.${field}` as 'fields.assetTag', { defaultValue: field });
  const rows = job.errors.map((error) => ({ key: `${error.row}-${error.field}-${error.code}`, row: error.row, field: fieldLabel(error.field), problem: translateErrorCode(error.code) }));
  return (
    <SectionCard title={t('import.errorsCaption')} padding={0}>
      <Table
        aria-label={t('import.errorsCaption')}
        data={rows}
        idKey="key"
        columns={[
          { key: 'row', header: t('import.columns.row') },
          { key: 'field', header: t('import.columns.field') },
          { key: 'problem', header: t('import.columns.problem') },
        ]}
      />
    </SectionCard>
  );
}

function RowTable({ job }: { job: AssetImportJobItem }) {
  const { t } = useTranslation('assets');
  const rows = job.rows.map((row) => ({
    key: String(row.row),
    tag: row.assetTag ?? t('import.autoTag'),
    device: `${t(`types.${row.type}`)}${META_SEPARATOR}${row.brand} ${row.model}`,
    location: row.locationPath,
    status: t(`statuses.${row.status}`),
  }));
  return (
    <SectionCard title={t('import.rowsCaption')} padding={0}>
      <Table
        aria-label={t('import.rowsCaption')}
        data={rows}
        idKey="key"
        columns={[
          { key: 'tag', header: t('list.columns.tag') },
          { key: 'device', header: t('list.columns.device') },
          { key: 'location', header: t('list.columns.location') },
          { key: 'status', header: t('list.columns.status') },
        ]}
      />
    </SectionCard>
  );
}

export function AssetImportPage() {
  const { t } = useTranslation('assets');
  const [file, setFile] = useState<File | null>(null);
  const preview = usePreviewImport();
  const commit = useCommitImport();
  const job = commit.data ?? preview.data;

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.assets.icon} titleOverride={t('import.title')} />
      {/* A preview not yet imported is work the user would lose by leaving. */}
      <UnsavedChangesGuard when={job?.status === 'previewed'} />
      <Card padding={6} maxWidth={FORM_MAX_WIDTH}>
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (file !== null) {
              commit.reset();
              preview.mutate(file);
            }
          }}
        >
          <VStack gap={4}>
            <Text type="body">{t('import.intro')}</Text>
            {/* A native download link: the router must not handle a file URL. */}
            <a href={TEMPLATE_URL} download>
              <HStack gap={1} vAlign="center">
                <Icon icon={Icons.download} size="sm" color="accent" />
                <Text type="body" color="accent">
                  {t('import.template')}
                </Text>
              </HStack>
            </a>
            <FileInput
              label={t('import.file')}
              accept={CSV_ACCEPT}
              value={file}
              onChange={(value) => {
                // A new file invalidates the previous preview; its job must not be committable any more.
                preview.reset();
                commit.reset();
                setFile(toFile(value));
              }}
            />
            <FormAlert message={preview.error ? describeImportError(preview.error) : null} />
            <FormAlert message={commit.error ? describeError(commit.error) : null} />
            <HStack hAlign="end">
              <Button type="submit" label={t('import.preview')} variant="secondary" icon={<Icon icon={Icons.search} />} isDisabled={file === null} isLoading={preview.isPending} />
            </HStack>
          </VStack>
        </form>
      </Card>

      {job && (
        <>
          <Card padding={5}>
            <VStack gap={3}>
              <VStack role="status">
                <Text type="label">
                  {job.status === 'committed' ? t('import.done', { count: job.validRows }) : t('import.summary', { total: job.totalRows, valid: job.validRows, errors: job.errorRows })}
                </Text>
              </VStack>
              {job.errorRows > 0 && <Text type="body">{t('import.fixAndRetry')}</Text>}
              {job.status === 'previewed' && (
                <HStack hAlign="end">
                  <Button
                    label={t('import.commit')}
                    variant="primary"
                    icon={<Icon icon={Icons.upload} />}
                    isDisabled={job.errorRows > 0 || job.validRows === 0}
                    isLoading={commit.isPending}
                    onClick={() => commit.mutate(job.jobId)}
                  />
                </HStack>
              )}
            </VStack>
          </Card>
          {job.errors.length > 0 && <ErrorTable job={job} />}
          {job.errorRows === 0 && job.rows.length > 0 && <RowTable job={job} />}
        </>
      )}
    </VStack>
  );
}
