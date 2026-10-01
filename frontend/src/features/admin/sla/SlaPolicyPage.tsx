import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { Button } from '../../../shared/ui/Button';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { ErrorState, LoadingState } from '../../../shared/ui/States';

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
const INPUT_CLASS = 'min-h-11 w-24 rounded-lg border border-slate-300 bg-white px-2 text-base';

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
    <form noValidate className="flex flex-col gap-6" onSubmit={onSubmit}>
      <FormAlert message={alert} />
      <section aria-labelledby="sla-targets" className="flex flex-col gap-3">
        <h2 id="sla-targets" className="text-lg font-semibold">
          {t('slaPolicy.targetsTitle')}
        </h2>
        <div className="overflow-x-auto">
          <table className="w-full min-w-max text-left text-sm">
            <thead>
              <tr>
                <th scope="col" className="p-2">
                  {tt('sla.listColumn')}
                </th>
                {SLA_METRICS.map((metric) => (
                  <th key={metric} scope="col" className="p-2">
                    {tt(`sla.metrics.${metric}`)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {SLA_KINDS.flatMap((kind) =>
                SLA_PRIORITIES.map((priority) => (
                  <tr key={`${kind}-${priority}`} className="border-t border-slate-100">
                    <th scope="row" className="p-2 font-medium">{`${tt(`kinds.${kind}`)} · ${tt(`priorities.${priority}`)}`}</th>
                    {SLA_METRICS.map((metric) => {
                      const key = cellKey(kind, priority, metric);
                      const label = `${tt(`kinds.${kind}`)} ${tt(`priorities.${priority}`)} ${tt(`sla.metrics.${metric}`)}`;
                      return (
                        <td key={metric} className="p-2">
                          <div className="flex gap-2">
                            <input
                              type="number"
                              inputMode="numeric"
                              className={INPUT_CLASS}
                              aria-label={`${label} ${t('slaPolicy.targetMinutes')}`}
                              value={cells[key]?.minutes ?? ''}
                              onChange={(event) => setCell(key, { minutes: event.target.value })}
                            />
                            <input
                              type="number"
                              inputMode="numeric"
                              className={INPUT_CLASS}
                              aria-label={`${label} ${t('slaPolicy.warningPercent')}`}
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
            </tbody>
          </table>
        </div>
      </section>
      <section aria-labelledby="sla-pauses" className="flex flex-col gap-3">
        <h2 id="sla-pauses" className="text-lg font-semibold">
          {t('slaPolicy.pauseTitle')}
        </h2>
        <p className="text-sm text-slate-600">{t('slaPolicy.pauseHint')}</p>
        <ul className="flex flex-col gap-2">
          {SLA_HOLD_REASONS.map((reason) => (
            <li key={reason} className="flex flex-wrap items-center gap-x-6 gap-y-1">
              <span className="min-w-60 text-sm font-medium">{tt(`holdReasons.${reason}`)}</span>
              {SLA_METRICS.map((metric) => (
                <label key={metric} className="flex min-h-11 items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={metric === 'first_response' ? pauses[reason].firstResponse : pauses[reason].resolution}
                    onChange={(event) => setPause(reason, metric, event.target.checked)}
                  />
                  {`${tt(`holdReasons.${reason}`)}: ${metric === 'first_response' ? t('slaPolicy.pausesFirstResponse') : t('slaPolicy.pausesResolution')}`}
                </label>
              ))}
            </li>
          ))}
        </ul>
      </section>
      <div className="flex items-center gap-3">
        <Button type="submit" isLoading={create.isPending}>
          {t('slaPolicy.save')}
        </Button>
        {saved && <p role="status">{t('slaPolicy.saved')}</p>}
      </div>
    </form>
  );
}

export function SlaPolicyPage() {
  const { t } = useTranslation('admin');
  const policy = useSlaPolicy();
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('slaPolicy.title')}</h1>
      {policy.isPending && <LoadingState />}
      {policy.isError && <ErrorState error={policy.error} onRetry={() => void policy.refetch()} />}
      {policy.data && (
        <>
          <p className="text-sm text-slate-700">{t('slaPolicy.version', { number: policy.data.number })}</p>
          <PolicyForm policy={policy.data} />
        </>
      )}
    </section>
  );
}
