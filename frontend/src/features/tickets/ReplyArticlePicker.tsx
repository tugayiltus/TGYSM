/**
 * @input A callback that receives the text to add to the reply.
 * @output A "Makale ekle" button that opens a search for published articles and adds the chosen one as a link.
 * @position Comment reply box on the ticket detail page.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Selector } from '@astryxdesign/core/Selector';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { useState, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { articlePath } from '../../shared/routes';
import { usePublishedArticleOptions } from '../kb/api';

const ENTER_KEY = 'Enter';

/** The panel sits inside the comment form: Enter in its search must not send the (possibly public) reply. */
function keepEnterFromSubmitting(event: KeyboardEvent<HTMLInputElement>) {
  if (event.key === ENTER_KEY) {
    event.preventDefault();
  }
}

/** The reply may reach the requester, so only published, employee-visible articles are offered (the server also refuses others). */
function ArticlePanel({ onInsert }: { onInsert: (articleId: string, title: string) => void }) {
  const { t } = useTranslation('kb');
  const [search, setSearch] = useState('');
  const [articleId, setArticleId] = useState('');
  const options = usePublishedArticleOptions(search, 'employees');
  const chosen = options.find((option) => option.value === articleId);

  return (
    <Card padding={3} variant="muted">
      <VStack gap={3}>
        <TextInput type="search" label={t('reply.search')} value={search} onKeyDown={keepEnterFromSubmitting} onChange={setSearch} />
        <Selector label={t('reply.pick')} placeholder={t('reply.select')} options={[...options]} value={articleId} onChange={setArticleId} />
        <HStack>
          <Button label={t('reply.insert')} variant="primary" isDisabled={chosen === undefined} onClick={() => chosen && onInsert(chosen.value, chosen.label)} />
        </HStack>
      </VStack>
    </Card>
  );
}

/** Appends `Title: /kb/{id}` to the reply text. */
export function ReplyArticlePicker({ onInsert }: { onInsert: (text: string) => void }) {
  const { t } = useTranslation('kb');
  const [open, setOpen] = useState(false);
  return (
    <VStack gap={2}>
      <HStack>
        <Button label={t('reply.add')} variant="secondary" aria-expanded={open} onClick={() => setOpen((current) => !current)} />
      </HStack>
      {open && (
        <ArticlePanel
          onInsert={(id, title) => {
            onInsert(`${title}: ${articlePath(id)}`);
            setOpen(false);
          }}
        />
      )}
    </VStack>
  );
}
