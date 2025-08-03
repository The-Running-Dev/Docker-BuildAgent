#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

// Read badge configuration
const badgeConfig = JSON.parse(fs.readFileSync('badges.json', 'utf8'));
const { project, badges } = badgeConfig;

// Badge generation functions
function generateWorkflowBadge(badge) {
  const url = `https://github.com/${project.owner}/${project.repository}/actions/workflows/${badge.workflow}/badge.svg?branch=${badge.branch}`;
  const link = `https://github.com/${project.owner}/${project.repository}/actions/workflows/${badge.workflow}`;
  return `[![${badge.name}](${url})](${link})`;
}

function generateCustomBadge(badge) {
  const url = `https://img.shields.io/badge/${badge.label}-${encodeURIComponent(badge.message)}-${badge.color}?logo=${badge.logo}&logoColor=white`;
  const link = badge.link ? `https://github.com/${project.owner}/${project.repository}${badge.link}` : '#';
  return `[![${badge.name}](${url})](${link})`;
}

function generateGithubBadge(badge) {
  const metrics = {
    'release': `https://img.shields.io/github/v/release/${project.owner}/${project.repository}?logo=${badge.logo || 'github'}&logoColor=white&label=${badge.name}`,
    'stars': `https://img.shields.io/github/stars/${project.owner}/${project.repository}?logo=github&logoColor=white&label=${badge.name}`,
    'forks': `https://img.shields.io/github/forks/${project.owner}/${project.repository}?logo=github&logoColor=white&label=${badge.name}`,
    'issues': `https://img.shields.io/github/issues/${project.owner}/${project.repository}?logo=github&logoColor=white&label=${badge.name}`
  };
  
  const links = {
    'release': `/releases/latest`,
    'stars': `/stargazers`, 
    'forks': `/network/members`,
    'issues': `/issues`
  };
  
  const url = metrics[badge.metric];
  const link = `https://github.com/${project.owner}/${project.repository}${links[badge.metric]}`;
  return `[![${badge.name}](${url})](${link})`;
}

function generateBadge(badge) {
  switch (badge.type) {
    case 'workflow': return generateWorkflowBadge(badge);
    case 'custom': return generateCustomBadge(badge);  
    case 'github': return generateGithubBadge(badge);
    default: return '';
  }
}

// Generate badge sections
function generateBadgeSection(sectionName, sectionBadges, emoji, title) {
  const badgeMarkdown = sectionBadges.map(generateBadge).join('\n');
  return `### ${emoji} ${title}\n\n${badgeMarkdown}`;
}

// Generate complete project status section
function generateProjectStatus() {
  const sections = [
    generateBadgeSection('buildRelease', badges.buildRelease, '🔄', 'Build & Release'),
    generateBadgeSection('distribution', badges.distribution, '📦', 'Distribution & Deployment'), 
    generateBadgeSection('quality', badges.quality, '🔒', 'Quality & Security'),
    generateBadgeSection('community', badges.community, '👥', 'Community & Activity')
  ];
  
  return `## 📊 Project Status\n\n${sections.join('\n\n')}`;
}

// Update README.md
function updateReadme() {
  const readmePath = 'README.md';
  let readme = fs.readFileSync(readmePath, 'utf8');
  
  const projectStatusSection = generateProjectStatus();
  
  // Replace existing project status section
  const startMarker = '## 📊 Project Status';
  const endMarker = '\n---\n';
  
  const startIndex = readme.indexOf(startMarker);
  const endIndex = readme.indexOf(endMarker, startIndex) + endMarker.length;
  
  if (startIndex !== -1 && endIndex !== -1) {
    readme = readme.substring(0, startIndex) + projectStatusSection + '\n\n---\n' + readme.substring(endIndex);
    fs.writeFileSync(readmePath, readme);
    console.log('✅ README.md updated successfully');
  } else {
    console.log('❌ Could not find project status section in README.md');
  }
}

// Run the update
updateReadme();
