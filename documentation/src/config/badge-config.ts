// Auto-generated BadgeConfig TS class from badge-config.json
export interface TemplateVariables {
  demoUrl: string;
  docsUrl: string;
  user: string;
  repository: string;
}

export interface Badge {
  name: string;
  url: string;
  link: string;
}

export interface BadgeCategory {
  key: string;
  title: string;
  icon: string;
  badges: Badge[];
}

export class BadgeConfig {
  static templateVariables: TemplateVariables = {
    demoUrl: '',
    docsUrl: 'https://build-agent.subzerodev.com',
    user: 'the-running-dev',
    repository: 'Docker-BuildAgent',
  };

  static badgeCategories: BadgeCategory[] = [
    {
      key: 'buildRelease',
      title: 'Build & Release',
      icon: 'faCogs',
      badges: [
        {
          name: 'CI',
          url: 'https://github.com/{user}/{repository}/actions/workflows/ci.yml/badge.svg?branch=main',
          link: 'https://github.com/{user}/{repository}/actions/workflows/ci.yml',
        },
        {
          name: 'Release',
          url: 'https://github.com/{user}/{repository}/actions/workflows/release.yml/badge.svg?branch=main',
          link: 'https://github.com/{user}/{repository}/actions/workflows/release.yml',
        },
        {
          name: 'Tests',
          url: 'https://img.shields.io/badge/Tests-Passing-brightgreen?logo=github-actions&logoColor=white',
          link: 'https://github.com/{user}/{repository}/actions/workflows/ci.yml',
        },
        {
          name: 'Coverage',
          url: 'https://img.shields.io/badge/Coverage-Report-green?logo=codecov&logoColor=white',
          link: 'https://github.com/{user}/{repository}/actions/workflows/ci.yml',
        },
        {
          name: 'Quality Gate',
          url: 'https://img.shields.io/badge/Quality%20Gate-Passed-success?logo=sonarqube&logoColor=white',
          link: 'https://github.com/{user}/{repository}/actions',
        },
      ],
    },
    {
      key: 'distribution',
      title: 'Distribution & Deployment',
      icon: 'faBoxOpen',
      badges: [
        {
          name: 'Version',
          url: 'https://img.shields.io/github/v/release/{user}/{repository}?logo=semver&logoColor=white&label=Version',
          link: 'https://github.com/{user}/{repository}/releases/latest',
        },
        {
          name: 'Docker',
          url: 'https://img.shields.io/badge/Docker-GHCR-blue?logo=docker&logoColor=white',
          link: 'https://github.com/{user}/{repository}/pkgs/container/build-agent',
        },
        {
          name: 'Deployments',
          url: 'https://img.shields.io/github/deployments/{user}/{repository}/github-pages?logo=github&logoColor=white&label=Deployments',
          link: 'https://github.com/{user}/{repository}/deployments',
        },
        {
          name: 'Platform',
          url: 'https://img.shields.io/badge/Platform-Linux%20%7C%20Windows%20%7C%20macOS-lightgrey?logo=linux&logoColor=white',
          link: 'https://github.com/{user}/{repository}',
        },
        {
          name: 'Image Size',
          url: 'https://img.shields.io/badge/Image%20Size-Optimized-blue?logo=docker&logoColor=white',
          link: 'https://github.com/{user}/{repository}/pkgs/container/build-agent',
        },
        {
          name: 'Download Count',
          url: 'https://img.shields.io/github/downloads/{user}/{repository}/total?logo=github&logoColor=white&label=Downloads',
          link: 'https://github.com/{user}/{repository}/releases',
        },
      ],
    },
    {
      key: 'documentation',
      title: 'Documentation & Demo',
      icon: 'faBook',
      badges: [
        {
          name: 'Docs',
          url: 'https://img.shields.io/badge/Docs-Live-blue?logo=gitbook&logoColor=white',
          link: '{docsUrl}',
        },
        {
          name: 'Demo',
          url: 'https://img.shields.io/badge/Demo-Barstrad-green?logo=angular&logoColor=white',
          link: '{demoUrl}',
        },
        {
          name: 'Uptime',
          url: 'https://img.shields.io/badge/Uptime-99.9%25-brightgreen?logo=statuspage&logoColor=white',
          link: '{docsUrl}',
        },
        {
          name: 'Wiki',
          url: 'https://img.shields.io/badge/Wiki-Available-blue?logo=github&logoColor=white',
          link: 'https://github.com/{user}/{repository}/wiki',
        },
        {
          name: 'Changelog',
          url: 'https://img.shields.io/badge/Changelog-Updated-blue?logo=keepachangelog&logoColor=white',
          link: 'https://github.com/{user}/{repository}/blob/main/CHANGELOG.md',
        },
      ],
    },
    {
      key: 'quality',
      title: 'Quality & Security',
      icon: 'faShieldAlt',
      badges: [
        {
          name: 'Security',
          url: 'https://img.shields.io/badge/Security-Scanned-success?logo=security&logoColor=white',
          link: 'https://github.com/{user}/{repository}/security',
        },
        {
          name: 'License',
          url: 'https://img.shields.io/badge/License-MIT-blue?logo=opensourceinitiative&logoColor=white',
          link: 'https://github.com/{user}/{repository}/blob/main/LICENSE',
        },
        {
          name: 'Language',
          url: 'https://img.shields.io/github/languages/top/{user}/{repository}?logo=csharp&logoColor=white&label=Language',
          link: 'https://github.com/{user}/{repository}',
        },
        {
          name: 'Vulnerabilities',
          url: 'https://img.shields.io/badge/Vulnerabilities-0-success?logo=snyk&logoColor=white',
          link: 'https://github.com/{user}/{repository}/security',
        },
        {
          name: 'Dependencies',
          url: 'https://img.shields.io/badge/Dependencies-Up%20to%20Date-success?logo=renovatebot&logoColor=white',
          link: 'https://github.com/{user}/{repository}/security/dependabot',
        },
        {
          name: 'Code Quality',
          url: 'https://img.shields.io/badge/Code%20Quality-A-success?logo=codeclimate&logoColor=white',
          link: 'https://github.com/{user}/{repository}',
        },
      ],
    },
    {
      key: 'community',
      title: 'Community & Activity',
      icon: 'faUsers',
      badges: [
        {
          name: 'Stars',
          url: 'https://img.shields.io/github/stars/{user}/{repository}?logo=github&logoColor=white&label=Stars',
          link: 'https://github.com/{user}/{repository}/stargazers',
        },
        {
          name: 'Forks',
          url: 'https://img.shields.io/github/forks/{user}/{repository}?logo=github&logoColor=white&label=Forks',
          link: 'https://github.com/{user}/{repository}/network/members',
        },
        {
          name: 'Contributors',
          url: 'https://img.shields.io/github/contributors/{user}/{repository}?logo=github&logoColor=white&label=Contributors',
          link: 'https://github.com/{user}/{repository}/graphs/contributors',
        },
        {
          name: 'Issues',
          url: 'https://img.shields.io/github/issues/{user}/{repository}?logo=github&logoColor=white&label=Issues',
          link: 'https://github.com/{user}/{repository}/issues',
        },
        {
          name: 'PRs Welcome',
          url: 'https://img.shields.io/badge/PRs-Welcome-brightgreen?logo=github&logoColor=white',
          link: 'https://github.com/{user}/{repository}/pulls',
        },
        {
          name: 'Discussions',
          url: 'https://img.shields.io/github/discussions/{user}/{repository}?logo=github&logoColor=white&label=Discussions',
          link: 'https://github.com/{user}/{repository}/discussions',
        },
      ],
    },
    {
      key: 'metrics',
      title: 'Development Metrics',
      icon: 'faChartLine',
      badges: [
        {
          name: 'Commits',
          url: 'https://img.shields.io/github/commit-activity/m/{user}/{repository}?logo=git&logoColor=white&label=Commits',
          link: 'https://github.com/{user}/{repository}/commits/main',
        },
        {
          name: 'Last Commit',
          url: 'https://img.shields.io/github/last-commit/{user}/{repository}?logo=git&logoColor=white&label=Last%20Commit',
          link: 'https://github.com/{user}/{repository}/commits/main',
        },
        {
          name: 'Code Size',
          url: 'https://img.shields.io/github/languages/code-size/{user}/{repository}?logo=github&logoColor=white&label=Code%20Size',
          link: 'https://github.com/{user}/{repository}',
        },
        {
          name: 'Release Frequency',
          url: 'https://img.shields.io/github/release-date/{user}/{repository}?logo=github&logoColor=white&label=Latest%20Release',
          link: 'https://github.com/{user}/{repository}/releases',
        },
        {
          name: 'Repo Size',
          url: 'https://img.shields.io/github/repo-size/{user}/{repository}?logo=github&logoColor=white&label=Repo%20Size',
          link: 'https://github.com/{user}/{repository}',
        },
        {
          name: 'Lines of Code',
          url: 'https://img.shields.io/badge/Lines%20of%20Code-Dynamic-blue?logo=github&logoColor=white',
          link: 'https://github.com/{user}/{repository}',
        },
      ],
    },
  ];
}
