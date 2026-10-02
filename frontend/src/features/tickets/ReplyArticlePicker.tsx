import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { articlePath } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { SelectField } from '../../shared/ui/SelectField';
import { TextField } from '../../shared/ui/TextField';
import { usePublishedArticleOptions } from '../kb/api';


/** The reply may reach the requester, so only published, employee-visible articles are offered (the server also refuses others). */
function ArticlePanel({ onInsert }: { onInsert: (articleId: string, title: string) => void }) {
  const { t } = useTranslation('kb');
  const [search, setSearch] = useState('');
  const [articleId, setArticleId] = useState('');
  const options = usePublishedArticleOptions(search, 'employees');
  const chosen = options.find((o) => o.value === articleId);

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-slate-200 p-3">
      <TextField type="search" label={t('reply.search')} value={search} onChange={(event) => setSearch(event.target.value)} />
      <SelectField label={t('reply.pick')} placeholder={t('reply.select')} options={options} value={articleId} onChange={(event) => setArticleId(event.target.value)} />
      <Button className="self-start" disabled={chosen === undefined} onClick={() => chosen && onInsert(chosen.value, chosen.label)}>
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
    <div className="flex flex-col gap-2">
      <Button variant="secondary" className="self-start" aria-expanded={open} onClick={() => setOpen((current) => !current)}>
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
