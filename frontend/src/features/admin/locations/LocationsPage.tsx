/**
 * @input Nothing; loads all locations.
 * @output The location tree with edit, add-child and (de)activate per node.
 * @position `/admin/locations`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Section } from '@astryxdesign/core/Section';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { ActiveToken } from '../../../shared/page/ActiveToken';
import { RecordActions } from '../../../shared/page/RecordActions';
import { Icons } from '../../../shared/ui/icons';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';

import { useLocations, useSetLocationActive, type LocationItem } from './api';
import { LocationFormDialog, type LocationFormTarget } from './LocationFormDialog';
import { buildLocationTree, type LocationNode } from './locationTree';

const SKELETON_ROWS = 5;
/** Each tree level indents by this spacing step, with a line at its start. */
const LEVEL_INDENT = 4;

interface TreeListProps {
  nodes: readonly LocationNode[];
  onEdit: (node: LocationNode) => void;
  onAddChild: (parentId: string) => void;
  onToggle: (item: LocationItem) => void;
}

function LocationTreeList({ nodes, onEdit, onAddChild, onToggle }: TreeListProps) {
  const { t } = useTranslation('admin');
  return (
    <VStack gap={2} role="list">
      {nodes.map((node) => (
        <VStack key={node.item.id} gap={2} role="listitem">
          <Card padding={3}>
            <HStack gap={2} hAlign="between" vAlign="center" wrap="wrap">
              <HStack gap={2} vAlign="center">
                <Text type="label" wordBreak="break-word">
                  {node.item.name}
                </Text>
                {!node.item.isActive && <ActiveToken isActive={false} />}
              </HStack>
              <RecordActions
                name={node.item.name}
                isActive={node.item.isActive}
                onEdit={() => onEdit(node)}
                onToggle={() => onToggle(node.item)}
                extra={
                  node.item.isActive && (
                    <Button
                      label={t('locations.addChild')}
                      aria-label={recordActionLabel(t('locations.addChild'), node.item.name)}
                      size="sm"
                      variant="secondary"
                      onClick={() => onAddChild(node.item.id)}
                    />
                  )
                }
              />
            </HStack>
          </Card>
          {node.children.length > 0 && (
            <Section variant="transparent" padding={0} paddingInlineStart={LEVEL_INDENT} dividers={['start']}>
              <LocationTreeList nodes={node.children} onEdit={onEdit} onAddChild={onAddChild} onToggle={onToggle} />
            </Section>
          )}
        </VStack>
      ))}
    </VStack>
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

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.adminLocations.icon}
        titleOverride={t('locations.title')}
        actions={<Button label={t('locations.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setTarget({ kind: 'create', parentId: null })} />}
      />
      <VStack gap={2} role="region" aria-label={t('locations.treeLabel')}>
        {locations.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {locations.isError && <ErrorState error={locations.error} onRetry={() => void locations.refetch()} />}
        {locations.data && roots.length === 0 && <EmptyState />}
        {roots.length > 0 && (
          <LocationTreeList
            nodes={roots}
            onEdit={(node) => setTarget({ kind: 'edit', node })}
            onAddChild={(parentId) => setTarget({ kind: 'create', parentId })}
            onToggle={(item) => {
              setActive.reset();
              setToggling(item);
            }}
          />
        )}
      </VStack>

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
          onConfirm={() => setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })}
          onCancel={() => setToggling(null)}
        />
      )}
    </VStack>
  );
}
