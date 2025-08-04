import * as fs from 'fs';
import * as path from 'path';

interface NavbarLink {
  label: string;
  to: string;
}

/**
 * PreBuild handles copying markdown from project root into src/pages
 * and regenerating the navbarLinks file. Use static getVersion() for date-based versioning.
 */
export class PreBuild {
  private projectRoot: string;
  private pagesDir: string;

  constructor() {
    // Project root is the parent directory of this scripts folder
    this.projectRoot = path.resolve(__dirname, '../..');
    this.pagesDir = path.join(path.resolve(__dirname, '..'), 'src', 'pages');
  }

  /**
   * Ensure the directory for a given file path exists, creating it if necessary.
   */
  private ensureDirectoryExists(filePath: string): void {
    const dir = path.dirname(filePath);

    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }
  }

  /**
   * Sanitize markdown content to ensure compatibility with Docusaurus
   */
  private sanitizeMarkdownContent(content: string): string {
    const contentLines = content.split('\n');
    const result: string[] = [];
    
    // Check if we need to add proper Docusaurus front matter
    // Docusaurus requires front matter to have proper properties for rendering
    let addedFrontMatter = false;
    let hasFrontMatterBlock = false;
    
    // Look for existing front matter block (content between --- markers)
    if (contentLines.length >= 2) {
      if (contentLines[0].trim() === '---') {
        // Check if there's a second --- to close the front matter
        const secondDashIndex = contentLines.slice(1).findIndex(line => line.trim() === '---');
        if (secondDashIndex !== -1) {
          hasFrontMatterBlock = true;
        }
      }
    }
    
    // Add proper Docusaurus front matter if none exists
    if (!hasFrontMatterBlock) {
      result.push('---');
      result.push('hide_table_of_contents: false');
      result.push('---');
      addedFrontMatter = true;
      console.log('➕ Added proper front matter for Docusaurus compatibility');
    }
    
    // Process the content line by line
    let skipLines = 0; // For skipping existing single --- lines
    
    for (let i = 0; i < contentLines.length; i++) {
      const line = contentLines[i];
      const trimmedLine = line.trim();
      
      // Skip processing if we're skipping lines (like a standalone ---)
      if (skipLines > 0) {
        skipLines--;
        continue;
      }
      
      // Special handling for horizontal rule markers
      if (trimmedLine === '---') {
        // If this is a standalone horizontal rule (not part of front matter)
        // Need to check surrounding context
        const prevLineEmpty = i === 0 || contentLines[i-1].trim() === '';
        const nextLineEmpty = i === contentLines.length - 1 || contentLines[i+1].trim() === '';
        
        if (prevLineEmpty && nextLineEmpty) {
          // This is likely a horizontal rule, convert to HTML
          result.push('<hr />');
          console.log('🔄 Replaced standalone "---" with <hr /> HTML tag');
        } else if (i === 0) {
          // First line is --- but we already added front matter
          if (addedFrontMatter) {
            // Skip this line as we've already added front matter
            skipLines = 0;
          } else {
            // This might be the start of front matter, keep it
            result.push(line);
          }
        } else {
          // Keep --- in other contexts (might be part of front matter or code blocks)
          result.push(line);
        }
      } else {
        // Regular content line
        result.push(line);
      }
    }
    
    return result.join('\n');
  }

  /**
   * Copy all markdown files from project root into src/pages, overwriting if present.
   */
  private copyAllRootMarkdown(): void {
    const mdFiles = fs.readdirSync(this.projectRoot).filter((f) => f.endsWith('.md'));

    mdFiles.forEach((file) => {
      const srcPath = path.join(this.projectRoot, file);
      // Rename README.md to index.md in destination directory
      const destFile = file.toLowerCase() === 'readme.md' ? 'index.md' : file;
      const dstPath = path.join(this.pagesDir, destFile);

      this.ensureDirectoryExists(dstPath);

      // Read the content and sanitize it
      const content = fs.readFileSync(srcPath, 'utf-8');
      const sanitizedContent = this.sanitizeMarkdownContent(content);
      
      if (!fs.existsSync(dstPath) || content !== sanitizedContent) {
        // Write the sanitized content to the destination file
        fs.writeFileSync(dstPath, sanitizedContent, 'utf-8');

        console.log(`✅ Processed ${file} --> src/pages/${destFile}`);
      } else {
        console.log(`ℹ️ Skipped ${file} --> src/pages/${destFile}`);
      }
    });
  }

  private filenameToLabel(filename: string): string {
    // Remove extension, replace -/_ with space, capitalize first letter
    return filename
      .replace(/\.(md|mdx)$/, '')
      .replace(/[-_]/g, ' ')
      .replace(/\b\w/g, (c) => c.toUpperCase());
  }

  /**
   * Generate navbarLinks.ts from the provided markdown files.
   */
  /**
   * Generate the navbarLinks.ts file based on markdown file names
   */
  private generateNavbar(mdFiles: string[]): void {
    const outPath = path.join(__dirname, '../src/navbarLinks.ts');
    let numberOfLinks = 0;

    // Exclude index.md (homepage) from navbar links
    const links: NavbarLink[] = mdFiles
      .filter((file) => path.parse(file).name.toLowerCase() !== 'index')
      .map((file) => {
        const name = path.parse(file).name;
        const toPath = `/${name}`;

        numberOfLinks++;

        return {
          label: this.filenameToLabel(file),
          to: toPath,
        };
      });

    const tsOutput = `// AUTO-GENERATED FILE. DO NOT EDIT.
export const navbarLinks = ${JSON.stringify(links, null, 2)} as const;
`;

    fs.writeFileSync(outPath, tsOutput, 'utf-8');

    console.log(`✅ Navbar Created with ${numberOfLinks} Entry(s)`);
  }

  /**
   * Main process: copy markdown files and update navbar links.
   */
  public process(): void {
    this.copyAllRootMarkdown();
    const mdFiles = fs.readdirSync(this.pagesDir).filter((f) => f.endsWith('.md'));

    this.generateNavbar(mdFiles);

    console.log('🚀 Pre Build Process Completed');
  }

  public static getVersion(): string {
    try {
      const now = new Date();
      const year = now.getFullYear();
      const month = String(now.getMonth() + 1).padStart(2, '0');
      const day = String(now.getDate()).padStart(2, '0');

      return `${year}.${month}.${day}`;
    } catch (error) {
      console.error('Error Generating Version:', error);

      return 'unknown';
    }
  }
}

// Execute when run directly
if (require.main === module) {
  new PreBuild().process();
}
