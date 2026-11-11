/**
 * @input Nothing; the current SLA policy version comes from the API.
 * @output Target minutes and warning percents per kind, priority and metric, pause rules, and "save as new version".
 * @position `/admin/sla/policy`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { CheckboxInput } from '@astryxdesign/core/CheckboxInput';
import { HStack } from '@astryxdesign/core/HStack';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { Text } from '@astryxdesign/core/Text';
import { useToast } from '@astryxdesign/core/Toast';
import { VStack } from '@astryxdesign/core/VStack';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { FormAlert } from '../../../shared/feedback/FormAlert';
import { ErrorState, LoadingState } from '../../../shared/feedback/States';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { SectionCard } from '../../../shared/page/SectionCard';

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
  type Priority,
  type SlaMetric,
  type TicketKind,
} from './api';

const MIN_WARNING_PERCENT = 1;
const MAX_WARNING_PERCENT = 99;
const MIN_TARGET_MINUTES = 1;
const MAX_TARGET_MINUTES = 366 * 24 * 60;
const INPUT_WIDTH = 120;
const SKELETON_ROWS = 8;

interface CellDraft {
  minutes: number | null;
  percent: number | null;
}

type PauseDraft = Record<HoldReason, { firstResponse: boolean; resolution: boolean }>;

interface TargetRow {
  kind: TicketKind;
  priority: Priority;
}

const TARGET_ROWS: readonly TargetRow[] = SLA_KINDS.flatMap((kind) => SLA_PRIORITIES.map((priority) => ({ kind, priority })));

const cellKey = (kind: string, priority: string, metric: string) => `${kind}:${priority}:${metric}`;
const isBaseTarget = (target: PolicyTargetItem) => target.requestTypeId === null;

function inRange(value: number | null, min: number, max: number): number | null {
  return value !== null && Number.isInteger(value) && value >= min && value <= max ? value : null;
}

function PolicyForm({ policy }: { policy: PolicyDetail }) {
  const { t } = useTranslation('admin');
  const { t: tt } = useTranslation('tickets');
  const showToast = useToast();
  const create = useCreateSlaPolicyVersion();
  const [cells, setCells] = useState<Record<string, CellDraft>>(() =>
    Object.fromEntries(policy.targets.filter(isBaseTarget).map((x) => [cellKey(x.kind, x.priority, x.metric), { minutes: x.targetMinutes, percent: x.warningPercent }])),
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

  const setCell = (key: string, change: Partial<CellDraft>) => {
    setCells((current) => ({ ...current, [key]: { ...(current[key] ?? { minutes: null, percent: null }), ...change } }));
  };
  const setPause = (reason: HoldReason, metric: SlaMetric, value: boolean) => {
    setPauses((current) => ({ ...current, [reason]: { ...current[reason], [metric === 'first_response' ? 'firstResponse' : 'resolution']: value } }));
  };

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const targets: PolicyTargetItem[] = [];
    for (const { kind, priority } of TARGET_ROWS) {
      for (const metric of SLA_METRICS) {
        const cell = cells[cellKey(kind, priority, metric)];
        const targetMinutes = inRange(cell?.minutes ?? null, MIN_TARGET_MINUTES, MAX_TARGET_MINUTES);
        const warningPercent = inRange(cell?.percent ?? null, MIN_WARNING_PERCENT, MAX_WARNING_PERCENT);
        if (targetMinutes === null || warningPercent === null) {
          setInvalid(true);
          return;
        }
        targets.push({ kind, priority, requestTypeId: null, metric, targetMinutes, warningPercent });
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
    create.mutate({ targets: [...targets, ...overrides], pauseRules }, { onSuccess: () => showToast({ body: t('slaPolicy.saved') }) });
  };

  const alert = invalid ? t('slaPolicy.invalid') : create.error ? describeError(create.error) : null;
  const rowLabel = ({ kind, priority }: TargetRow) => `${tt(`kinds.${kind}`)} · ${tt(`priorities.${priority}`)}`;

  /** The column header names the inputs on screen; each label keeps the full name for screen readers. */
  const targetInputs = (row: TargetRow, metric: SlaMetric) => {
    const key = cellKey(row.kind, row.priority, metric);
    const label = `${tt(`kinds.${row.kind}`)} ${tt(`priorities.${row.priority}`)} ${tt(`sla.metrics.${metric}`)}`;
    return (
      <HStack gap={2} wrap="wrap">
        <NumberInput
          label={`${label} ${t('slaPolicy.targetMinutes')}`}
          isLabelHidden
          isIntegerOnly
          hasClear
          width={INPUT_WIDTH}
          units={t('slaPolicy.minutesUnit')}
          value={cells[key]?.minutes ?? null}
          onChange={(value: number | null) => setCell(key, { minutes: value })}
        />
        <NumberInput
          label={`${label} ${t('slaPolicy.warningPercent')}`}
          isLabelHidden
          isIntegerOnly
          hasClear
          width={INPUT_WIDTH}
          units={t('slaPolicy.percentUnit')}
          value={cells[key]?.percent ?? null}
          onChange={(value: number | null) => setCell(key, { percent: value })}
        />
      </HStack>
    );
  };

  const columns: DataColumn<TargetRow>[] = [
    { key: 'row', header: tt('sla.listColumn'), renderCell: (row) => <Text type="label">{rowLabel(row)}</Text> },
    ...SLA_METRICS.map((metric) => ({ key: metric, header: tt(`sla.metrics.${metric}`), renderCell: (row: TargetRow) => targetInputs(row, metric) })),
  ];
  const renderCard = (row: TargetRow) => (
    <VStack gap={2}>
      <Text type="label">{rowLabel(row)}</Text>
      {SLA_METRICS.map((metric) => (
        <VStack key={metric} gap={1}>
          <Text type="supporting">{tt(`sla.metrics.${metric}`)}</Text>
          {targetInputs(row, metric)}
        </VStack>
      ))}
    </VStack>
  );

  return (
    <form noValidate onSubmit={onSubmit}>
      <VStack gap={4}>
        <FormAlert message={alert} />
        <SectionCard title={t('slaPolicy.targetsTitle')} padding={0}>
          <DataList items={TARGET_ROWS} columns={columns} getKey={(row) => `${row.kind}-${row.priority}`} renderCard={renderCard} caption={t('slaPolicy.targetsTitle')} />
        </SectionCard>
        <SectionCard title={t('slaPolicy.pauseTitle')}>
          <Text type="supporting">{t('slaPolicy.pauseHint')}</Text>
          <VStack gap={3}>
            {SLA_HOLD_REASONS.map((reason) => (
              <VStack key={reason} gap={1}>
                <Text type="label">{tt(`holdReasons.${reason}`)}</Text>
                <HStack gap={4} wrap="wrap">
                  {SLA_METRICS.map((metric) => (
                    <CheckboxInput
                      key={metric}
                      label={`${tt(`holdReasons.${reason}`)}: ${metric === 'first_response' ? t('slaPolicy.pausesFirstResponse') : t('slaPolicy.pausesResolution')}`}
                      value={metric === 'first_response' ? pauses[reason].firstResponse : pauses[reason].resolution}
                      onChange={(checked: boolean) => setPause(reason, metric, checked)}
                    />
                  ))}
                </HStack>
              </VStack>
            ))}
          </VStack>
        </SectionCard>
        <HStack hAlign="end">
          <Button type="submit" label={t('slaPolicy.save')} variant="primary" isLoading={create.isPending} />
        </HStack>
      </VStack>
    </form>
  );
}

export function SlaPolicyPage() {
  const { t } = useTranslation('admin');
  const policy = useSlaPolicy();
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminSlaPolicy.icon}
        titleOverride={t('slaPolicy.title')}
        description={policy.data ? t('slaPolicy.version', { number: policy.data.number }) : undefined}
      />
      {policy.isPending && <LoadingState rows={SKELETON_ROWS} />}
      {policy.isError && <ErrorState error={policy.error} onRetry={() => void policy.refetch()} />}
      {policy.data && <PolicyForm key={policy.data.versionId} policy={policy.data} />}
    </VStack>
  );
}
