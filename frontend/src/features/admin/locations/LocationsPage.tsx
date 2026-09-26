import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';

import { useLocations, useSetLocationActive, type LocationItem } from './api';
import { LocationFormDialog, type LocationFormTarget } from './LocationFormDialog';
import { buildLocationTree, type LocationNode } from './locationTree';

interface TreeListProps {
  nodes: readonly LocationNode[];
  onEdit: (node: LocationNode) => void;
  onAddChild: (parentId: string) => void;
  onToggle: (item: LocationItem) => void;
}

function LocationTreeList({ nodes, onEdit, onAddChild, onToggle }: TreeListProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  return (
    <ul className="flex flex-col gap-2">
      {nodes.map((node) => (
        <li key={node.item.id}>
          <div className="flex flex-col gap-2 rounded-xl border border-slate-200 bg-white p-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex min-w-0 items-center gap-2">
              <span className="font-medium break-words">{node.item.name}</span>
              {!node.item.isActive && <StatusBadge isActive={false} />}
            </div>
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" onClick={() => onEdit(node)}>
                {tc('actions.edit')}
              </Button>
              {node.item.isActive && (
                <Button variant="secondary" onClick={() => onAddChild(node.item.id)}>
                  {t('locations.addChild')}
                </Button>
              )}
              <Button variant="ghost" onClick={() => onToggle(node.item)}>
                {node.item.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
              </Button>
            </div>
          </div>
          {node.children.length > 0 && (
            <div className="mt-2 border-l-2 border-slate-200 pl-3 sm:pl-6">
              <LocationTreeList nodes={node.children} onEdit={onEdit} onAddChild={onAddChild} onToggle={onToggle} />
            </div>
          )}
        </li>
      ))}
    </ul>
  );
}

export function LocationsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const locations = useLocations();
  const setActive = useSetLocationActive();
  const [target, setTarget] = useState<LocationFormTarget | null>(null);
  const [toggling, setToggling] = useState<LocationItem | null>(null);
  const roots = useMemo(() => buildLocationTree(locations.data ?? []), [locations.data]);

  const openToggle = (item: LocationItem) => {
    setActive.reset();
    setToggling(item);
  };

  return (
    <section className="flex flex-col gap-4" aria-label={t('locations.treeLabel')}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('locations.title')}</h1>
        <Button onClick={() => setTarget({ kind: 'create', parentId: null })}>{t('locations.new')}</Button>
      </div>

      {locations.isPending && <LoadingState />}
      {locations.isError && <ErrorState error={locations.error} onRetry={() => void locations.refetch()} />}
      {locations.data && roots.length === 0 && <EmptyState />}
      {roots.length > 0 && (
        <LocationTreeList
          nodes={roots}
          onEdit={(node) => setTarget({ kind: 'edit', node })}
          onAddChild={(parentId) => setTarget({ kind: 'create', parentId })}
          onToggle={openToggle}
        />
      )}

      {target !== null && (
        <LocationFormDialog
          key={target.kind === 'edit' ? target.node.item.id : `new-${target.parentId ?? 'root'}`}
          roots={roots}
          target={target}
          onClose={() => setTarget(null)}
        />
      )}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('locations.confirmDeactivate', { name: toggling.name }) : t('locations.confirmReactivate', { name: toggling.name })}
          confirmLabel={tc('actions.confirm')}
          variant={toggling.isActive ? 'danger' : 'primary'}
          isPending={setActive.isPending}
          error={setActive.error}
          onConfirm={() =>
            setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })
          }
          onCancel={() => setToggling(null)}
        />
      )}
    </section>
  );
}
