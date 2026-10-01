/** Mirrors backend `NotificationEventTypes`; `admin.json` `emailEvents` carries one Turkish label per entry. */
export const EMAIL_EVENT_TYPES = [
  'ticket_created',
  'ticket_assigned',
  'public_reply',
  'approval_requested',
  'approval_decided',
  'ticket_completed',
  'sla_warning',
  'sla_breached',
  'account_activation',
  'password_reset',
] as const;

export type EmailEventType = (typeof EMAIL_EVENT_TYPES)[number];

export const isEmailEventType = (value: string): value is EmailEventType => (EMAIL_EVENT_TYPES as readonly string[]).includes(value);
