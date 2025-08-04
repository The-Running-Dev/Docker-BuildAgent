import { IconDefinition } from '@fortawesome/fontawesome-svg-core';

export interface Badge {
  name: string;
  url: string;
  link: string;
}

export interface BadgeSection {
  key: string;
  title: string;
  icon: IconDefinition;
  badges: Badge[];
}

export interface UseBadgeConfigProps {
  user: string;
  repository: string;
  groups?: string[];
}

export interface UseBadgeConfigResult {
  badgeSections: BadgeSection[];
  loading: boolean;
}

export interface GitHubProjectBadgesProps {
  user: string;
  repository: string;
  groups?: string[]; // Optional array of group IDs to display
}
