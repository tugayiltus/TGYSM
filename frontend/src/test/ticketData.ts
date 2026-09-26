import type { CommentItem, TicketDetail, TicketListItem, TimelineItem } from '../features/tickets/api';

export const INCIDENT: TicketDetail = {
  id: 't-1',
  number: 'INC-2026-000001',
  kind: 'incident',
  subject: 'Yazıcı çalışmıyor',
  description: 'Kağıt sıkışıyor',
  status: 'open',
  holdReason: null,
  holdNote: null,
  impact: 'medium',
  urgency: 'medium',
  priority: 'p3',
  categoryId: null,
  categoryName: null,
  locationId: null,
  locationName: null,
  requesterId: '00000000-0000-7000-8000-000000000002',
  requesterName: 'Ayşe Kaya',
  teamId: 'team-1',
  teamName: 'Servis Masası',
  assigneeId: null,
  assigneeName: null,
  requestTypeId: null,
  requestTypeName: null,
  requiresApproval: false,
  rootCause: null,
  resolution: null,
  outcome: null,
  cancelReason: null,
  createdAt: '2026-09-26T08:00:00Z',
  updatedAt: '2026-09-26T08:00:00Z',
  completedAt: null,
  approval: null,
  relatedTickets: [],
  canWork: false,
  version: 11,
};

export const LIST_ITEM: TicketListItem = {
  number: INCIDENT.number,
  kind: 'incident',
  subject: INCIDENT.subject,
  status: 'open',
  holdReason: null,
  priority: 'p3',
  teamName: 'Servis Masası',
  assigneeName: null,
  requesterName: 'Ayşe Kaya',
  createdAt: INCIDENT.createdAt,
  updatedAt: INCIDENT.updatedAt,
};

export const TIMELINE: TimelineItem[] = [
  { id: 'e-1', kind: 'created', visibility: 'public', actorName: 'Ayşe Kaya', data: {}, occurredAt: '2026-09-26T08:00:00Z' },
];

export const COMMENTS: CommentItem[] = [
  {
    id: 'c-1',
    body: 'Ekran görüntüsü ekte',
    visibility: 'public',
    authorId: INCIDENT.requesterId,
    authorName: 'Ayşe Kaya',
    createdAt: '2026-09-26T08:05:00Z',
    attachments: [{ id: 'f-1', originalName: 'ekran.png', contentType: 'image/png', sizeBytes: 2048, scanStatus: 'clean' }],
  },
];

export function detailRoutes(ticket: TicketDetail = INCIDENT) {
  const base = `/api/v1/tickets/${ticket.number}`;
  return {
    [`GET ${base}`]: { status: 200, body: ticket },
    [`GET ${base}/comments`]: { status: 200, body: COMMENTS },
    [`GET ${base}/timeline`]: { status: 200, body: TIMELINE },
    [`GET ${base}/assets`]: { status: 200, body: [] },
  };
}
