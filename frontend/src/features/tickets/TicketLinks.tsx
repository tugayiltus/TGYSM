import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { ticketPath } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { Dialog } from '../../shared/ui/Dialog';
import { FormAlert } from '../../shared/ui/FormAlert';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { SelectField } from '../../shared/ui/SelectField';
import { TextField } from '../../shared/ui/TextField';
import { useAssets, useMyAssets } from '../assets/api';

import { useCreateRelatedRequest, useLinkAsset, useReferenceRequestTypes, useTicketAssets, useUnlinkAsset, type TicketDetail } from './api';
import { TicketStatusBadge } from './TicketBadges';

const SEARCH_DEBOUNCE_MS = 300;
const ASSET_SEARCH_PAGE_SIZE = 20;
const FIRST_PAGE = 1;

function AssetPicker({ ticket, value, onChange }: { ticket: TicketDetail; value: string; onChange: (id: string) => void }) {
  const { t } = useTranslation('tickets');
  const [search, setSearch] = useState('');
  const debounced = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  // Workers pick from the inventory; the requester only from devices assigned to them.
  const inventory = useAssets({ search: debounced, type: undefined, status: undefined, page: FIRST_PAGE, pageSize: ASSET_SEARCH_PAGE_SIZE }, ticket.canWork);
  const mine = useMyAssets(!ticket.canWork);
  const options = useMemo(
    () =>
      ticket.canWork
        ? (inventory.data?.items ?? []).map((a) => ({ value: a.id, label: `${a.assetTag} · ${a.brand} ${a.model}` }))
        : (mine.data ?? []).map((a) => ({ value: a.id, label: `${a.assetTag} · ${a.brand} ${a.model}` })),
    [ticket.canWork, inventory.data, mine.data],
  );
  return (
    <>
      {ticket.canWork && <TextField type="search" label={t('fields.assetSearch')} value={search} onChange={(event) => setSearch(event.target.value)} />}
      <SelectField label={t('fields.asset')} placeholder={t('fields.select')} options={options} value={value} onChange={(event) => onChange(event.target.value)} />
    </>
  );
}

function LinkAssetDialog({ ticket, onClose }: { ticket: TicketDetail; onClose: () => void }) {
  const { t } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const link = useLinkAsset(ticket.number);
  const [assetId, setAssetId] = useState('');
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t('actions.linkAsset')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button disabled={assetId === ''} isLoading={link.isPending} onClick={() => link.mutate(assetId, { onSuccess: onClose })}>
            {tc('actions.confirm')}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <FormAlert message={link.error ? describeError(link.error) : null} />
        <AssetPicker ticket={ticket} value={assetId} onChange={setAssetId} />
      </div>
    </Dialog>
  );
}

function RelatedRequestDialog({ ticket, onClose }: { ticket: TicketDetail; onClose: () => void }) {
  const { t } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const navigate = useNavigate();
  const types = useReferenceRequestTypes();
  const create = useCreateRelatedRequest(ticket.number);
  const [typeId, setTypeId] = useState('');
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t('actions.relatedRequest')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button
            disabled={typeId === ''}
            isLoading={create.isPending}
            onClick={() => create.mutate(typeId, { onSuccess: (request) => void navigate(ticketPath(request.number)) })}
          >
            {tc('actions.create')}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <FormAlert message={create.error ? describeError(create.error) : null} />
        <SelectField
          label={t('fields.requestType')}
          placeholder={t('fields.select')}
          options={(types.data ?? []).map((r) => ({ value: r.id, label: r.name }))}
          value={typeId}
          onChange={(event) => setTypeId(event.target.value)}
        />
      </div>
    </Dialog>
  );
}

/** Related tickets and linked devices (both respect access scope on the server). */
export function TicketLinks({ ticket, isRequester }: { ticket: TicketDetail; isRequester: boolean }) {
  const { t } = useTranslation('tickets');
  const assets = useTicketAssets(ticket.number);
  const unlink = useUnlinkAsset(ticket.number);
  const [dialog, setDialog] = useState<'asset' | 'related' | null>(null);
  const open = ticket.status !== 'completed' && ticket.status !== 'cancelled';

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <section aria-labelledby="ticket-assets" className="flex flex-col gap-2">
        <h2 id="ticket-assets" className="text-lg font-semibold">
          {t('detail.assets')}
        </h2>
        <FormAlert message={unlink.error ? describeError(unlink.error) : null} />
        {(assets.data ?? []).length === 0 && <p className="text-slate-600">{t('detail.noAssets')}</p>}
        <ul className="flex flex-col gap-2">
          {(assets.data ?? []).map((a) => (
            <li key={a.assetId} className="flex items-center justify-between gap-2 rounded-lg border border-slate-200 bg-white p-2">
              <span>{`${a.assetTag} · ${a.brand} ${a.model}`}</span>
              {ticket.canWork && open && (
                <Button variant="ghost" aria-label={recordActionLabel(t('actions.unlinkAsset'), a.assetTag)} onClick={() => unlink.mutate(a.assetId)}>
                  {t('actions.unlinkAsset')}
                </Button>
              )}
            </li>
          ))}
        </ul>
        {open && (ticket.canWork || isRequester) && (
          <Button variant="secondary" className="self-start" onClick={() => setDialog('asset')}>
            {t('actions.linkAsset')}
          </Button>
        )}
      </section>
      <section aria-labelledby="ticket-related" className="flex flex-col gap-2">
        <h2 id="ticket-related" className="text-lg font-semibold">
          {t('detail.related')}
        </h2>
        <ul className="flex flex-col gap-2">
          {ticket.relatedTickets.map((r) => (
            <li key={r.number} className="flex items-center justify-between gap-2">
              <Link to={ticketPath(r.number)} className="text-brand-900 underline">
                {`${r.number} · ${r.subject}`}
              </Link>
              <TicketStatusBadge status={r.status} />
            </li>
          ))}
        </ul>
        {ticket.canWork && open && ticket.kind === 'incident' && (
          <Button variant="secondary" className="self-start" onClick={() => setDialog('related')}>
            {t('actions.relatedRequest')}
          </Button>
        )}
      </section>
      {dialog === 'asset' && <LinkAssetDialog ticket={ticket} onClose={() => setDialog(null)} />}
      {dialog === 'related' && <RelatedRequestDialog ticket={ticket} onClose={() => setDialog(null)} />}
    </div>
  );
}
