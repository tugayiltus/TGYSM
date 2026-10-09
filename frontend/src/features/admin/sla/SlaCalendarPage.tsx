import { useId, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError, translateErrorCode } from '../../../shared/api/errorMessage';
import { ApiError, ERROR_CODES } from '../../../shared/api/errors';
import { formatDate } from '../../../shared/lib/format';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { Button } from '../../../shared/ui/Button';
import { CheckField } from '../../../shared/ui/CheckField';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { FormSection } from '../../../shared/ui/FormSection';
import { PageCard } from '../../../shared/ui/PageCard';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { ErrorState, LoadingState } from '../../../shared/ui/States';
import { TextField } from '../../../shared/ui/TextField';

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
  const formId = useId();
  const update = useUpdateSlaCalendar();
  const [draft, setDraft] = useState<CalendarDraft>(() => toDraft(calendar));
  const [problem, setProblem] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const { timeZone, week } = draft;

  const setDay = (day: DayOfWeek, change: Partial<DayDraft>) => {
    setDraft((current) => ({ ...current, week: { ...current.week, [day]: { ...current.week[day], ...change } } }));
    setSaved(false);
  };

  const reload = async () => {
    const fresh = await onReload();
    if (fresh) {
      setDraft(toDraft(fresh));
      setProblem(null);
      setSaved(false);
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
          setSaved(true);
        },
      },
    );
  };

  const alert = problem !== null ? translateErrorCode(problem) : update.error ? describeError(update.error) : null;

  return (
    <form id={formId} noValidate onSubmit={onSubmit}>
      <FormSection
        title={t('slaCalendar.hoursTitle')}
        actions={
          <>
            {saved && (
              <p role="status" className="mb-0 me-auto align-self-center text-success-emphasis">
                {t('slaCalendar.saved')}
              </p>
            )}
            <Button type="submit" isLoading={update.isPending}>
              {t('slaCalendar.save')}
            </Button>
          </>
        }
      >
        <div className="col-12">
          <FormAlert message={alert} />
        </div>
        {problem === null && isConflict(update.error) && (
          <div className="col-12">
            <Button variant="secondary" onClick={() => void reload()}>
              {t('slaCalendar.reload')}
            </Button>
          </div>
        )}
        <TextField
          className="col-12 col-md-6"
          label={t('slaCalendar.timeZone')}
          hint={t('slaCalendar.timeZoneHint')}
          autoComplete="off"
          value={timeZone}
          onChange={(event) => {
            const value = event.target.value;
            setDraft((current) => ({ ...current, timeZone: value }));
            setSaved(false);
          }}
        />
        {WEEKDAYS.map((day) => {
          const name = t(`slaCalendar.weekdays.${day}`);
          return (
            <div key={day} className="col-12 tgysm-grid tgysm-grid-calendar-day align-items-end">
              <CheckField type="switch" label={`${name} · ${t('slaCalendar.open')}`} checked={week[day].open} onChange={(event) => setDay(day, { open: event.target.checked })} />
              <TextField
                type="time"
                label={`${name} ${t('slaCalendar.starts')}`}
                disabled={!week[day].open}
                value={week[day].starts}
                onChange={(event) => setDay(day, { starts: event.target.value })}
              />
              <TextField
                type="time"
                label={`${name} ${t('slaCalendar.ends')}`}
                disabled={!week[day].open}
                value={week[day].ends}
                onChange={(event) => setDay(day, { ends: event.target.value })}
              />
            </div>
          );
        })}
      </FormSection>
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
    <Button variant="secondary" aria-label={recordActionLabel(t('slaCalendar.holidays.remove'), holiday.name)} isLoading={remove.isPending} onClick={() => remove.mutate(holiday.id)}>
      {t('slaCalendar.holidays.remove')}
    </Button>
  );

  const columns: Column<HolidayItem>[] = [
    { key: 'date', header: t('slaCalendar.holidays.date'), renderCell: (h) => formatDate(h.date) },
    { key: 'name', header: t('slaCalendar.holidays.name'), renderCell: (h) => h.name },
    { key: 'actions', header: '', renderCell: removeButton },
  ];
  const renderCard = (h: HolidayItem) => (
    <div className="d-flex flex-column gap-2">
      <p className="mb-0 fw-medium">{h.name}</p>
      <p className="mb-0 small text-body-secondary">{formatDate(h.date)}</p>
      {removeButton(h)}
    </div>
  );

  return (
    <PageCard title={t('slaCalendar.holidays.title')} bodyClassName="d-flex flex-column gap-3">
      <form noValidate className="tgysm-grid tgysm-grid-holiday align-items-end" onSubmit={onAdd}>
        <TextField type="date" label={t('slaCalendar.holidays.date')} value={date} onChange={(event) => setDate(event.target.value)} />
        <TextField label={t('slaCalendar.holidays.name')} autoComplete="off" value={name} onChange={(event) => setName(event.target.value)} />
        <Button type="submit" isLoading={add.isPending}>
          {t('slaCalendar.holidays.add')}
        </Button>
      </form>
      <FormAlert message={add.error ? describeError(add.error) : remove.error ? describeError(remove.error) : null} />
      {holidays.isPending && <LoadingState />}
      {holidays.isError && <ErrorState error={holidays.error} onRetry={() => void holidays.refetch()} />}
      {holidays.data && holidays.data.length === 0 && <p className="mb-0 small text-body-secondary">{t('slaCalendar.holidays.empty')}</p>}
      {holidays.data && holidays.data.length > 0 && (
        <ResponsiveList items={holidays.data} columns={columns} getKey={(h) => h.id} renderCard={renderCard} caption={t('slaCalendar.holidays.caption')} />
      )}
    </PageCard>
  );
}

export function SlaCalendarPage() {
  const { t } = useTranslation('admin');
  const calendar = useSlaCalendar();
  return (
    <section className="d-flex flex-column gap-3">
      <PageHeader icon={ROUTE_META.adminSlaCalendar.icon} titleOverride={t('slaCalendar.title')} />
      {calendar.isPending && <LoadingState />}
      {calendar.isError && <ErrorState error={calendar.error} onRetry={() => void calendar.refetch()} />}
      {calendar.data && <CalendarForm calendar={calendar.data} onReload={async () => (await calendar.refetch()).data} />}
      <HolidaysSection />
    </section>
  );
}
