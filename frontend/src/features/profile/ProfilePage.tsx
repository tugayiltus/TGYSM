/**
 * @input Nothing; reads the session.
 * @output The signed-in person's name, role, e-mail, department and location, with a link to change the password.
 * @position `/profile`, inside the app shell.
 */
import { Avatar } from '@astryxdesign/core/Avatar';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { useSession } from '../auth/session';

/** Readable width for a short details card. */
const CARD_MAX_WIDTH = 640;

export function ProfilePage() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const me = useSession();
  const fullName = `${me.firstName} ${me.lastName}`;
  const role = tc(`roles.${me.role}`);
  const rows = [
    { label: t('profile.name'), value: fullName },
    { label: t('profile.email'), value: me.email },
    { label: t('profile.role'), value: role },
    { label: t('profile.department'), value: me.departmentName ?? t('profile.notSet') },
    { label: t('profile.location'), value: me.locationName ?? t('profile.notSet') },
  ];

  return (
    <VStack gap={4}>
      <PageHeader icon="user" actions={<Button label={t('profile.changePassword')} variant="secondary" href={ROUTES.changePassword} icon={<Icon icon={Icons.lock} />} />} />
      <Card padding={6} maxWidth={CARD_MAX_WIDTH}>
        <VStack gap={5}>
          <HStack gap={3} vAlign="center">
            <Avatar name={fullName} size="lg" tooltip={false} />
            <VStack gap={0.5}>
              <Heading level={2}>{fullName}</Heading>
              <Text type="supporting">{role}</Text>
            </VStack>
          </HStack>
          <MetadataList>
            {rows.map((row) => (
              <MetadataListItem key={row.label} label={row.label}>
                <Text wordBreak="break-word">{row.value}</Text>
              </MetadataListItem>
            ))}
          </MetadataList>
        </VStack>
      </Card>
    </VStack>
  );
}
