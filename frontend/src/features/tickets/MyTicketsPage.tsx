/**
 * @input Nothing.
 * @output The signed-in person's tickets with buttons to report a fault or ask for something.
 * @position `/tickets`, inside the app shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

import { TicketList } from './TicketList';

export function MyTicketsPage() {
  const { t } = useTranslation('tickets');
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.myTickets.icon}
        titleOverride={t('list.title')}
        actions={
          <>
            <Button label={t('list.newIncident')} variant="primary" href={ROUTES.newIncident} icon={<Icon icon={Icons.warning} />} />
            <Button label={t('list.newRequest')} variant="secondary" href={ROUTES.newRequest} icon={<Icon icon={Icons.plus} />} />
          </>
        }
      />
      <TicketList queue="mine" showWorkColumns={false} />
    </VStack>
  );
}
