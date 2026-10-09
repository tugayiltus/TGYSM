import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { articlePath } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { Dialog } from '../../shared/ui/Dialog';
import { FormAlert } from '../../shared/ui/FormAlert';
import { PageCard } from '../../shared/ui/PageCard';
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
      <div className="d-flex flex-column gap-3">
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
    <PageCard title={t('ticket.title')} bodyClassName="d-flex flex-column gap-2">
      <FormAlert message={unlink.error ? describeError(unlink.error) : null} />
      {items.length === 0 && <p className="mb-0 text-body-secondary">{t('ticket.empty')}</p>}
      <ul className="list-unstyled mb-0 d-flex flex-column gap-2">
        {items.map((a) => (
          <li key={a.articleId} className="d-flex align-items-center justify-content-between gap-2 rounded border bg-body-tertiary p-2">
            <span className="d-flex min-w-0 flex-column">
              <Link to={articlePath(a.articleId)} className="link-primary text-decoration-underline text-break">
                {a.title}
              </Link>
              {a.visibility === 'internal' && <span className="small text-body-secondary">{t('ticket.notShown')}</span>}
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
        <Button variant="secondary" className="align-self-start" onClick={() => setLinking(true)}>
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
    </PageCard>
  );
}
