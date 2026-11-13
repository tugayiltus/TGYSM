/**
 * @input A label and a text value (or null).
 * @output A metadata row; each typed line is its own text line, so the author's line breaks stay.
 * @position Change detail and summary pages.
 */
import { MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

const LINE_BREAK = /\r?\n/;

export function Field({ label, value }: { label: string; value: string | null }) {
  const { t: tc } = useTranslation();
  const lines = (value ?? tc('none')).split(LINE_BREAK);
  return (
    <MetadataListItem label={label}>
      <VStack gap={0}>
        {lines.map((line, index) => (
          <Text key={index} wordBreak="break-word">
            {line}
          </Text>
        ))}
      </VStack>
    </MetadataListItem>
  );
}

/** A metadata row whose value is already a component (a badge, a window). */
export function NodeField({ label, children }: { label: string; children: ReactNode }) {
  return <MetadataListItem label={label}>{children}</MetadataListItem>;
}
