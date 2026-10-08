import { useTranslation } from 'react-i18next';
import { Link, useLocation, useParams } from 'react-router';

import { formatDateTime } from '../../shared/lib/format';
import { ROUTES } from '../../shared/routes';
import { FormAlert } from '../../shared/ui/FormAlert';
import { ErrorState, LoadingState } from '../../shared/ui/States';
import { useSession } from '../auth/session';

import { useComments, useTicket, useTimeline, type TicketDetail } from './api';
import { CommentBox, CommentList } from './Comments';
import { SlaSummary } from './SlaBadge';
import { TicketActions } from './TicketActions';
import { TicketArticles } from './TicketArticles';
import { PriorityBadge, TicketStatusBadge } from './TicketBadges';
import { TicketLinks } from './TicketLinks';
import { Timeline } from './Timeline';

function Field({ label, value }: { label: string; value: string | null }) {
  const { t: tc } = useTranslation();
  return (
    <div>
      <dt className="text-xs text-slate-600">{label}</dt>
      <dd className="font-medium break-words">{value ?? tc('none')}</dd>
    </div>
  );
}

function ApprovalBox({ ticket }: { ticket: TicketDetail }) {
  const { t } = useTranslation('tickets');
  const approval = ticket.approval;
  if (approval === null) {
    return null;
  }
  return (
    <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm">
      <p className="font-medium">{`${t('detail.approval')}: ${t(`approvalStatuses.${approval.status}`)}`}</p>
      <p>{approval.approverName ? t('detail.approver', { name: approval.approverName }) : t('detail.noApprover')}</p>
      {approval.reason && <p>{approval.reason}</p>}
    </div>
  );
}

/** Navigation state from the new-ticket page when the files could not be uploaded. */
export interface TicketDetailState {
  /** Set when the ticket was just created, so the form's leave guard lets this navigation through. */
  created?: boolean;
  uploadError?: string;
}

/** Keyed by number: switching tickets starts with fresh drafts, dialogs and errors. */
export function TicketDetailPage() {
  const { number = '' } = useParams();
  return <TicketDetail key={number} number={number} />;
}

function TicketDetail({ number }: { number: string }) {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  const uploadError = (location.state as TicketDetailState | null)?.uploadError ?? null;
  const me = useSession();
  const ticket = useTicket(number);
  const comments = useComments(number);
  const timeline = useTimeline(number);

  if (ticket.isPending) {
    return <LoadingState />;
  }
  if (ticket.isError) {
    return <ErrorState error={ticket.error} onRetry={() => void ticket.refetch()} />;
  }
  const item = ticket.data;

  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <Link to={ROUTES.tickets} className="text-sm text-brand-900 underline">
          {t('detail.back')}
        </Link>
        <p className="text-sm text-slate-600">{`${item.number} · ${t(`kinds.${item.kind}`)}`}</p>
        <h1 className="text-2xl font-semibold break-words">{item.subject}</h1>
        <div className="flex flex-wrap items-center gap-2">
          <TicketStatusBadge status={item.status} />
          <PriorityBadge priority={item.priority} />
          {item.holdReason && <span className="text-sm text-slate-700">{t(`holdReasons.${item.holdReason}`)}</span>}
        </div>
      </div>

      <section aria-labelledby="ticket-sla" className="flex flex-col gap-2">
        <h2 id="ticket-sla" className="text-lg font-semibold">
          {t('sla.title')}
        </h2>
        <SlaSummary sla={item.sla} showDue />
      </section>

      <FormAlert message={uploadError} />
      <ApprovalBox ticket={item} />

      <dl className="grid gap-4 rounded-2xl border border-slate-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-3">
        <Field label={t('detail.requester')} value={item.requesterName} />
        <Field label={t('detail.team')} value={item.teamName} />
        <Field label={t('detail.assignee')} value={item.assigneeName} />
        <Field label={t('detail.category')} value={item.categoryName} />
        <Field label={t('detail.location')} value={item.locationName} />
        {item.kind === 'request' && <Field label={t('detail.requestType')} value={item.requestTypeName} />}
        <Field label={t('detail.createdAt')} value={formatDateTime(item.createdAt)} />
        <div className="sm:col-span-2 lg:col-span-3">
          <Field label={t('detail.description')} value={item.description} />
        </div>
        {item.holdNote && <Field label={t('detail.holdNote')} value={item.holdNote} />}
        {item.resolution && <Field label={t('detail.resolution')} value={item.resolution} />}
        {item.rootCause && <Field label={t('detail.rootCause')} value={item.rootCause} />}
        {item.cancelReason && <Field label={t('detail.cancelReason')} value={item.cancelReason} />}
      </dl>

      <TicketActions ticket={item} />
      <TicketLinks ticket={item} isRequester={item.requesterId === me.id} />
      <TicketArticles ticket={item} />

      {/* min-w-0: grid items otherwise grow to their content's min width and push the page wider than 360 px. */}
      <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div className="flex min-w-0 flex-col gap-4">
          {comments.isError && <ErrorState error={comments.error} onRetry={() => void comments.refetch()} />}
          {comments.data && <CommentList ticket={item} comments={comments.data} />}
          <CommentBox ticket={item} />
        </div>
        <section aria-labelledby="ticket-timeline" className="flex min-w-0 flex-col gap-3">
          <h2 id="ticket-timeline" className="text-lg font-semibold">
            {t('detail.timeline')}
          </h2>
          {timeline.isError && <ErrorState error={timeline.error} onRetry={() => void timeline.refetch()} />}
          {timeline.data && <Timeline events={timeline.data} />}
        </section>
      </div>
    </section>
  );
}
