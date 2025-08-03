# Documentation Build Process

This documentation site automatically includes content from the main repository README.md file.

## How it works

1. **Build Script**: `scripts/copy-readme.js` runs during the build process
2. **Content Processing**: The script reads `../../README.md` and processes it for documentation use
3. **Component Generation**: Creates `src/components/ReadmeContent/index.tsx` with the processed content
4. **Main Index**: The `main-index.md` file serves as the homepage and includes the ReadmeContent component

## Build Commands

- `npm start` - Starts development server (includes README copying)
- `npm run build:prod` - Builds for production (includes README copying)
- `node ./scripts/copy-readme.js` - Manual README content update

## Content Processing

The build script automatically:

- Removes the main title (since it's in frontmatter)
- Updates internal links to point to documentation pages
- Removes the "Documentation Portal" section (redundant in docs)
- Updates project status dashboard links

## File Structure

```
docs/
├── main-index.md          # Homepage (routes to /)
├── index.md               # Introduction page  
└── ...                    # Other documentation pages

src/components/
└── ReadmeContent/
    └── index.tsx          # Auto-generated from README.md
```
