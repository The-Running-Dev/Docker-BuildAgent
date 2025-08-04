import React from 'react';
import { GiscusConfig } from '../../config/giscus-config';
import GiscusComments from '../GiscusComments';

const GiscusWrapper: React.FC = () => {
  const config = GiscusConfig;

  if (!config) {
    return (
      <div style={{ padding: '20px', textAlign: 'center', color: '#666' }}>
        <p>💬 Comments are not configured yet.</p>
        <p>
          Create <code>src/config/giscus-config.json</code> to enable comments.
        </p>
        <p>
          <a
            href="https://giscus.app"
            target="_blank"
            rel="noopener noreferrer"
            style={{ color: '#0969da' }}
          >
            Configure Giscus →
          </a>
        </p>
      </div>
    );
  }

  return (
    <GiscusComments
      repo={config.repo as `${string}/${string}`}
      repoId={config.repoId}
      category={config.category}
      categoryId={config.categoryId}
      mapping={config.mapping as 'number' | 'pathname' | 'url' | 'title' | 'og:title' | 'specific'}
      reactionsEnabled={config.reactionsEnabled}
      emitMetadata={config.emitMetadata}
      inputPosition={config.inputPosition as 'bottom' | 'top'}
      lang={config.lang}
      loading={config.loading as 'lazy' | 'eager'}
    />
  );
};

export default GiscusWrapper;
