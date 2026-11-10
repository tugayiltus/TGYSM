/**
 * @input A report, the drill-down selection and the filter.
 * @output An export button; above the synchronous row limit it offers a background export and links to "Dışa aktarımlarım".
 * @position Drill-down list pages.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { ApiError, ERROR_CODES } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { ROUTES } from '../../shared/routes';

import { useCreateExportJob, useSyncExport, type DrillSelection, type ReportName } from './api';
import type { ReportFilterValues } from './useReportFilter';

export interface ExportButtonProps {
  report: ReportName;
  selection: DrillSelection;
  filter: ReportFilterValues;
}

const isTooLarge = (error: unknown) => error instanceof ApiError && error.code === ERROR_CODES.reportExportTooLarge;

/** Above the synchronous row limit the server answers 409 `reports.export_too_large`. */
export function ExportButton({ report, selection, filter }: ExportButtonProps) {
  const { t } = useTranslation('reports');
  const sync = useSyncExport(report);
  const background = useCreateExportJob(report);
  const offerBackground = isTooLarge(sync.error) && !background.isSuccess;

  const exportNow = () => {
    background.reset();
    sync.mutate({ selection, filter });
  };

  return (
    <VStack gap={2} hAlign="start">
      <Button label={t('export.button')} variant="secondary" isLoading={sync.isPending} onClick={exportNow} />
      {sync.isError && !isTooLarge(sync.error) && <FormAlert message={describeError(sync.error)} />}
      {offerBackground && (
        <Banner
          status="warning"
          title={t('export.tooLarge')}
          endContent={<Button label={t('export.background')} variant="primary" isLoading={background.isPending} onClick={() => background.mutate({ selection, filter })} />}
        />
      )}
      {background.isError && <FormAlert message={describeError(background.error)} />}
      {background.isSuccess && (
        <VStack role="status">
          <Banner
            status="success"
            title={
              <HStack gap={1} wrap="wrap">
                <Text type="inherit">{t('export.queued')}</Text>
                <Link href={ROUTES.reportExports}>{t('export.queuedLink')}</Link>
              </HStack>
            }
          />
        </VStack>
      )}
    </VStack>
  );
}
