import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { Button } from '../../../shared/ui/Button';
import { CheckField } from '../../../shared/ui/CheckField';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { FormSection } from '../../../shared/ui/FormSection';
import { PageCard } from '../../../shared/ui/PageCard';
import { StackTable } from '../../../shared/ui/StackTable';
import { ErrorState, LoadingState } from '../../../shared/ui/States';
import { TextField } from '../../../shared/ui/TextField';

import {
  SLA_HOLD_REASONS,
  SLA_KINDS,
  SLA_METRICS,
  SLA_PRIORITIES,
  useCreateSlaPolicyVersion,
  useSlaPolicy,
  type HoldReason,
  type PauseRuleItem,
  type PolicyDetail,
  type PolicyTargetItem,
  type SlaMetric,
} from './api';

const MIN_WARNING_PERCENT = 1;
const MAX_WARNING_PERCENT = 99;
const MIN_TARGET_MINUTES = 1;
const MAX_TARGET_MINUTES = 366 * 24 * 60;

interface CellDraft {
  minutes: string;
  percent: string;
}

type PauseDraft = Record<HoldReason, { firstResponse: boolean; resolution: boolean }>;

const cellKey = (kind: string, priority: string, metric: string) => `${kind}:${priority}:${metric}`;
const isBaseTarget = (target: PolicyTargetItem) => target.requestTypeId === null;

function inRange(text: string, min: number, max: number): number | null {
  const value = Number(text);
  return text.trim() !== '' && Number.isInteger(value) && value >= min && value <= max ? value : null;
}

function PolicyForm({ policy }: { policy: PolicyDetail }) {
  const { t } = useTranslation('admin');
  const { t: tt } = useTranslation('tickets');
  const create = useCreateSlaPolicyVersion();
  const [cells, setCells] = useState<Record<string, CellDraft>>(() =>
    Object.fromEntries(policy.targets.filter(isBaseTarget).map((x) => [cellKey(x.kind, x.priority, x.metric), { minutes: String(x.targetMinutes), percent: String(x.warningPercent) }])),
  );
  const [pauses, setPauses] = useState<PauseDraft>(() => {
    const draft = {} as PauseDraft;
    for (const reason of SLA_HOLD_REASONS) {
      const rule = policy.pauseRules.find((r) => r.holdReason === reason);
      draft[reason] = { firstResponse: rule?.pausesFirstResponse ?? false, resolution: rule?.pausesResolution ?? false };
    }
    return draft;
  });
  const [invalid, setInvalid] = useState(false);
  const [saved, setSaved] = useState(false);

  const setCell = (key: string, change: Partial<CellDraft>) => {
    setCells((current) => ({ ...current, [key]: { ...(current[key] ?? { minutes: '', percent: '' }), ...change } }));
    setSaved(false);
  };
  const setPause = (reason: HoldReason, metric: SlaMetric, value: boolean) => {
    setPauses((current) => ({ ...current, [reason]: { ...current[reason], [metric === 'first_response' ? 'firstResponse' : 'resolution']: value } }));
    setSaved(false);
  };

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const targets: PolicyTargetItem[] = [];
    for (const kind of SLA_KINDS) {
      for (const priority of SLA_PRIORITIES) {
        for (const metric of SLA_METRICS) {
          const cell = cells[cellKey(kind, priority, metric)];
          const targetMinutes = inRange(cell?.minutes ?? '', MIN_TARGET_MINUTES, MAX_TARGET_MINUTES);
          const warningPercent = inRange(cell?.percent ?? '', MIN_WARNING_PERCENT, MAX_WARNING_PERCENT);
          if (targetMinutes === null || warningPercent === null) {
            setInvalid(true);
            return;
          }
          targets.push({ kind, priority, requestTypeId: null, metric, targetMinutes, warningPercent });
        }
      }
    }
    setInvalid(false);
    const pauseRules: PauseRuleItem[] = SLA_HOLD_REASONS.filter((r) => pauses[r].firstResponse || pauses[r].resolution).map((holdReason) => ({
      holdReason,
      pausesFirstResponse: pauses[holdReason].firstResponse,
      pausesResolution: pauses[holdReason].resolution,
    }));
    // Request-type overrides are not edited here; they carry over unchanged into the new version.
    const overrides = policy.targets.filter((x) => !isBaseTarget(x));
    create.mutate({ targets: [...targets, ...overrides], pauseRules }, { onSuccess: () => setSaved(true) });
  };

  const alert = invalid ? t('slaPolicy.invalid') : create.error ? describeError(create.error) : null;

  return (
    <form noValidate className="d-flex flex-column gap-3" onSubmit={onSubmit}>
      <FormAlert message={alert} />
      <PageCard title={t('slaPolicy.targetsTitle')} flush>
        <StackTable
          caption={t('slaPolicy.targetsTitle')}
          head={
            <tr>
              <th scope="col" className="text-body-secondary small text-uppercase fw-semibold">
                {tt('sla.listColumn')}
              </th>
              {SLA_METRICS.map((metric) => (
                <th key={metric} scope="col" className="text-body-secondary small text-uppercase fw-semibold">
                  {tt(`sla.metrics.${metric}`)}
                </th>
              ))}
            </tr>
          }
        >
          {SLA_KINDS.flatMap((kind) =>
            SLA_PRIORITIES.map((priority) => (
              <tr key={`${kind}-${priority}`}>
                <th scope="row" className="fw-medium">{`${tt(`kinds.${kind}`)} · ${tt(`priorities.${priority}`)}`}</th>
                {SLA_METRICS.map((metric) => {
                  const key = cellKey(kind, priority, metric);
                  const label = `${tt(`kinds.${kind}`)} ${tt(`priorities.${priority}`)} ${tt(`sla.metrics.${metric}`)}`;
                  return (
                    <td key={metric} data-label={tt(`sla.metrics.${metric}`)}>
                      <div className="d-flex flex-wrap gap-2">
                        {/* The column header names the cell; the full label stays for screen readers. */}
                        <TextField
                          className="tgysm-label-hidden tgysm-number-field"
                          type="number"
                          inputMode="numeric"
                          label={`${label} ${t('slaPolicy.targetMinutes')}`}
                          endText={t('slaPolicy.minutesUnit')}
                          value={cells[key]?.minutes ?? ''}
                          onChange={(event) => setCell(key, { minutes: event.target.value })}
                        />
                        <TextField
                          className="tgysm-label-hidden tgysm-number-field"
                          type="number"
                          inputMode="numeric"
                          label={`${label} ${t('slaPolicy.warningPercent')}`}
                          endText={t('slaPolicy.percentUnit')}
                          value={cells[key]?.percent ?? ''}
                          onChange={(event) => setCell(key, { percent: event.target.value })}
                        />
                      </div>
                    </td>
                  );
                })}
              </tr>
            )),
          )}
        </StackTable>
      </PageCard>
      <FormSection
        title={t('slaPolicy.pauseTitle')}
        actions={
          <>
            {saved && (
              <p role="status" className="mb-0 me-auto align-self-center text-success-emphasis">
                {t('slaPolicy.saved')}
              </p>
            )}
            <Button type="submit" isLoading={create.isPending}>
              {t('slaPolicy.save')}
            </Button>
          </>
        }
      >
        <p className="col-12 mb-0 small text-body-secondary">{t('slaPolicy.pauseHint')}</p>
        {SLA_HOLD_REASONS.map((reason) => (
          <div key={reason} className="col-12 d-flex flex-wrap align-items-center column-gap-4 row-gap-1">
            <span className="small fw-medium tgysm-pause-reason">{tt(`holdReasons.${reason}`)}</span>
            {SLA_METRICS.map((metric) => (
              <CheckField
                key={metric}
                label={`${tt(`holdReasons.${reason}`)}: ${metric === 'first_response' ? t('slaPolicy.pausesFirstResponse') : t('slaPolicy.pausesResolution')}`}
                checked={metric === 'first_response' ? pauses[reason].firstResponse : pauses[reason].resolution}
                onChange={(event) => setPause(reason, metric, event.target.checked)}
              />
            ))}
          </div>
        ))}
      </FormSection>
    </form>
  );
}

export function SlaPolicyPage() {
  const { t } = useTranslation('admin');
  const policy = useSlaPolicy();
  return (
    <section>
      <PageHeader
        icon={ROUTE_META.adminSlaPolicy.icon}
        titleOverride={t('slaPolicy.title')}
        description={policy.data ? t('slaPolicy.version', { number: policy.data.number }) : undefined}
      />
      {policy.isPending && <LoadingState />}
      {policy.isError && <ErrorState error={policy.error} onRetry={() => void policy.refetch()} />}
      {policy.data && <PolicyForm policy={policy.data} />}
    </section>
  );
}
