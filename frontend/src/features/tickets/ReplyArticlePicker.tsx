import { useState, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { articlePath } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { SelectField } from '../../shared/ui/SelectField';
import { TextField } from '../../shared/ui/TextField';
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
  const chosen = options.find((o) => o.value === articleId);

  return (
    <div className="d-flex flex-column gap-2 rounded border p-2">
      <TextField type="search" label={t('reply.search')} value={search} onKeyDown={keepEnterFromSubmitting} onChange={(event) => setSearch(event.target.value)} />
      <SelectField label={t('reply.pick')} placeholder={t('reply.select')} options={options} value={articleId} onChange={(event) => setArticleId(event.target.value)} />
      <Button className="align-self-start" disabled={chosen === undefined} onClick={() => chosen && onInsert(chosen.value, chosen.label)}>
        {t('reply.insert')}
      </Button>
    </div>
  );
}

/** "Makale ekle": appends `Title: /kb/{id}` to the reply text. */
export function ReplyArticlePicker({ onInsert }: { onInsert: (text: string) => void }) {
  const { t } = useTranslation('kb');
  const [open, setOpen] = useState(false);
  return (
    <div className="d-flex flex-column gap-2">
      <Button variant="secondary" className="align-self-start" aria-expanded={open} onClick={() => setOpen((current) => !current)}>
        {t('reply.add')}
      </Button>
      {open && (
        <ArticlePanel
          onInsert={(id, title) => {
            onInsert(`${title}: ${articlePath(id)}`);
            setOpen(false);
          }}
        />
      )}
    </div>
  );
}
