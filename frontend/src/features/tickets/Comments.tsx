import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { ApiError } from '../../shared/api/errors';
import { formatDateTime } from '../../shared/lib/format';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextAreaField } from '../../shared/ui/TextAreaField';

import { useAddComment, type CommentItem, type TicketDetail, type Visibility } from './api';
import { AttachmentInput } from './AttachmentInput';

const FILES_FIELD = 'files';
const BODY_FIELD = 'body';
const ATTACHMENTS_PATH = 'attachments';

const attachmentUrl = (number: string, id: string) => `/api/v1/tickets/${number}/${ATTACHMENTS_PATH}/${id}`;

/** File errors arrive as a field error on "files"; body errors on "body". */
function describeCommentError(error: unknown): string {
  if (error instanceof ApiError) {
    const code = error.fieldErrors[FILES_FIELD]?.[0] ?? error.fieldErrors[BODY_FIELD]?.[0];
    if (code !== undefined) {
      return describeError(new ApiError(error.status, code));
    }
  }
  return describeError(error);
}

export function CommentList({ ticket, comments }: { ticket: TicketDetail; comments: readonly CommentItem[] }) {
  const { t } = useTranslation('tickets');
  if (comments.length === 0) {
    return <p className="text-slate-600">{t('detail.noComments')}</p>;
  }
  return (
    <ul className="flex flex-col gap-3">
      {comments.map((c) => (
        <li key={c.id} className={`rounded-xl border p-3 ${c.visibility === 'internal' ? 'border-amber-200 bg-amber-50' : 'border-slate-200 bg-white'}`}>
          <p className="flex flex-wrap items-center gap-2 text-sm">
            <span className="font-medium">{c.authorName}</span>
            <time className="text-slate-600" dateTime={c.createdAt}>
              {formatDateTime(c.createdAt)}
            </time>
            {c.visibility === 'internal' && <span className="rounded bg-amber-200 px-1.5 text-xs text-amber-900">{t('detail.internal')}</span>}
          </p>
          {c.body && <p className="mt-1 whitespace-pre-wrap break-words">{c.body}</p>}
          {c.attachments.length > 0 && (
            <ul className="mt-2 flex flex-wrap gap-2">
              {c.attachments.map((a) => (
                <li key={a.id}>
                  <a href={attachmentUrl(ticket.number, a.id)} className="text-sm text-brand-900 underline" download>
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
  const titleId = useId();
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
    <form
      aria-labelledby={titleId}
      className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4"
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      <h2 id={titleId} className="text-lg font-semibold">
        {t('detail.comments')}
      </h2>
      {ticket.canWork && (
        <div role="tablist" aria-label={t('comment.tabs')} className="flex gap-2">
          {tabs.map((tab) => (
            <button
              key={tab}
              type="button"
              role="tab"
              aria-selected={visibility === tab}
              className="min-h-11 rounded-lg px-3 text-sm font-medium aria-selected:bg-brand-50 aria-selected:text-brand-900"
              onClick={() => setVisibility(tab)}
            >
              {t(`comment.${tab}`)}
            </button>
          ))}
        </div>
      )}
      <FormAlert message={add.error ? describeCommentError(add.error) : null} />
      <TextAreaField label={t('comment.body')} value={body} onChange={(event) => setBody(event.target.value)} />
      <AttachmentInput key={formKey} label={t('comment.files')} hint={t('new.filesHint')} files={files} onChange={setFiles} />
      <Button type="submit" className="self-start" disabled={body.trim() === '' && files.length === 0} isLoading={add.isPending}>
        {t('comment.send')}
      </Button>
    </form>
  );
}
