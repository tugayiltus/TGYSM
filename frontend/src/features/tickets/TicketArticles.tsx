import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { articlePath } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { Dialog } from '../../shared/ui/Dialog';
import { FormAlert } from '../../shared/ui/FormAlert';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { SelectField } from '../../shared/ui/SelectField';
import { TextField } from '../../shared/ui/TextField';
import { useLinkArticle, usePublishedArticleOptions, useTicketArticles, useUnlinkArticle } from '../kb/api';

import type { TicketDetail } from './api';

function LinkArticleDialog({ ticketNumber, onClose, onLinked }: { ticketNumber: string; onClose: () => void; onLinked: () => void }) {
  const { t } = useTranslation('kb');
  const { t: tc } = useTranslation();
  const link = useLinkArticle(ticketNumber);
  const [search, setSearch] = useState('');
  const [articleId, setArticleId] = useState('');
  const options = usePublishedArticleOptions(search, '');
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t('ticket.link')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button disabled={articleId === ''} isLoading={link.isPending} onClick={() => link.mutate(articleId, { onSuccess: onLinked })}>
            {tc('actions.confirm')}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <FormAlert message={link.error ? describeError(link.error) : null} />
        <TextField type="search" label={t('ticket.search')} value={search} onChange={(event) => setSearch(event.target.value)} />
        <SelectField label={t('ticket.pick')} placeholder={t('ticket.select')} options={options} value={articleId} onChange={(event) => setArticleId(event.target.value)} />
      </div>
    </Dialog>
  );
}

/** Knowledge articles linked to the ticket; only people working on it can link or unlink (the server hides internal ones from the requester). */
export function TicketArticles({ ticket }: { ticket: TicketDetail }) {
  const { t } = useTranslation('kb');
  const articles = useTicketArticles(ticket.number);
  const unlink = useUnlinkArticle(ticket.number);
  const [linking, setLinking] = useState(false);
  const open = ticket.status !== 'completed' && ticket.status !== 'cancelled';
  const items = articles.data ?? [];

  return (
    <section aria-labelledby="ticket-articles" className="flex flex-col gap-2">
      <h2 id="ticket-articles" className="text-lg font-semibold">
        {t('ticket.title')}
      </h2>
      <FormAlert message={unlink.error ? describeError(unlink.error) : null} />
      {items.length === 0 && <p className="text-slate-600">{t('ticket.empty')}</p>}
      <ul className="flex flex-col gap-2">
        {items.map((a) => (
          <li key={a.articleId} className="flex items-center justify-between gap-2 rounded-lg border border-slate-200 bg-white p-2">
            <span className="flex min-w-0 flex-col">
              <Link to={articlePath(a.articleId)} className="text-brand-900 underline break-words">
                {a.title}
              </Link>
              {a.visibility === 'internal' && <span className="text-xs text-slate-600">{t('ticket.notShown')}</span>}
            </span>
            {ticket.canWork && open && (
              <Button variant="ghost" aria-label={recordActionLabel(t('ticket.unlink'), a.title)} onClick={() => unlink.mutate(a.articleId)}>
                {t('ticket.unlink')}
              </Button>
            )}
          </li>
        ))}
      </ul>
      {ticket.canWork && open && (
        <Button variant="secondary" className="self-start" onClick={() => setLinking(true)}>
          {t('ticket.link')}
        </Button>
      )}
      {linking && (
        <LinkArticleDialog
          ticketNumber={ticket.number}
          onClose={() => setLinking(false)}
          onLinked={() => {
            // The list has changed since the failed unlink, so its message no longer describes what is shown.
            unlink.reset();
            setLinking(false);
          }}
        />
      )}
    </section>
  );
}
