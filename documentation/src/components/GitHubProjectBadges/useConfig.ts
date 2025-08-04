import { useMemo } from 'react';
import { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import {
  faCogs,
  faBoxOpen,
  faBook,
  faShieldAlt,
  faUsers,
  faChartLine,
} from '@fortawesome/free-solid-svg-icons';
import { BadgeConfig } from '../../config/badge-config';
import { UseBadgeConfigProps, UseBadgeConfigResult } from './models';

export default function useConfig({
  user,
  repository,
  groups,
}: UseBadgeConfigProps): UseBadgeConfigResult {
  const iconMap: Record<string, IconDefinition> = {
    faCogs,
    faBoxOpen,
    faBook,
    faShieldAlt,
    faUsers,
    faChartLine,
  };

  // Compute badge sections based on config and props
  const badgeSections = useMemo(() => {
    const replacements: Record<string, string> = {
      ...BadgeConfig.templateVariables,
      user: user || BadgeConfig.templateVariables.user,
      repository: repository || BadgeConfig.templateVariables.repository,
    };
    return BadgeConfig.badgeCategories
      .filter((category) => !groups || groups.includes(category.key))
      .map((category) => ({
        key: category.key,
        title: category.title,
        icon: iconMap[category.icon] || faCogs,
        badges: category.badges.map((badge) => ({
          name: badge.name,
          url: badge.url.replace(/\{(\w+)\}/g, (_, k) => replacements[k] || ''),
          link: badge.link.replace(/\{(\w+)\}/g, (_, k) => replacements[k] || ''),
        })),
      }));
  }, [user, repository, groups]);

  return { badgeSections, loading: false };
}
