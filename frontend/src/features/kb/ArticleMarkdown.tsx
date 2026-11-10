/**
 * @input Article body text in Markdown.
 * @output The article rendered with Astryx text styles; raw HTML stays text.
 * @position Article page and the editor preview.
 */
import { Code } from '@astryxdesign/core/Code';
import { Heading } from '@astryxdesign/core/Heading';
import { Link } from '@astryxdesign/core/Link';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import Markdown, { type Components } from 'react-markdown';

const NEW_TAB = '_blank';

/** A remote image would let an author track every reader, so an image is shown as a link with its alt text. */
function ImageAsLink({ src, alt }: { src: string | undefined; alt: string | undefined }) {
  if (src === undefined || src === '') {
    return <Text type="inherit">{alt}</Text>;
  }
  return (
    <Link href={src} target={NEW_TAB}>
      {alt || src}
    </Link>
  );
}

/** The page shows the article title as its only H1, so Markdown headings move one level down (h6 stays h6). */
const COMPONENTS: Components = {
  h1: ({ children }) => <Heading level={2}>{children}</Heading>,
  h2: ({ children }) => <Heading level={3}>{children}</Heading>,
  h3: ({ children }) => <Heading level={4}>{children}</Heading>,
  h4: ({ children }) => <Heading level={5}>{children}</Heading>,
  h5: ({ children }) => <Heading level={6}>{children}</Heading>,
  h6: ({ children }) => <Heading level={6}>{children}</Heading>,
  p: ({ children }) => <Text wordBreak="break-word">{children}</Text>,
  a: ({ href, children }) => (
    <Link href={href} target={NEW_TAB}>
      {children}
    </Link>
  ),
  code: ({ children }) => <Code>{children}</Code>,
  img: ({ src, alt }) => <ImageAsLink src={typeof src === 'string' ? src : undefined} alt={alt} />,
};

/**
 * Article text is Markdown from an editor, not trusted HTML: no `rehype-raw`, so raw HTML (including script tags)
 * renders as text. react-markdown's default `urlTransform` drops unsafe URL protocols.
 */
export function ArticleMarkdown({ body }: { body: string }) {
  return (
    <VStack gap={2}>
      <Markdown components={COMPONENTS}>{body}</Markdown>
    </VStack>
  );
}
