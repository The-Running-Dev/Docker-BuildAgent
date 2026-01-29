import { themes as prismThemes } from 'prism-react-renderer';

import type { Config } from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

import { navbarLinks } from './src/navbarLinks';

const config: Config = {
  title: 'Build Agent',
  tagline: 'Smart automation for DevOps teams and CI/CD pipelines',
  url: 'https://build-agent.subzerodev.com',
  baseUrl: '/',
  trailingSlash: false,
  favicon: 'img/favicon.ico',
  projectName: 'build-agent',
  organizationName: 'the-running-dev',
  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',
  markdown: {
    mermaid: true,
  },
  themes: ['@docusaurus/theme-mermaid'],
  future: {
    v4: true,
  },
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },
  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: 'docs',
        },
        blog: false,
        theme: {
          customCss: require.resolve('./src/css/agent.css'),
        },
      } satisfies Preset.Options,
    ],
  ],
  themeConfig: {
    image: 'img/docusaurus-social-card.jpg',
    navbar: {
      title: 'Build Agent',
      logo: {
        alt: 'Build Agent Logo',
        src: 'img/logo.svg',
      },
      hideOnScroll: false,
      items: [
        {
          type: 'doc',
          docId: 'index',
          position: 'left',
          label: 'Docs',
        },
        {
          type: 'custom-gitHubLinks',
          position: 'right',
        },
        {
          type: 'custom-versionDisplay',
          position: 'right',
        },
        {
          type: 'custom-themeSwitcher',
          position: 'right',
        },
        // ...auto generated links,
        ...navbarLinks,
      ],
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
