/**
 * @input Nothing; reads and sets the app color mode.
 * @output An icon button whose menu picks light, dark or the system mode.
 * @position Top bar of the app shell.
 */
import { DropdownMenu, DropdownMenuRadioGroup, DropdownMenuRadioItem } from '@astryxdesign/core/DropdownMenu';
import { Icon } from '@astryxdesign/core/Icon';
import { useTranslation } from 'react-i18next';

import { COLOR_MODES, useColorMode, type ColorMode } from '../ui/colorMode';
import { Icons, type AppIcon } from '../ui/icons';

const MODE_ICON: Record<ColorMode, AppIcon> = { light: 'sun', dark: 'moon', auto: 'contrast' };

const isColorMode = (value: string): value is ColorMode => (COLOR_MODES as readonly string[]).includes(value);

export function ThemeMenu() {
  const { t } = useTranslation();
  const { mode, setMode } = useColorMode();
  return (
    <DropdownMenu
      hasChevron={false}
      alignment="end"
      button={{ label: `${t('layout.theme.label')}: ${t(`layout.theme.${mode}`)}`, variant: 'ghost', isIconOnly: true, icon: <Icon icon={Icons[MODE_ICON[mode]]} /> }}
    >
      <DropdownMenuRadioGroup label={t('layout.theme.label')} value={mode} onChange={(value: string) => isColorMode(value) && setMode(value)}>
        {COLOR_MODES.map((item) => (
          <DropdownMenuRadioItem key={item} value={item} label={t(`layout.theme.${item}`)} icon={Icons[MODE_ICON[item]]} />
        ))}
      </DropdownMenuRadioGroup>
    </DropdownMenu>
  );
}
