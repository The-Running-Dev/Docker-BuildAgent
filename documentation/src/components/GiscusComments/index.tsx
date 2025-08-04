import React from 'react';
import Giscus from '@giscus/react';
import { useColorMode } from '@docusaurus/theme-common';

export interface GiscusCommentsProps {
  repo: `${string}/${string}`;
  repoId: string;
  category: string;
  categoryId: string;
  mapping?: 'pathname' | 'url' | 'title' | 'og:title' | 'specific' | 'number';
  term?: string;
  reactionsEnabled?: boolean;
  emitMetadata?: boolean;
  inputPosition?: 'top' | 'bottom';
  lang?: string;
  loading?: 'lazy' | 'eager';
}

const GiscusComments: React.FC<GiscusCommentsProps> = ({
  repo,
  repoId,
  category,
  categoryId,
  mapping = 'pathname',
  term,
  reactionsEnabled = true,
  emitMetadata = false,
  inputPosition = 'bottom',
  lang = 'en',
  loading = 'lazy',
}) => {
  const { colorMode } = useColorMode();

  return (
    <Giscus
      id="comments"
      repo={repo}
      repoId={repoId}
      category={category}
      categoryId={categoryId}
      mapping={mapping}
      term={term}
      reactionsEnabled={reactionsEnabled ? '1' : '0'}
      emitMetadata={emitMetadata ? '1' : '0'}
      inputPosition={inputPosition}
      theme={colorMode === 'dark' ? 'dark' : 'light'}
      lang={lang}
      loading={loading}
    />
  );
};

export default GiscusComments;
