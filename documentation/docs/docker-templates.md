---
id: docker-templates
title: Docker Templates
sidebar_position: 6
---

Canonical contract (Docker-template discovery): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

The Build Agent supports flexible Docker template discovery to make it easy to use custom templates in your projects. When you run a build and no `Dockerfile` exists in your project directory, the agent will:

1. Try to determine your application type from the files in the project root.
2. Search for matching templates in multiple locations (see [Template Discovery Order](#template-discovery-order)).
3. If a matching template exists (such as `Dockerfile.angular` or `Dockerfile.node`), it will be copied and used as your Dockerfile for the build.

The template file name is `Dockerfile.<type>`. The type is the first rule that matches:

| Type | Detected when |
|---|---|
| `angular` | `angular.json` exists |
| `nextjs` | `next.config.js` exists, or `package.json` lists `next` under dependencies |
| `nestjs` | `nest-cli.json` exists, or `package.json` lists `@nestjs/core` under dependencies |
| `vite` | `vite.config.ts` or `vite.config.js` exists |
| `react` | `package.json` lists `react-scripts` under dependencies |
| `express` | `package.json` lists `express` under dependencies |
| `node` | `tsconfig.json` exists |
| `unknown` | none of the above, or there is no `package.json` |

The Build Agent image ships two templates, `Dockerfile.angular` and `Dockerfile.node`. For any other type, supply
your own `Dockerfile.<type>` in a template directory, or put a `Dockerfile` in the project. When no template
matches, the build fails and the error lists the locations it searched.

## 🔍 Template Discovery Order

The Build Agent looks for `Dockerfile.<appType>` in these directories and uses the first one that contains it:

1. **The configured `TemplatesDir`** - the value as given when it names an existing directory, otherwise the same value resolved against your project root
2. **`<project-root>/.github/templates/`**
3. **`<project-root>/templates/`**
4. **`/nuke/templates/`** - the built-in templates inside the Build Agent image

`TemplatesDir` defaults to `/nuke/templates`, so with nothing set the first and last locations are the same directory. A template is only used when no Dockerfile exists at the configured path. When none of the four contains a matching template, the build fails and the error lists every location it searched.

This lets you:

- Store templates in your own repository (recommended)
- Override built-in templates with project-specific ones
- Fall back to container templates for quick starts

## Storing Templates in Your Repository

Store your custom templates in your project repository:

```text
your-project/
├── templates/
│   ├── Dockerfile.angular
│   └── Dockerfile.node
└── src/
    └── ...
```

Templates in the repository are versioned with your code and shared across your team. Nothing has to be mounted
into the container.

## Using Custom Template Directories

To use a template directory that is not in the repository, set the templates directory
(`--templates-dir`, the `TemplatesDir` variable or the `templates-dir` key; see [Parameters](./parameters.md)).
The path is read inside the container, so mount the directory first:

```bash
docker run --rm -it \
    -v "${PWD}:/workspace" \
    -v "${PWD}/my-templates:/custom-templates" \
    -v /var/run/docker.sock:/var/run/docker.sock \
    ghcr.io/the-running-dev/build-agent:latest \
    build docker --templates-dir /custom-templates
```

## Angular

The template serves a built Angular application with NGINX. It copies `./artifacts/browser` from the project root
into the image, so the Angular build output must be in that directory before the image build starts. The path
is set inside the template, so `--artifacts-dir` does not change it. The image exposes port 80 and runs NGINX in
the foreground.

[View Dockerfile](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/templates/Dockerfile.angular)

## Node.js

The template runs a built Node.js application. It copies `./artifacts` from the project root into
`/usr/src/app`, runs `npm install --omit-dev` there and starts the application with `npm start`, so the
artifacts directory needs a `package.json` with a `start` script. As with the Angular template, the path is set
inside the template and `--artifacts-dir` does not change it.

[View Dockerfile](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/templates/Dockerfile.node)
