// scripts/copy-readme.js
const fs = require('fs');
const path = require('path');

// Read the main README.md file
const readmePath = path.join(__dirname, '../../README.md');
const readmeContent = fs.readFileSync(readmePath, 'utf8');

// Process the README content to adapt it for documentation
function processReadmeContent(content) {
  // Remove the first heading since we'll add our own
  let processed = content.replace(/^# Docker-BuildAgent\s*\n+---\s*\n+/, '');
  
  // Update internal links to point to documentation pages
  processed = processed.replace(/\(https:\/\/build-agent\.subzerodev\.com\/([^)]+)\)/g, '($1)');
  
  // Remove the documentation portal section since this IS the documentation
  // processed = processed.replace(/## 📚 Documentation Portal[\s\S]*?For the most up-to-date and detailed information, always refer to the documentation site\. The rest of this README provides a high-level summary\.\s*\n+/, '');
  
  // Update the project status dashboard link
  processed = processed.replace(/\[project-status\]/g, '[project-status](project-status)');
  
  return processed;
}

const processedContent = processReadmeContent(readmeContent);

// Create the TypeScript component with the README content
const componentContent = `// documentation/src/components/ReadmeContent/index.tsx
// This file is auto-generated during build. Do not edit manually.
import React from 'react';
import Markdown from 'markdown-to-jsx';

const readmeContent = \`${processedContent.replace(/`/g, '\\`').replace(/\$/g, '\\$')}\`;

const ReadmeContent: React.FC = () => {
  return (
    <div className="readme-content">
      <Markdown options={{
        overrides: {
          // Custom link rendering to handle internal navigation
          a: {
            component: ({ href, children, ...props }) => {
              // Handle internal documentation links
              if (href && !href.startsWith('http') && !href.startsWith('mailto:')) {
                return <a href={href} {...props}>{children}</a>;
              }
              // External links open in new tab
              return <a href={href} target="_blank" rel="noopener noreferrer" {...props}>{children}</a>;
            }
          }
        }
      }}>{readmeContent}</Markdown>
    </div>
  );
};

export default ReadmeContent;`;

// Write the component file
const componentPath = path.join(__dirname, '../src/components/ReadmeContent/index.tsx');
fs.writeFileSync(componentPath, componentContent, 'utf8');

console.log('✅ README content copied to ReadmeContent component');
