import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router';

import { describeError, translateErrorCode } from '../../shared/api/errorMessage';
import { ApiError, ERROR_CODES } from '../../shared/api/errors';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { articlePath } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { FormSection } from '../../shared/ui/FormSection';
import { SelectField } from '../../shared/ui/SelectField';
import { ErrorState, LoadingState } from '../../shared/ui/States';
import { TextAreaField } from '../../shared/ui/TextAreaField';
import { TextField } from '../../shared/ui/TextField';
import { UnsavedChangesGuard } from '../../shared/ui/UnsavedChangesGuard';

import {
  ARTICLE_LIMITS,
  ARTICLE_VISIBILITIES,
  useArticle,
  useArticleCategories,
  useCreateArticle,
  useUpdateArticle,
  type ArticleDetail,
  type ArticleInput,
  type ArticleVisibility,
} from './api';
import { ArticleMarkdown } from './ArticleMarkdown';

const BODY_ROWS = 16;
const SUMMARY_ROWS = 3;
const FIELDS = ['title', 'summary', 'body'] as const;
type Field = (typeof FIELDS)[number];
type Problems = Partial<Record<Field, string>>;

/** The working copy. `baseVersion` is the server version it was loaded from; a save always sends that one. */
interface Draft {
  baseVersion: number;
  title: string;
  summary: string;
  body: string;
  categoryId: string;
  visibility: ArticleVisibility;
}

const EMPTY_DRAFT: Draft = { baseVersion: 0, title: '', summary: '', body: '', categoryId: '', visibility: 'employees' };

const toDraft = (article: ArticleDetail): Draft => ({
  baseVersion: article.version,
  title: article.title,
  summary: article.summary,
  body: article.body,
  categoryId: article.categoryId ?? '',
  visibility: article.visibility,
});

/** Required fields and the server's length limits, checked before any request. */
function validate(draft: Draft): Problems {
  const problems: Problems = {};
  for (const field of FIELDS) {
    const value = draft[field].trim();
    if (field !== 'summary' && value === '') {
      problems[field] = ERROR_CODES.required;
    } else if (draft[field].length > ARTICLE_LIMITS[field]) {
      problems[field] = ERROR_CODES.tooLong;
    }
  }
  return problems;
}

function toInput(draft: Draft): ArticleInput {
  return {
    title: draft.title.trim(),
    summary: draft.summary.trim(),
    body: draft.body,
    categoryId: draft.categoryId === '' ? null : draft.categoryId,
    visibility: draft.visibility,
  };
}

const isConflict = (error: unknown) => error instanceof ApiError && error.code === ERROR_CODES.concurrencyConflict;

function serverFieldProblems(error: unknown): Problems {
  if (!(error instanceof ApiError)) {
    return {};
  }
  const problems: Problems = {};
  for (const field of FIELDS) {
    const code = error.fieldErrors[field]?.[0];
    if (code !== undefined) {
      problems[field] = code;
    }
  }
  return problems;
}

/** Navigation state after a save, so the unsaved-changes guard lets it through. */
const SAVED = { saved: true } as const;

interface EditorFormProps {
  initial: Draft;
  isPending: boolean;
  error: unknown;
  onSubmit: (draft: Draft) => void;
  onReload?: () => Promise<ArticleDetail | undefined>;
  heading: string;
}

function EditorForm({ initial, isPending, error, onSubmit, onReload, heading }: EditorFormProps) {
  const { t } = useTranslation('kb');
  const categories = useArticleCategories();
  const [draft, setDraft] = useState<Draft>(initial);
  const [problems, setProblems] = useState<Problems>({});
  const [tab, setTab] = useState<'write' | 'preview'>('write');

  const change = (patch: Partial<Draft>) => {
    setDraft((current) => ({ ...current, ...patch }));
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const found = validate(draft);
    setProblems(found);
    if (Object.keys(found).length === 0) {
      onSubmit(draft);
    }
  };

  const reload = async () => {
    const fresh = await onReload?.();
    if (fresh) {
      setDraft(toDraft(fresh));
      setProblems({});
    }
  };

  const shown: Problems = { ...serverFieldProblems(error), ...problems };
  const fieldMessage = (field: Field) => (shown[field] === undefined ? undefined : translateErrorCode(shown[field]));
  const alert = error && Object.keys(serverFieldProblems(error)).length === 0 ? describeError(error) : null;

  const dirty = (Object.keys(initial) as (keyof Draft)[]).some((key) => draft[key] !== initial[key]);

  return (
    <>
    <PageHeader icon={ROUTE_META.kbList.icon} titleOverride={heading} />
    {/* A saved article navigates with state.saved; only other ways out ask about unsaved changes. */}
    <UnsavedChangesGuard when={({ nextLocation }) => dirty && (nextLocation.state as { saved?: boolean } | null)?.saved !== true} />
    <form noValidate className="tgysm-editor" onSubmit={submit}>
      <FormSection
        actions={
          <Button type="submit" isLoading={isPending}>
            {t('editor.save')}
          </Button>
        }
      >
        <div className="col-12">
          <FormAlert message={alert} />
        </div>
        {isConflict(error) && onReload && (
          <div className="col-12">
            <Button variant="secondary" onClick={() => void reload()}>
              {t('editor.reload')}
            </Button>
          </div>
        )}
        <TextField className="col-12" label={t('editor.title')} autoComplete="off" value={draft.title} error={fieldMessage('title')} onChange={(event) => change({ title: event.target.value })} />
        <TextAreaField className="col-12" label={t('editor.summary')} rows={SUMMARY_ROWS} value={draft.summary} error={fieldMessage('summary')} onChange={(event) => change({ summary: event.target.value })} />
        <SelectField
          className="col-12 col-sm-6"
          label={t('editor.category')}
          placeholder={t('editor.noCategory')}
          options={(categories.data ?? []).map((c) => ({ value: c.id, label: c.path }))}
          value={draft.categoryId}
          onChange={(event) => change({ categoryId: event.target.value })}
        />
        <SelectField
          className="col-12 col-sm-6"
          label={t('editor.visibility')}
          options={ARTICLE_VISIBILITIES.map((v) => ({ value: v, label: t(`visibility.${v}`) }))}
          value={draft.visibility}
          onChange={(event) => change({ visibility: event.target.value as ArticleVisibility })}
        />
        <div className="col-12">
          {/* Two toggle buttons, not a tablist: a tab pattern would also need arrow-key focus and tabpanel wiring. */}
          <div role="group" aria-label={t('editor.tabs')} className="btn-group">
            {(['write', 'preview'] as const).map((name) => (
              <button
                key={name}
                type="button"
                aria-pressed={tab === name}
                className={`btn tgysm-touch ${tab === name ? 'btn-primary' : 'btn-outline-primary'}`}
                onClick={() => setTab(name)}
              >
                {t(`editor.${name}`)}
              </button>
            ))}
          </div>
        </div>
        {tab === 'write' ? (
          <TextAreaField
            className="col-12"
            label={t('editor.body')}
            hint={t('editor.bodyHint')}
            rows={BODY_ROWS}
            value={draft.body}
            error={fieldMessage('body')}
            onChange={(event) => change({ body: event.target.value })}
          />
        ) : (
          <div className="col-12">
            <div className="border rounded bg-body-tertiary p-3 tgysm-preview">
              {draft.body.trim() === '' ? <p className="mb-0 text-body-secondary">{t('editor.previewEmpty')}</p> : <ArticleMarkdown body={draft.body} />}
            </div>
          </div>
        )}
      </FormSection>
    </form>
    </>
  );
}

export function ArticleCreatePage() {
  const { t } = useTranslation('kb');
  const navigate = useNavigate();
  const create = useCreateArticle();
  return (
    <EditorForm
      initial={EMPTY_DRAFT}
      heading={t('editor.createTitle')}
      isPending={create.isPending}
      error={create.error}
      onSubmit={(draft) => create.mutate(toInput(draft), { onSuccess: (article) => void navigate(articlePath(article.id), { state: SAVED }) })}
    />
  );
}

function EditLoaded({ article, onReload }: { article: ArticleDetail; onReload: () => Promise<ArticleDetail | undefined> }) {
  const { t } = useTranslation('kb');
  const navigate = useNavigate();
  const update = useUpdateArticle(article.id);
  useBreadcrumbLabel('kbArticle', article.title);
  return (
    <EditorForm
      initial={toDraft(article)}
      heading={t('editor.editTitle')}
      isPending={update.isPending}
      error={update.error}
      onReload={async () => {
        update.reset();
        return onReload();
      }}
      onSubmit={(draft) => update.mutate({ ...toInput(draft), version: draft.baseVersion }, { onSuccess: () => void navigate(articlePath(article.id), { state: SAVED }) })}
    />
  );
}

export function ArticleEditPage() {
  const { id = '' } = useParams();
  const article = useArticle(id);
  if (article.isPending) {
    return <LoadingState />;
  }
  if (article.isError) {
    return <ErrorState error={article.error} onRetry={() => void article.refetch()} />;
  }
  return <EditLoaded article={article.data} onReload={async () => (await article.refetch()).data} />;
}
