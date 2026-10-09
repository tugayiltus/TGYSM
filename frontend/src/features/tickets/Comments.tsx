import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextAreaField } from '../../shared/ui/TextAreaField';

import { useAddComment, type CommentItem, type TicketDetail, type Visibility } from './api';
import { AttachmentInput } from './AttachmentInput';
import { describeCommentError } from './commentErrors';
import { ReplyArticlePicker } from './ReplyArticlePicker';

const ATTACHMENTS_PATH = 'attachments';

const attachmentUrl = (number: string, id: string) => `/api/v1/tickets/${number}/${ATTACHMENTS_PATH}/${id}`;

export function CommentList({ ticket, comments }: { ticket: TicketDetail; comments: readonly CommentItem[] }) {
  const { t } = useTranslation('tickets');
  if (comments.length === 0) {
    return <p className="mb-0 text-body-secondary">{t('detail.noComments')}</p>;
  }
  return (
    <ul className="list-unstyled mb-0 d-flex flex-column gap-2">
      {comments.map((c) => (
        <li key={c.id} className={`rounded border p-3 ${c.visibility === 'internal' ? 'border-warning-subtle bg-warning-subtle' : 'bg-body-tertiary'}`}>
          <p className="mb-0 d-flex flex-wrap align-items-center gap-2 small">
            <span className="fw-medium">{c.authorName}</span>
            <time className="text-body-secondary" dateTime={c.createdAt}>
              {formatDateTime(c.createdAt)}
            </time>
            {c.visibility === 'internal' && <span className="badge bg-warning-subtle text-warning-emphasis">{t('detail.internal')}</span>}
          </p>
          {c.body && <p className="mb-0 mt-1 tgysm-pre-wrap text-break">{c.body}</p>}
          {c.attachments.length > 0 && (
            <ul className="list-unstyled mb-0 mt-2 d-flex flex-wrap gap-2">
              {c.attachments.map((a) => (
                <li key={a.id}>
                  <a href={attachmentUrl(ticket.number, a.id)} className="small link-primary text-decoration-underline" download>
                    {a.originalName}
                  </a>
                </li>
              ))}
            </ul>
          )}
        </li>
      ))}
    </ul>
  );
}

/** Reply box; people working on the ticket also get an internal-note tab (spec §3.7). */
export function CommentBox({ ticket }: { ticket: TicketDetail }) {
  const { t } = useTranslation('tickets');
  const add = useAddComment();
  const [visibility, setVisibility] = useState<Visibility>('public');
  const [body, setBody] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [formKey, setFormKey] = useState(0);

  const tabs: Visibility[] = ['public', 'internal'];
  const submit = () =>
    add.mutate(
      { number: ticket.number, body, visibility, files },
      {
        onSuccess: () => {
          setBody('');
          setFiles([]);
          setFormKey((key) => key + 1);
        },
      },
    );

  return (
    // Inside the "Yazışmalar" card: the form keeps that name for screen readers and tests.
    <form
      aria-label={t('detail.comments')}
      className="d-flex flex-column gap-2 border-top pt-3"
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      {ticket.canWork && (
        <div role="tablist" aria-label={t('comment.tabs')} className="d-flex gap-2">
          {tabs.map((tab) => (
            <button
              key={tab}
              type="button"
              role="tab"
              aria-selected={visibility === tab}
              disabled={add.isPending}
              className="btn btn-sm btn-ghost-secondary tgysm-touch aria-selected-active"
              onClick={() => setVisibility(tab)}
            >
              {t(`comment.${tab}`)}
            </button>
          ))}
        </div>
      )}
      <FormAlert message={add.error ? describeCommentError(add.error) : null} />
      {/* Locked while sending: the success handler clears the box, which would drop anything typed meanwhile. */}
      <TextAreaField label={t('comment.body')} value={body} disabled={add.isPending} onChange={(event) => setBody(event.target.value)} />
      <ReplyArticlePicker onInsert={(text) => setBody((current) => (current.trim() === '' ? text : `${current}\n${text}`))} />
      <AttachmentInput key={formKey} label={t('comment.files')} hint={t('new.filesHint')} files={files} onChange={setFiles} />
      <Button type="submit" className="align-self-start" disabled={body.trim() === '' && files.length === 0} isLoading={add.isPending}>
        {t('comment.send')}
      </Button>
    </form>
  );
}
