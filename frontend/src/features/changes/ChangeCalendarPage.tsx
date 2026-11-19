/**
 * @input `view` (`month` or `week`) and `date` (`yyyy-MM-dd`) in the URL query.
 * @output The maintenance calendar: a month grid or a seven-day week on wide screens, a day-by-day agenda on phones.
 * @position `/changes/calendar`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Grid } from '@astryxdesign/core/Grid';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { SegmentedControl, SegmentedControlItem } from '@astryxdesign/core/SegmentedControl';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useLocation, useSearchParams } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDate } from '../../shared/lib/format';
import { TURKISH_LOCALE } from '../../shared/lib/turkish';
import { useIsPhone } from '../../shared/list/useIsPhone';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { changePath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

import { useChangeCalendar, type ChangeCalendarItem } from './api';
import { addDays, eachDay, isIsoDay, monthRange, shiftMonth, todayInCompanyZone, weekRange, windowDays, type DateRange } from './calendarRange';
import { ChangeStatusBadge, RiskBadge } from './ChangeBadges';
import { WindowText } from './WindowText';

const VIEWS = ['month', 'week'] as const;
type CalendarView = (typeof VIEWS)[number];
const DEFAULT_VIEW: CalendarView = 'month';
const VIEW_PARAM = 'view';
const DATE_PARAM = 'date';
const DAYS_PER_WEEK = 7;
const MONTH_KEY_LENGTH = 7;
const SKELETON_ROWS = 6;
const UTC = 'UTC';
const REFERENCE_MONDAY = '2026-01-05';

const monthFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, { month: 'long', year: 'numeric', timeZone: UTC });
const weekdayFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, { weekday: 'short', timeZone: UTC });
const dayNumberFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, { day: 'numeric', timeZone: UTC });
const dayHeadingFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, { weekday: 'long', day: 'numeric', month: 'long', timeZone: UTC });
const MIDNIGHT_UTC = 'T00:00:00Z';

const asDate = (day: string) => new Date(`${day}${MIDNIGHT_UTC}`);
const WEEKDAYS = eachDay(weekRange(REFERENCE_MONDAY)).map((day) => weekdayFormatter.format(asDate(day)));

function ConflictToken() {
  const { t } = useTranslation('changes');
  return <Token size="sm" color="orange" icon={<Icon icon={Icons.warning} />} label={t('calendar.conflict')} description={t('calendar.conflictHint')} />;
}

function EntryLink({ item }: { item: ChangeCalendarItem }) {
  const location = useLocation();
  return (
    <StateLink href={changePath(item.number)} state={linkState(location)}>
      {`${item.number} ${item.title}`}
    </StateLink>
  );
}

/** One change inside a day cell; `detailed` adds status, risk and the window (agenda and week). */
function CalendarEntry({ item, detailed }: { item: ChangeCalendarItem; detailed: boolean }) {
  return (
    <VStack gap={1}>
      <Text type="label" wordBreak="break-word">
        <EntryLink item={item} />
      </Text>
      {detailed && <WindowText start={item.windowStart} end={item.windowEnd} />}
      <HStack gap={1} vAlign="center" wrap="wrap">
        {detailed && <ChangeStatusBadge status={item.status} />}
        {detailed && <RiskBadge risk={item.risk} />}
        {item.hasConflict && <ConflictToken />}
      </HStack>
    </VStack>
  );
}

function DayCell({ day, items, detailed, muted }: { day: string; items: ChangeCalendarItem[]; detailed: boolean; muted: boolean }) {
  return (
    <Card padding={2}>
      <VStack gap={2}>
        <Text type={muted ? 'supporting' : 'label'}>{detailed ? `${weekdayFormatter.format(asDate(day))} ${dayNumberFormatter.format(asDate(day))}` : dayNumberFormatter.format(asDate(day))}</Text>
        {items.map((item) => (
          <CalendarEntry key={item.number} item={item} detailed={detailed} />
        ))}
      </VStack>
    </Card>
  );
}

function DayGrid({ days, byDay, view, month }: { days: string[]; byDay: Map<string, ChangeCalendarItem[]>; view: CalendarView; month: string }) {
  const { t } = useTranslation('changes');
  const isMonth = view === 'month';
  return (
    <VStack gap={2} role="group" aria-label={t('calendar.gridCaption')}>
      {isMonth && (
        <Grid columns={DAYS_PER_WEEK} gap={2}>
          {WEEKDAYS.map((name) => (
            <Text key={name} type="supporting">
              {name}
            </Text>
          ))}
        </Grid>
      )}
      <Grid columns={DAYS_PER_WEEK} gap={2}>
        {days.map((day) => (
          <DayCell key={day} day={day} items={byDay.get(day) ?? []} detailed={!isMonth} muted={isMonth && !day.startsWith(month)} />
        ))}
      </Grid>
    </VStack>
  );
}

function Agenda({ days, byDay }: { days: string[]; byDay: Map<string, ChangeCalendarItem[]> }) {
  const { t } = useTranslation('changes');
  return (
    <VStack gap={4} role="group" aria-label={t('calendar.agendaCaption')}>
      {days.map((day) => (
        <VStack key={day} gap={2}>
          <Heading level={2}>{dayHeadingFormatter.format(asDate(day))}</Heading>
          {(byDay.get(day) ?? []).map((item) => (
            <CalendarEntry key={item.number} item={item} detailed />
          ))}
        </VStack>
      ))}
    </VStack>
  );
}

function groupByDay(items: ChangeCalendarItem[]): Map<string, ChangeCalendarItem[]> {
  const byDay = new Map<string, ChangeCalendarItem[]>();
  for (const item of items) {
    for (const day of windowDays(item.windowStart, item.windowEnd)) {
      byDay.set(day, [...(byDay.get(day) ?? []), item]);
    }
  }
  return byDay;
}

const readView = (value: string | null): CalendarView => VIEWS.find((view) => view === value) ?? DEFAULT_VIEW;

export function ChangeCalendarPage() {
  const { t } = useTranslation('changes');
  const isPhone = useIsPhone();
  const [params, setParams] = useSearchParams();
  const view = readView(params.get(VIEW_PARAM));
  const rawDate = params.get(DATE_PARAM);
  const date = rawDate !== null && isIsoDay(rawDate) ? rawDate : todayInCompanyZone();
  const month = date.slice(0, MONTH_KEY_LENGTH);
  const range: DateRange = view === 'month' ? monthRange(month) : weekRange(date);
  const calendar = useChangeCalendar(range.from, range.to);

  const update = (next: { view?: CalendarView; date?: string }) => {
    const nextParams = new URLSearchParams(params);
    nextParams.set(DATE_PARAM, next.date ?? date);
    const nextView = next.view ?? view;
    if (nextView === DEFAULT_VIEW) {
      nextParams.delete(VIEW_PARAM);
    } else {
      nextParams.set(VIEW_PARAM, nextView);
    }
    setParams(nextParams);
  };
  const step = (direction: 1 | -1) => update({ date: view === 'month' ? shiftMonth(date, direction) : addDays(date, direction * DAYS_PER_WEEK) });

  const byDay = groupByDay(calendar.data ?? []);
  const days = eachDay(range);
  const agendaDays = days.filter((day) => byDay.has(day) && (view === 'week' || day.startsWith(month)));
  const isEmpty = isPhone ? agendaDays.length === 0 : (calendar.data?.length ?? 0) === 0;
  const title = view === 'month' ? monthFormatter.format(asDate(`${month}-01`)) : `${formatDate(range.from)} – ${formatDate(range.to)}`;

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.changeCalendar.icon} />
      <HStack gap={3} vAlign="center" wrap="wrap">
        <HStack gap={2} vAlign="center" wrap="wrap">
          <Button label={t('calendar.previous')} variant="secondary" onClick={() => step(-1)} />
          <Button label={t('calendar.today')} variant="secondary" onClick={() => update({ date: todayInCompanyZone() })} />
          <Button label={t('calendar.next')} variant="secondary" onClick={() => step(1)} />
        </HStack>
        <StackItem size="fill">
          <Heading level={2}>{title}</Heading>
        </StackItem>
        <SegmentedControl label={t('calendar.viewLabel')} value={view} onChange={(value) => update({ view: readView(value) })}>
          {VIEWS.map((value) => (
            <SegmentedControlItem key={value} value={value} label={t(`calendar.views.${value}`)} />
          ))}
        </SegmentedControl>
      </HStack>
      {calendar.isPending && <LoadingState rows={SKELETON_ROWS} />}
      {calendar.isError && <ErrorState error={calendar.error} onRetry={() => void calendar.refetch()} />}
      {calendar.data && isEmpty && <EmptyState message={t(view === 'month' ? 'calendar.emptyMonth' : 'calendar.emptyWeek')} />}
      {calendar.data && !isEmpty && (isPhone ? <Agenda days={agendaDays} byDay={byDay} /> : <DayGrid days={days} byDay={byDay} view={view} month={month} />)}
    </VStack>
  );
}
