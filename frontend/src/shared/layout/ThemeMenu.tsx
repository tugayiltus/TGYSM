import CIcon from '@coreui/icons-react';
import { CDropdown, CDropdownItem, CDropdownMenu, CDropdownToggle } from '@coreui/react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { COLOR_MODES, useColorMode, type ColorMode } from '../ui/colorMode';
import { cilContrast, cilMoon, cilSun } from '../ui/icons';

const MODE_ICON: Record<ColorMode, string[]> = { light: cilSun, dark: cilMoon, auto: cilContrast };

/** Header menu for the color mode (spec §2.2): the toggle shows the active mode's icon. */
export function ThemeMenu() {
  const { t } = useTranslation();
  const { mode, setMode } = useColorMode();
  // Items exist only while the menu is open, so a closed menu adds nothing to the page's accessibility tree.
  const [open, setOpen] = useState(false);
  return (
    <CDropdown variant="dropdown" placement="bottom-end" visible={open} onShow={() => setOpen(true)} onHide={() => setOpen(false)}>
      <CDropdownToggle
        caret={false}
        className="btn btn-ghost-secondary tgysm-touch d-inline-flex align-items-center"
        aria-label={`${t('layout.theme.label')}: ${t(`layout.theme.${mode}`)}`}
      >
        <CIcon icon={MODE_ICON[mode]} size="lg" aria-hidden="true" />
      </CDropdownToggle>
      <CDropdownMenu>
        {open &&
          COLOR_MODES.map((item) => (
            <CDropdownItem
              key={item}
              as="button"
              type="button"
              active={item === mode}
              aria-pressed={item === mode}
              className="d-flex align-items-center gap-2"
              onClick={() => setMode(item)}
            >
              <CIcon icon={MODE_ICON[item]} aria-hidden="true" />
              {t(`layout.theme.${item}`)}
            </CDropdownItem>
          ))}
      </CDropdownMenu>
    </CDropdown>
  );
}
