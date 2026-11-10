/**
 * @input A ticket.
 * @output The knowledge articles linked to it; people working on it can link or unlink one.
 * @position Ticket detail page.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { List, ListItem } from '@astryxdesign/core/List';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { ActionDialog } from '../../shared/feedback/ActionDialog';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { SectionCard } from '../../shared/page/SectionCard';
import { articlePath } from '../../shared/routes';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { useLinkArticle, usePublishedArticleOptions, useTicketArticles, useUnlinkArticle } from '../kb/api';

import type { TicketDetail } from './api';

function LinkArticleDialog({ ticketNumber, onClose, onLinked }: { ticketNumber: string; onClose: () => void; onLinked: () => void }) {
  const { t } = useTranslation('kb');
  const link = useLinkArticle(ticketNumber);
  const [search, setSearch] = useState('');
  const [articleId, setArticleId] = useState('');
  const options = usePublishedArticleOptions(search, '');
  return (
    <ActionDialog
      title={t('ticket.link')}
      isPending={link.isPending}
      isConfirmDisabled={articleId === ''}
      error={link.error}
      onConfirm={() => link.mutate(articleId, { onSuccess: onLinked })}
      onClose={onClose}
    >
      <TextInput type="search" label={t('ticket.search')} value={search} onChange={setSearch} />
      <Selector label={t('ticket.pick')} placeholder={t('ticket.select')} options={[...options]} value={articleId} onChange={setArticleId} />
    </ActionDialog>
  );
}

/** Only people working on the ticket can link or unlink; the server hides internal articles from the requester. */
export function TicketArticles({ ticket }: { ticket: TicketDetail }) {
  const { t } = useTranslation('kb');
  const articles = useTicketArticles(ticket.number);
  const unlink = useUnlinkArticle(ticket.number);
  const [linking, setLinking] = useState(false);
  const open = ticket.status !== 'completed' && ticket.status !== 'cancelled';
  const items = articles.data ?? [];

  return (
    <SectionCard title={t('ticket.title')}>
      <FormAlert message={unlink.error ? describeError(unlink.error) : null} />
      {items.length === 0 ? (
        <Text type="supporting">{t('ticket.empty')}</Text>
      ) : (
        <List hasDividers density="compact" aria-label={t('ticket.title')}>
          {items.map((article) => (
            <ListItem
              key={article.articleId}
              label={<Link href={articlePath(article.articleId)}>{article.title}</Link>}
              description={article.visibility === 'internal' ? t('ticket.notShown') : undefined}
              endContent={
                ticket.canWork &&
                open && <Button label={t('ticket.unlink')} aria-label={recordActionLabel(t('ticket.unlink'), article.title)} variant="ghost" onClick={() => unlink.mutate(article.articleId)} />
              }
            />
          ))}
        </List>
      )}
      {ticket.canWork && open && (
        <HStack>
          <Button label={t('ticket.link')} variant="secondary" onClick={() => setLinking(true)} />
        </HStack>
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
    </SectionCard>
  );
}
