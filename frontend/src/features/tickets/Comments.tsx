/**
 * @input A ticket and its comments.
 * @output The comment thread with attachments, and the reply box (public reply or internal note).
 * @position "Yazışmalar" card on the ticket detail page.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { FormAlert } from '../../shared/feedback/FormAlert';
import { formatDateTime } from '../../shared/lib/format';

import { useAddComment, type CommentItem, type TicketDetail, type Visibility } from './api';
import { AttachmentInput } from './AttachmentInput';
import { describeCommentError } from './commentErrors';
import { ReplyArticlePicker } from './ReplyArticlePicker';

const ATTACHMENTS_PATH = 'attachments';
const LINE_BREAK = /\r?\n/;
const VISIBILITIES: readonly Visibility[] = ['public', 'internal'];

const attachmentUrl = (number: string, id: string) => `/api/v1/tickets/${number}/${ATTACHMENTS_PATH}/${id}`;
const isVisibility = (value: string): value is Visibility => (VISIBILITIES as readonly string[]).includes(value);

export function CommentList({ ticket, comments }: { ticket: TicketDetail; comments: readonly CommentItem[] }) {
  const { t } = useTranslation('tickets');
  if (comments.length === 0) {
    return <Text type="supporting">{t('detail.noComments')}</Text>;
  }
  return (
    <VStack gap={2}>
      {comments.map((comment) => (
        <Card key={comment.id} padding={3} variant={comment.visibility === 'internal' ? 'yellow' : 'muted'}>
          <VStack gap={1}>
            <HStack gap={2} vAlign="center" wrap="wrap">
              <Text type="label">{comment.authorName}</Text>
              <Text type="supporting">{formatDateTime(comment.createdAt)}</Text>
              {comment.visibility === 'internal' && <Token size="sm" color="yellow" label={t('detail.internal')} />}
            </HStack>
            {comment.body &&
              comment.body.split(LINE_BREAK).map((line, index) => (
                <Text key={index} wordBreak="break-word">
                  {line}
                </Text>
              ))}
            {comment.attachments.length > 0 && (
              <HStack gap={3} wrap="wrap">
                {comment.attachments.map((attachment) => (
                  // A native download link: the router must not handle a file URL.
                  <a key={attachment.id} href={attachmentUrl(ticket.number, attachment.id)} download>
                    <Text type="supporting" color="accent">
                      {attachment.originalName}
                    </Text>
                  </a>
                ))}
              </HStack>
            )}
          </VStack>
        </Card>
      ))}
    </VStack>
  );
}

/** People working on the ticket also get an internal-note tab. */
export function CommentBox({ ticket }: { ticket: TicketDetail }) {
  const { t } = useTranslation('tickets');
  const add = useAddComment();
  const [visibility, setVisibility] = useState<Visibility>('public');
  const [body, setBody] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [formKey, setFormKey] = useState(0);

  const submit = () =>
    add.mutate(
      { number: ticket.number, body, visibility, files },
      {
        onSuccess: () => {
          setBody('');
          setFiles([]);
          setFormKey((key) => key + 1);
        },
      },
    );

  return (
    // Inside the "Yazışmalar" card: the form keeps that name for screen readers and tests.
    <form
      aria-label={t('detail.comments')}
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      <VStack gap={3}>
        {ticket.canWork && (
          <TabList role="tablist" aria-label={t('comment.tabs')} value={visibility} onChange={(value) => isVisibility(value) && !add.isPending && setVisibility(value)}>
            {VISIBILITIES.map((tab) => (
              <Tab key={tab} value={tab} label={t(`comment.${tab}`)} />
            ))}
          </TabList>
        )}
        <FormAlert message={add.error ? describeCommentError(add.error) : null} />
        {/* Locked while sending: the success handler clears the box, which would drop anything typed meanwhile. */}
        <TextArea label={t('comment.body')} value={body} isDisabled={add.isPending} onChange={setBody} />
        <ReplyArticlePicker onInsert={(text) => setBody((current) => (current.trim() === '' ? text : `${current}\n${text}`))} />
        <AttachmentInput key={formKey} label={t('comment.files')} hint={t('new.filesHint')} files={files} onChange={setFiles} />
        <HStack>
          <Button type="submit" label={t('comment.send')} variant="primary" isDisabled={body.trim() === '' && files.length === 0} isLoading={add.isPending} />
        </HStack>
      </VStack>
    </form>
  );
}
