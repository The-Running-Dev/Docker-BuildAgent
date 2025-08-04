import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';
import { PreBuild } from './scripts/pre-build';
import { navbarLinks } from './src/navbarLinks';

const version = PreBuild.getVersion();
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
          customCss: './src/css/custom.css',
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
          label: `v${version}`,
          position: 'right',
          href: '#',
        },
        {
          href: 'https://github.com/The-Running-Dev/Docker-BuildAgent',
          label: 'GitHub',
          position: 'right',
        },
        {
          href: 'https://github.com/The-Running-Dev/Docker-BuildAgent/releases',
          label: 'Releases',
          position: 'right',
        },
        {
          href: 'https://ghcr.io/the-running-dev/build-agent',
          label: 'Container Registry',
          position: 'right',
        },
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
