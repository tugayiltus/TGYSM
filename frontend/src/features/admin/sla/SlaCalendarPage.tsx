/**
 * @input Nothing; the working calendar and holidays come from the API.
 * @output The SLA time zone and weekly working hours, and the holiday list with add and remove.
 * @position `/admin/sla/calendar`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { DateInput } from '@astryxdesign/core/DateInput';
import { HStack } from '@astryxdesign/core/HStack';
import { Switch } from '@astryxdesign/core/Switch';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { TimeInput } from '@astryxdesign/core/TimeInput';
import { useToast } from '@astryxdesign/core/Toast';
import { VStack } from '@astryxdesign/core/VStack';
import { useState, type ComponentProps, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError, translateErrorCode } from '../../../shared/api/errorMessage';
import { ApiError, ERROR_CODES } from '../../../shared/api/errors';
import { FormAlert } from '../../../shared/feedback/FormAlert';
import { ErrorState, LoadingState } from '../../../shared/feedback/States';
import { formatDate } from '../../../shared/lib/format';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { SectionCard } from '../../../shared/page/SectionCard';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';

import {
  WEEKDAYS,
  useAddHoliday,
  useRemoveHoliday,
  useSlaCalendar,
  useSlaHolidays,
  useUpdateSlaCalendar,
  type CalendarDetail,
  type DayOfWeek,
  type HolidayItem,
  type WorkingHoursItem,
} from './api';
import { SECONDS_SUFFIX, toWeekDraft, validateCalendar, type DayDraft, type WeekDraft } from './calendarDraft';

type IsoTime = ComponentProps<typeof TimeInput>['value'];
type IsoDay = ComponentProps<typeof DateInput>['value'];

const TIME_INPUT_WIDTH = 140;
const SWITCH_WIDTH = 220;
const SKELETON_ROWS = 4;
const HOUR_FORMAT = '24h';

/** Drafts keep "HH:mm" text; Astryx takes the same ISO form, and an empty draft means no value. */
const asIsoTime = (time: string): IsoTime => (time === '' ? undefined : (time as IsoTime));
const asIsoDay = (day: string): IsoDay => (day === '' ? undefined : (day as IsoDay));

/**
 * The form's working copy. `baseVersion` is the server version the draft was loaded from: a save always sends it,
 * never a version refetched in the background, so a stale draft cannot silently overwrite a newer calendar.
 */
interface CalendarDraft {
  baseVersion: number;
  timeZone: string;
  week: WeekDraft;
}

function toDraft(calendar: CalendarDetail): CalendarDraft {
  return { baseVersion: calendar.version, timeZone: calendar.timeZone, week: toWeekDraft(calendar.hours) };
}

function isConflict(error: unknown): boolean {
  return error instanceof ApiError && error.code === ERROR_CODES.concurrencyConflict;
}

function CalendarForm({ calendar, onReload }: { calendar: CalendarDetail; onReload: () => Promise<CalendarDetail | undefined> }) {
  const { t } = useTranslation('admin');
  const showToast = useToast();
  const update = useUpdateSlaCalendar();
  const [draft, setDraft] = useState<CalendarDraft>(() => toDraft(calendar));
  const [problem, setProblem] = useState<string | null>(null);
  const { timeZone, week } = draft;

  const setDay = (day: DayOfWeek, change: Partial<DayDraft>) => {
    setDraft((current) => ({ ...current, week: { ...current.week, [day]: { ...current.week[day], ...change } } }));
  };

  const reload = async () => {
    const fresh = await onReload();
    if (fresh) {
      setDraft(toDraft(fresh));
      setProblem(null);
      update.reset();
    }
  };

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const code = validateCalendar(timeZone, week);
    setProblem(code);
    if (code !== null) {
      return;
    }
    const hours: WorkingHoursItem[] = WEEKDAYS.filter((day) => week[day].open).map((day) => ({
      weekday: day,
      startsAt: `${week[day].starts}${SECONDS_SUFFIX}`,
      endsAt: `${week[day].ends}${SECONDS_SUFFIX}`,
    }));
    update.mutate(
      { version: draft.baseVersion, timeZone: timeZone.trim(), hours },
      {
        onSuccess: (result) => {
          setDraft((current) => ({ ...current, baseVersion: result.version }));
          showToast({ body: t('slaCalendar.saved') });
        },
      },
    );
  };

  const alert = problem !== null ? translateErrorCode(problem) : update.error ? describeError(update.error) : null;

  return (
    <form noValidate onSubmit={onSubmit}>
      <SectionCard title={t('slaCalendar.hoursTitle')}>
        <VStack gap={4}>
          <FormAlert message={alert} />
          {problem === null && isConflict(update.error) && (
            <HStack>
              <Button label={t('slaCalendar.reload')} variant="secondary" onClick={() => void reload()} />
            </HStack>
          )}
          <TextInput
            label={t('slaCalendar.timeZone')}
            description={t('slaCalendar.timeZoneHint')}
            autoComplete="off"
            isRequired
            value={timeZone}
            onChange={(value) => setDraft((current) => ({ ...current, timeZone: value }))}
          />
          <VStack gap={3}>
            {WEEKDAYS.map((day) => {
              const name = t(`slaCalendar.weekdays.${day}`);
              return (
                <HStack key={day} gap={3} vAlign="end" wrap="wrap">
                  <VStack width={SWITCH_WIDTH}>
                    <Switch label={`${name} · ${t('slaCalendar.open')}`} value={week[day].open} onChange={(open: boolean) => setDay(day, { open })} />
                  </VStack>
                  <TimeInput
                    label={`${name} ${t('slaCalendar.starts')}`}
                    hourFormat={HOUR_FORMAT}
                    width={TIME_INPUT_WIDTH}
                    isDisabled={!week[day].open}
                    value={asIsoTime(week[day].starts)}
                    onChange={(value) => setDay(day, { starts: value ?? '' })}
                  />
                  <TimeInput
                    label={`${name} ${t('slaCalendar.ends')}`}
                    hourFormat={HOUR_FORMAT}
                    width={TIME_INPUT_WIDTH}
                    isDisabled={!week[day].open}
                    value={asIsoTime(week[day].ends)}
                    onChange={(value) => setDay(day, { ends: value ?? '' })}
                  />
                </HStack>
              );
            })}
          </VStack>
          <HStack hAlign="end">
            <Button type="submit" label={t('slaCalendar.save')} variant="primary" isLoading={update.isPending} />
          </HStack>
        </VStack>
      </SectionCard>
    </form>
  );
}

function HolidaysSection() {
  const { t } = useTranslation('admin');
  const holidays = useSlaHolidays();
  const add = useAddHoliday();
  const remove = useRemoveHoliday();
  const [date, setDate] = useState('');
  const [name, setName] = useState('');

  const onAdd = (event: FormEvent) => {
    event.preventDefault();
    if (date === '' || name.trim() === '') {
      return;
    }
    add.mutate(
      { date, name: name.trim() },
      {
        onSuccess: () => {
          setDate('');
          setName('');
        },
      },
    );
  };

  const removeButton = (holiday: HolidayItem) => (
    <Button
      label={t('slaCalendar.holidays.remove')}
      aria-label={recordActionLabel(t('slaCalendar.holidays.remove'), holiday.name)}
      size="sm"
      variant="secondary"
      isLoading={remove.isPending && remove.variables === holiday.id}
      onClick={() => remove.mutate(holiday.id)}
    />
  );

  const columns: DataColumn<HolidayItem>[] = [
    { key: 'date', header: t('slaCalendar.holidays.date'), renderCell: (h) => formatDate(h.date) },
    { key: 'name', header: t('slaCalendar.holidays.name'), renderCell: (h) => <Text type="label">{h.name}</Text> },
    { key: 'actions', header: t('slaCalendar.holidays.actions'), renderCell: removeButton },
  ];
  const renderCard = (h: HolidayItem) => (
    <VStack gap={2}>
      <VStack gap={0}>
        <Text type="label">{h.name}</Text>
        <Text type="supporting">{formatDate(h.date)}</Text>
      </VStack>
      <HStack>{removeButton(h)}</HStack>
    </VStack>
  );

  return (
    <SectionCard title={t('slaCalendar.holidays.title')}>
      <form noValidate onSubmit={onAdd}>
        <HStack gap={3} vAlign="end" wrap="wrap">
          <DateInput label={t('slaCalendar.holidays.date')} value={asIsoDay(date)} onChange={(value) => setDate(value ?? '')} />
          <TextInput label={t('slaCalendar.holidays.name')} autoComplete="off" value={name} onChange={setName} />
          <Button type="submit" label={t('slaCalendar.holidays.add')} variant="secondary" isLoading={add.isPending} />
        </HStack>
      </form>
      <FormAlert message={add.error ? describeError(add.error) : remove.error ? describeError(remove.error) : null} />
      {holidays.isPending && <LoadingState rows={SKELETON_ROWS} />}
      {holidays.isError && <ErrorState error={holidays.error} onRetry={() => void holidays.refetch()} />}
      {holidays.data && holidays.data.length === 0 && <Text type="supporting">{t('slaCalendar.holidays.empty')}</Text>}
      {holidays.data && holidays.data.length > 0 && (
        <DataList items={holidays.data} columns={columns} getKey={(h) => h.id} renderCard={renderCard} caption={t('slaCalendar.holidays.caption')} />
      )}
    </SectionCard>
  );
}

export function SlaCalendarPage() {
  const { t } = useTranslation('admin');
  const calendar = useSlaCalendar();
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminSlaCalendar.icon} titleOverride={t('slaCalendar.title')} />
      {calendar.isPending && <LoadingState rows={SKELETON_ROWS} />}
      {calendar.isError && <ErrorState error={calendar.error} onRetry={() => void calendar.refetch()} />}
      {calendar.data && <CalendarForm calendar={calendar.data} onReload={async () => (await calendar.refetch()).data} />}
      <HolidaysSection />
    </VStack>
  );
}
