import type { ComponentProps } from 'react';
import Markdown, { type Components } from 'react-markdown';

const LINK_CLASS = 'link-primary text-break';

type AnchorProps = ComponentProps<'a'>;
type ImageProps = ComponentProps<'img'>;

function ExternalLink({ href, children }: AnchorProps) {
  return (
    <a href={href} target="_blank" rel="noopener noreferrer" className={LINK_CLASS}>
      {children}
    </a>
  );
}

/** A remote image would let an author track every reader, so an image is shown as a link with its alt text. */
function ImageAsLink({ src, alt }: ImageProps) {
  if (typeof src !== 'string' || src === '') {
    return <span>{alt}</span>;
  }
  return (
    <a href={src} target="_blank" rel="noopener noreferrer" className={LINK_CLASS}>
      {alt || src}
    </a>
  );
}

/** The page already shows the article title as its only `<h1>`, so Markdown headings move one level down (h1→h2 … h6 stays h6). */
const COMPONENTS: Components = {
  h1: ({ children }) => <h2>{children}</h2>,
  h2: ({ children }) => <h3>{children}</h3>,
  h3: ({ children }) => <h4>{children}</h4>,
  h4: ({ children }) => <h5>{children}</h5>,
  h5: ({ children }) => <h6>{children}</h6>,
  h6: ({ children }) => <h6>{children}</h6>,
  a: ({ href, children }) => <ExternalLink href={href}>{children}</ExternalLink>,
  img: ({ src, alt }) => <ImageAsLink src={typeof src === 'string' ? src : undefined} alt={alt} />,
};

/**
 * Article text is Markdown from an editor, not trusted HTML: no `rehype-raw`, so raw HTML (including script tags)
 * renders as text; unsafe URL protocols are dropped by react-markdown's default `urlTransform`.
 */
export function ArticleMarkdown({ body }: { body: string }) {
  return (
    <div className="d-flex flex-column gap-2 text-break tgysm-markdown">
      <Markdown components={COMPONENTS}>{body}</Markdown>
    </div>
  );
}
