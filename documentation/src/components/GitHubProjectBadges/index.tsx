// documentation/src/components/GitHubProjectBadges/index.tsx - GitHubProjectBadges component
import React from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { Badge, BadgeSection, GitHubProjectBadgesProps } from './models';
import useConfig from './useConfig';

const GitHubProjectBadges: React.FC<GitHubProjectBadgesProps> = ({
  user,
  repository,
  groups, // Array of group IDs to display
}) => {
  // Use custom hook to load badge configuration
  const { badgeSections, loading } = useConfig({ user, repository, groups });

  if (loading) {
    return (
      <div style={{ padding: '1rem 0', textAlign: 'center' }}>
        <p>Loading Badges...</p>
      </div>
    );
  }

  const BadgeSection: React.FC<BadgeSection> = ({ title, badges, icon }) => (
    <div style={{ marginBottom: '2rem' }}>
      <h3
        style={{
          fontSize: '1.2rem',
          marginBottom: '1rem',
          color: 'var(--ifm-color-primary)',
          borderBottom: '2px solid var(--ifm-color-primary-light)',
          paddingBottom: '0.5rem',
          display: 'flex',
          alignItems: 'center',
          gap: '0.5rem',
        }}
      >
        <FontAwesomeIcon icon={icon} style={{ color: 'var(--ifm-color-primary)' }} />
        {title}
      </h3>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '0.5rem',
          alignItems: 'center',
        }}
      >
        {badges.map((badge: Badge, index: number) => (
          <a
            key={index}
            href={badge.link}
            target="_blank"
            rel="noopener noreferrer"
            style={{ textDecoration: 'none' }}
          >
            <img
              src={badge.url}
              alt={badge.name}
              style={{
                height: '20px',
                transition: 'transform 0.2s ease',
                cursor: 'pointer',
              }}
              onMouseOver={(e: React.MouseEvent<HTMLImageElement>) => {
                (e.target as HTMLImageElement).style.transform = 'scale(1.05)';
              }}
              onMouseOut={(e: React.MouseEvent<HTMLImageElement>) => {
                (e.target as HTMLImageElement).style.transform = 'scale(1)';
              }}
            />
          </a>
        ))}
      </div>
    </div>
  );

  return (
    <div style={{ padding: '1rem 0' }}>
      {badgeSections.map((section) => (
        <BadgeSection
          key={section.key}
          title={section.title}
          badges={section.badges}
          icon={section.icon}
        />
      ))}
    </div>
  );
};

export default GitHubProjectBadges;
