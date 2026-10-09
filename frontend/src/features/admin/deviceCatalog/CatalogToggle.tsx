import { useTranslation } from 'react-i18next';

import { Button } from '../../../shared/ui/Button';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';

/** Edit and deactivate/reactivate buttons of one catalog row; the row's name labels them for screen readers. */
export function CatalogRowActions({ name, isActive, onEdit, onToggle }: { name: string; isActive: boolean; onEdit: () => void; onToggle: () => void }) {
  const { t: tc } = useTranslation();
  const toggleLabel = isActive ? tc('actions.deactivate') : tc('actions.reactivate');
  return (
    <div className="d-flex flex-wrap gap-2">
      <Button variant="secondary" aria-label={recordActionLabel(tc('actions.edit'), name)} onClick={onEdit}>
        {tc('actions.edit')}
      </Button>
      <Button variant="secondary" aria-label={recordActionLabel(toggleLabel, name)} onClick={onToggle}>
        {toggleLabel}
      </Button>
    </div>
  );
}
