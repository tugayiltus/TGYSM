import { CCard, CCardBody } from '@coreui/react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useParams } from 'react-router';

import { formatDateTime } from '../../shared/lib/format';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { useReturnTo } from '../../shared/navigation/returnTo';
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

function Field({ label, value, wide = false }: { label: string; value: string | null; wide?: boolean }) {
  const { t: tc } = useTranslation();
  return (
    <div className={wide ? 'col-12' : 'col-12 col-sm-6 col-lg-12'}>
      <dt className="small fw-normal text-body-secondary">{label}</dt>
      <dd className="mb-0 fw-medium text-break tgysm-pre-wrap">{value ?? tc('none')}</dd>
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
    <div className="alert alert-warning mb-0 small">
      <p className="fw-semibold mb-1">{`${t('detail.approval')}: ${t(`approvalStatuses.${approval.status}`)}`}</p>
      <p className="mb-0">{approval.approverName ? t('detail.approver', { name: approval.approverName }) : t('detail.noApprover')}</p>
      {approval.reason && <p className="mb-0 mt-1">{approval.reason}</p>}
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
  const backTo = useReturnTo(ROUTES.tickets);
  // Only from loaded data: a 404 must not show the number typed into the URL as a title.
  useBreadcrumbLabel('ticketDetail', ticket.data?.number);

  if (ticket.isPending) {
    return <LoadingState />;
  }
  if (ticket.isError) {
    return <ErrorState error={ticket.error} onRetry={() => void ticket.refetch()} />;
  }
  const item = ticket.data;

  return (
    <>
      <PageHeader
        titleOverride={item.subject}
        actions={
          <Link to={backTo} className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center">
            {t('detail.back')}
          </Link>
        }
      />
      <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
        <span className="small text-body-secondary">{`${item.number} · ${t(`kinds.${item.kind}`)}`}</span>
        <TicketStatusBadge status={item.status} />
        <PriorityBadge priority={item.priority} />
        {item.holdReason && <span className="small">{t(`holdReasons.${item.holdReason}`)}</span>}
      </div>

      <div className="row g-4">
        {/* First in the DOM: on phones the actions come before the long content; beside it from 992 px. */}
        <aside className="col-12 col-lg-4 order-lg-2">
          <div className="tgysm-sticky-aside d-flex flex-column gap-3">
            <TicketActions ticket={item} />
            <ApprovalBox ticket={item} />
            <CCard>
              <CCardBody className="d-flex flex-column gap-2">
                <h2 id="ticket-sla" className="h6 mb-0">
                  {t('sla.title')}
                </h2>
                <SlaSummary sla={item.sla} showDue />
              </CCardBody>
            </CCard>
            <CCard>
              <CCardBody>
                <dl className="row g-2 mb-0">
                  <Field label={t('detail.requester')} value={item.requesterName} />
                  <Field label={t('detail.team')} value={item.teamName} />
                  <Field label={t('detail.assignee')} value={item.assigneeName} />
                  <Field label={t('detail.category')} value={item.categoryName} />
                  <Field label={t('detail.location')} value={item.locationName} />
                  {item.kind === 'request' && <Field label={t('detail.requestType')} value={item.requestTypeName} />}
                  <Field label={t('detail.createdAt')} value={formatDateTime(item.createdAt)} />
                </dl>
              </CCardBody>
            </CCard>
          </div>
        </aside>

        <div className="col-12 col-lg-8 order-lg-1 d-flex flex-column gap-4 min-w-0">
          <FormAlert message={uploadError} />
          <CCard>
            <CCardBody>
              <dl className="row g-3 mb-0">
                <Field wide label={t('detail.description')} value={item.description} />
                {item.holdNote && <Field wide label={t('detail.holdNote')} value={item.holdNote} />}
                {item.resolution && <Field wide label={t('detail.resolution')} value={item.resolution} />}
                {item.rootCause && <Field wide label={t('detail.rootCause')} value={item.rootCause} />}
                {item.cancelReason && <Field wide label={t('detail.cancelReason')} value={item.cancelReason} />}
              </dl>
            </CCardBody>
          </CCard>
          <TicketLinks ticket={item} isRequester={item.requesterId === me.id} />
          <TicketArticles ticket={item} />
          <div className="d-flex flex-column gap-3">
            {comments.isError && <ErrorState error={comments.error} onRetry={() => void comments.refetch()} />}
            {comments.data && <CommentList ticket={item} comments={comments.data} />}
            <CommentBox ticket={item} />
          </div>
          <section aria-labelledby="ticket-timeline" className="d-flex flex-column gap-2">
            <h2 id="ticket-timeline" className="h5 mb-0">
              {t('detail.timeline')}
            </h2>
            {timeline.isError && <ErrorState error={timeline.error} onRetry={() => void timeline.refetch()} />}
            {timeline.data && <Timeline events={timeline.data} />}
          </section>
        </div>
      </div>
    </>
  );
}
