import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { ApiError, ERROR_CODES } from '../../shared/api/errors';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';

import { useCreateExportJob, useSyncExport, type DrillSelection, type ReportName } from './api';
import type { ReportFilterValues } from './useReportFilter';

export interface ExportButtonProps {
  report: ReportName;
  selection: DrillSelection;
  filter: ReportFilterValues;
}

const isTooLarge = (error: unknown) => error instanceof ApiError && error.code === ERROR_CODES.reportExportTooLarge;

/**
 * Downloads the rows at once; above the synchronous row limit (409 `reports.export_too_large`) it offers a background
 * export instead and then points to "Dışa aktarımlarım".
 */
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
    <div className="flex flex-col items-start gap-3">
      <Button variant="secondary" isLoading={sync.isPending} onClick={exportNow}>
        {t('export.button')}
      </Button>
      {sync.isError && !isTooLarge(sync.error) && <FormAlert message={describeError(sync.error)} />}
      {offerBackground && (
        <div className="flex flex-col items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
          <p>{t('export.tooLarge')}</p>
          <Button isLoading={background.isPending} onClick={() => background.mutate({ selection, filter })}>
            {t('export.background')}
          </Button>
        </div>
      )}
      {background.isError && <FormAlert message={describeError(background.error)} />}
      {background.isSuccess && (
        <p role="status" className="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900">
          {t('export.queued')}{' '}
          <Link to={ROUTES.reportExports} className="font-medium underline">
            {t('export.queuedLink')}
          </Link>
        </p>
      )}
    </div>
  );
}
