import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { PageHeader } from '../../../shared/navigation/PageHeader';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
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
    <ul className="list-unstyled mb-0 d-flex flex-column gap-2">
      {nodes.map((node) => (
        <li key={node.item.id}>
          <div className="d-flex flex-column gap-2 rounded border bg-body p-2 flex-sm-row align-items-sm-center justify-content-sm-between">
            <div className="d-flex min-w-0 align-items-center gap-2">
              <span className="fw-medium text-break">{node.item.name}</span>
              {!node.item.isActive && <StatusBadge isActive={false} />}
            </div>
            <div className="d-flex flex-wrap gap-2">
              <Button aria-label={recordActionLabel(tc('actions.edit'), node.item.name)} variant="secondary" onClick={() => onEdit(node)}>
                {tc('actions.edit')}
              </Button>
              {node.item.isActive && (
                <Button aria-label={recordActionLabel(t('locations.addChild'), node.item.name)} variant="secondary" onClick={() => onAddChild(node.item.id)}>
                  {t('locations.addChild')}
                </Button>
              )}
              <Button aria-label={recordActionLabel(node.item.isActive ? tc('actions.deactivate') : tc('actions.reactivate'), node.item.name)} variant="ghost" onClick={() => onToggle(node.item)}>
                {node.item.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
              </Button>
            </div>
          </div>
          {node.children.length > 0 && (
            <div className="mt-2 border-l-2 pl-3">
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
    <section className="d-flex flex-column gap-3" aria-label={t('locations.treeLabel')}>
      <PageHeader titleOverride={t('locations.title')} actions={<Button onClick={() => setTarget({ kind: 'create', parentId: null })}>{t('locations.new')}</Button>} />

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
